using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyPhotoHelper.Data;
using MyPhotoHelper.Models;

namespace MyPhotoHelper.Services
{
    public class DuplicateGroup
    {
        public string FileHash { get; set; } = "";
        public long TotalSize { get; set; }
        public int Count { get; set; }
        public List<tbl_images> Images { get; set; } = new();
        public long PotentialSavings => TotalSize - (Images.FirstOrDefault()?.FileSizeBytes ?? 0);
    }

    public class DuplicateStatistics
    {
        public int TotalDuplicateGroups { get; set; }
        public int TotalDuplicateFiles { get; set; }
        public long TotalWastedSpace { get; set; }
        public List<DuplicateGroup> DuplicateGroups { get; set; } = new();
        
        public string FormattedWastedSpace => FormatBytes(TotalWastedSpace);
        
        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            
            return $"{size:0.##} {sizes[order]}";
        }
    }

    public interface IDuplicateDetectionService
    {
        Task<DuplicateStatistics> GetDuplicateStatisticsAsync();
        Task<List<DuplicateGroup>> FindDuplicatesAsync();
        Task<DuplicateGroup?> GetDuplicateGroupAsync(string fileHash);
    }

    public class DuplicateDetectionService : IDuplicateDetectionService
    {
        private readonly MyPhotoHelperDbContext _context;
        private readonly ILogger<DuplicateDetectionService> _logger;
        private readonly IPhotoPathService _photoPathService;

        public DuplicateDetectionService(
            MyPhotoHelperDbContext context,
            ILogger<DuplicateDetectionService> logger,
            IPhotoPathService photoPathService)
        {
            _context = context;
            _logger = logger;
            _photoPathService = photoPathService;
        }

        public async Task<DuplicateStatistics> GetDuplicateStatisticsAsync()
        {
            var duplicateGroups = await FindDuplicatesAsync();
            
            var stats = new DuplicateStatistics
            {
                DuplicateGroups = duplicateGroups,
                TotalDuplicateGroups = duplicateGroups.Count,
                TotalDuplicateFiles = duplicateGroups.Sum(g => g.Count),
                TotalWastedSpace = duplicateGroups.Sum(g => g.PotentialSavings)
            };

            return stats;
        }

        public async Task<List<DuplicateGroup>> FindDuplicatesAsync()
        {
            // Find all file hashes that appear more than once
            var duplicateHashes = await _context.tbl_images
                .Where(img => img.FileExists == 1 && 
                             img.IsDeleted == 0 && 
                             img.FileHash != null && 
                             img.FileHash != "")
                .GroupBy(img => img.FileHash)
                .Where(g => g.Count() > 1)
                .Select(g => new
                {
                    Hash = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            _logger.LogInformation($"Found {duplicateHashes.Count} duplicate hash groups");

            var duplicateGroups = new List<DuplicateGroup>();

            foreach (var hashGroup in duplicateHashes)
            {
                if (hashGroup.Hash == null) continue;

                var images = await _context.tbl_images
                    .Include(img => img.ScanDirectory)
                    .Include(img => img.tbl_image_metadata)
                    .Where(img => img.FileHash == hashGroup.Hash && 
                                 img.FileExists == 1 && 
                                 img.IsDeleted == 0)
                    .ToListAsync();
                
                // Smart sorting: prioritize originals over copies
                images = images.OrderBy(img => 
                {
                    var fileName = img.FileName.ToLowerInvariant();
                    var nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(fileName);
                    
                    // Highest priority (0): Original files without copy indicators
                    if (!nameWithoutExt.Contains("copy") && 
                        !nameWithoutExt.Contains("(1)") && 
                        !nameWithoutExt.Contains("(2)") &&
                        !nameWithoutExt.Contains(" - copy") &&
                        !nameWithoutExt.Contains("_copy") &&
                        !nameWithoutExt.Contains("-copy"))
                    {
                        return 0;
                    }
                    
                    // Lower priority (1): Files with numbers in parentheses
                    if (System.Text.RegularExpressions.Regex.IsMatch(nameWithoutExt, @"\(\d+\)"))
                    {
                        return 1;
                    }
                    
                    // Lowest priority (2): Files with "copy" in the name
                    if (nameWithoutExt.Contains("copy"))
                    {
                        return 2;
                    }
                    
                    // Default priority
                    return 0;
                })
                .ThenBy(img => img.DateCreated) // Then by date for files with same priority
                .ToList();

                if (images.Count > 1)
                {
                    var group = new DuplicateGroup
                    {
                        FileHash = hashGroup.Hash,
                        Count = images.Count,
                        Images = images,
                        TotalSize = images.Sum(img => (long)img.FileSizeBytes)
                    };

                    duplicateGroups.Add(group);
                }
            }

            // Sort by potential savings (descending)
            return duplicateGroups
                .OrderByDescending(g => g.PotentialSavings)
                .ToList();
        }

        public async Task<DuplicateGroup?> GetDuplicateGroupAsync(string fileHash)
        {
            var images = await _context.tbl_images
                .Include(img => img.ScanDirectory)
                .Include(img => img.tbl_image_metadata)
                .Where(img => img.FileHash == fileHash && 
                             img.FileExists == 1 && 
                             img.IsDeleted == 0)
                .ToListAsync();

            if (images.Count <= 1)
                return null;
            
            // Apply same smart sorting
            images = images.OrderBy(img => 
            {
                var fileName = img.FileName.ToLowerInvariant();
                var nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(fileName);
                
                // Highest priority (0): Original files without copy indicators
                if (!nameWithoutExt.Contains("copy") && 
                    !nameWithoutExt.Contains("(1)") && 
                    !nameWithoutExt.Contains("(2)") &&
                    !nameWithoutExt.Contains(" - copy") &&
                    !nameWithoutExt.Contains("_copy") &&
                    !nameWithoutExt.Contains("-copy"))
                {
                    return 0;
                }
                
                // Lower priority (1): Files with numbers in parentheses
                if (System.Text.RegularExpressions.Regex.IsMatch(nameWithoutExt, @"\(\d+\)"))
                {
                    return 1;
                }
                
                // Lowest priority (2): Files with "copy" in the name
                if (nameWithoutExt.Contains("copy"))
                {
                    return 2;
                }
                
                // Default priority
                return 0;
            })
            .ThenBy(img => img.DateCreated)
            .ToList();

            return new DuplicateGroup
            {
                FileHash = fileHash,
                Count = images.Count,
                Images = images,
                TotalSize = images.Sum(img => (long)img.FileSizeBytes)
            };
        }
    }
}