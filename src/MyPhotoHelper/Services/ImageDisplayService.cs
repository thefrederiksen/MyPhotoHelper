using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyPhotoHelper.Data;

namespace MyPhotoHelper.Services
{
    public class ImageDisplayService : IImageDisplayService
    {
        private readonly IDbContextFactory<MyPhotoHelperDbContext> _contextFactory;
        private readonly IThumbnailService _thumbnailService;
        private readonly IThumbnailCacheService _thumbnailCacheService;

        public ImageDisplayService(
            IDbContextFactory<MyPhotoHelperDbContext> contextFactory,
            IThumbnailService thumbnailService,
            IThumbnailCacheService thumbnailCacheService)
        {
            _contextFactory = contextFactory;
            _thumbnailService = thumbnailService;
            _thumbnailCacheService = thumbnailCacheService;
        }

        public async Task<string?> GetThumbnailDataUriAsync(int imageId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                
                var image = await context.tbl_images
                    .Include(img => img.ScanDirectory)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(img => img.ImageId == imageId && img.IsDeleted == 0 && img.FileExists == 1);

                if (image?.ScanDirectory == null)
                {
                    return null;
                }

                // Build the full path
                var normalizedRelativePath = image.RelativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                var fullPath = Path.Combine(image.ScanDirectory.DirectoryPath, normalizedRelativePath);
                
                if (!File.Exists(fullPath))
                {
                    return null;
                }

                // Use the unified thumbnail cache service for all image types
                byte[]? thumbnailBytes = await _thumbnailCacheService.GetCachedThumbnailAsync(fullPath, 200);

                if (thumbnailBytes == null || thumbnailBytes.Length == 0)
                {
                    return null;
                }

                // Convert to base64 data URI
                var base64 = Convert.ToBase64String(thumbnailBytes);
                return $"data:image/jpeg;base64,{base64}";
            }
            catch (Exception ex)
            {
                Logger.Error($"Error generating thumbnail data URI for image {imageId}: {ex.Message}");
                return null;
            }
        }

        public async Task<string?> GetImageFullPathAsync(int imageId)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                
                var image = await context.tbl_images
                    .Include(img => img.ScanDirectory)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(img => img.ImageId == imageId && img.IsDeleted == 0 && img.FileExists == 1);

                if (image?.ScanDirectory == null)
                {
                    return null;
                }

                // Build and validate the full path
                var normalizedRelativePath = image.RelativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                var fullPath = Path.Combine(image.ScanDirectory.DirectoryPath, normalizedRelativePath);
                
                // Ensure the path is within the scan directory (prevent path traversal)
                var resolvedPath = Path.GetFullPath(fullPath);
                var scanDirPath = Path.GetFullPath(image.ScanDirectory.DirectoryPath);
                
                if (!resolvedPath.StartsWith(scanDirPath, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Warning($"Potential path traversal attempt for image {imageId}: {fullPath}");
                    return null;
                }

                if (!File.Exists(resolvedPath))
                {
                    return null;
                }

                return resolvedPath;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting full path for image {imageId}: {ex.Message}");
                return null;
            }
        }

        public async Task<string?> GetThumbnailDataUriFromPathAsync(string filePath, int maxSize = 200)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return null;
                }

                // Validate the path is within a scan directory
                using var context = await _contextFactory.CreateDbContextAsync();
                var scanDirs = await context.tbl_scan_directory.ToListAsync();
                
                var resolvedPath = Path.GetFullPath(filePath);
                var isInScanDir = scanDirs.Any(dir => 
                    resolvedPath.StartsWith(Path.GetFullPath(dir.DirectoryPath), StringComparison.OrdinalIgnoreCase));
                
                if (!isInScanDir)
                {
                    Logger.Warning($"Attempted to access file outside scan directories: {filePath}");
                    return null;
                }

                // Use the unified thumbnail cache service for all image types
                byte[]? thumbnailBytes = await _thumbnailCacheService.GetCachedThumbnailAsync(filePath, maxSize);

                if (thumbnailBytes == null || thumbnailBytes.Length == 0)
                {
                    return null;
                }

                // Convert to base64 data URI
                var base64 = Convert.ToBase64String(thumbnailBytes);
                return $"data:image/jpeg;base64,{base64}";
            }
            catch (Exception ex)
            {
                Logger.Error($"Error generating thumbnail data URI for path {filePath}: {ex.Message}");
                return null;
            }
        }
    }
}