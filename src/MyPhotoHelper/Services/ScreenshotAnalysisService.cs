using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyPhotoHelper.Data;
using MyPhotoHelper.Models;

namespace MyPhotoHelper.Services
{
    public interface IScreenshotAnalysisService
    {
        Task<List<ScreenshotPattern>> AnalyzeExistingPatternsAsync();
        Task<List<ResolutionStats>> GetResolutionStatsAsync();
        Task<CameraDataStats> GetCameraDataStatsAsync();
        Task<ScreenshotStatistics> GetScreenshotStatisticsAsync();
    }

    public class ScreenshotPattern
    {
        public string Pattern { get; set; } = string.Empty;
        public int Count { get; set; }
        public double Percentage { get; set; }
        public List<string> Examples { get; set; } = new();
    }

    public class ResolutionStats
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int Count { get; set; }
        public double Percentage { get; set; }
        public bool LikelyScreenshot { get; set; }
    }

    public class CameraDataStats
    {
        public int TotalImages { get; set; }
        public int WithCameraMake { get; set; }
        public int WithCameraModel { get; set; }
        public int WithExifData { get; set; }
        public int LikelyScreenshots { get; set; }
        public List<string> CommonCameraMakes { get; set; } = new();
    }

    public class ScreenshotStatistics
    {
        public int TotalImages { get; set; }
        public int DetectedScreenshots { get; set; }
        public int RegularPhotos { get; set; }
        public double ScreenshotPercentage { get; set; }
        public List<tbl_images> SampleScreenshots { get; set; } = new();
    }

    public class ScreenshotAnalysisService : IScreenshotAnalysisService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ScreenshotAnalysisService> _logger;

        public ScreenshotAnalysisService(IServiceProvider serviceProvider, ILogger<ScreenshotAnalysisService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task<List<ScreenshotPattern>> AnalyzeExistingPatternsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MyPhotoHelperDbContext>();

            var patterns = new List<ScreenshotPattern>();

            // Common screenshot patterns to check
            var patternsToCheck = new[]
            {
                "screenshot", "screen shot", "capture", "snip", "shot", 
                "grab", "clipboardimage", "image", "pic", "photo"
            };

            var totalImages = await dbContext.tbl_images
                .Where(img => img.FileExists == 1 && img.IsDeleted == 0)
                .CountAsync();

            foreach (var pattern in patternsToCheck)
            {
                var count = await dbContext.tbl_images
                    .Where(img => img.FileExists == 1 && img.IsDeleted == 0)
                    .Where(img => img.FileName.ToLower().Contains(pattern.ToLower()))
                    .CountAsync();

                if (count > 0)
                {
                    var examples = await dbContext.tbl_images
                        .Where(img => img.FileExists == 1 && img.IsDeleted == 0)
                        .Where(img => img.FileName.ToLower().Contains(pattern.ToLower()))
                        .Select(img => img.FileName)
                        .Take(5)
                        .ToListAsync();

                    patterns.Add(new ScreenshotPattern
                    {
                        Pattern = pattern,
                        Count = count,
                        Percentage = totalImages > 0 ? (count * 100.0 / totalImages) : 0,
                        Examples = examples
                    });
                }
            }

            return patterns.OrderByDescending(p => p.Count).ToList();
        }

        public async Task<List<ResolutionStats>> GetResolutionStatsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MyPhotoHelperDbContext>();

            // Common screenshot resolutions
            var screenshotResolutions = new HashSet<(int, int)>
            {
                (1920, 1080), (1366, 768), (1536, 864), (2560, 1440), (3840, 2160),
                (1440, 900), (1680, 1050), (1280, 720), (1280, 800), (1024, 768),
                (390, 844), (393, 852), (430, 932), (414, 896), (375, 812),
                (375, 667), (360, 800), (412, 915), (768, 1024), (834, 1194)
            };

            var resolutionStats = await dbContext.tbl_image_metadata
                .Where(m => m.Width.HasValue && m.Height.HasValue)
                .GroupBy(m => new { m.Width, m.Height })
                .Select(g => new ResolutionStats
                {
                    Width = g.Key.Width!.Value,
                    Height = g.Key.Height!.Value,
                    Count = g.Count()
                })
                .OrderByDescending(r => r.Count)
                .Take(20)
                .ToListAsync();

            var totalImages = resolutionStats.Sum(r => r.Count);
            
            foreach (var stat in resolutionStats)
            {
                stat.Percentage = totalImages > 0 ? (stat.Count * 100.0 / totalImages) : 0;
                stat.LikelyScreenshot = screenshotResolutions.Contains((stat.Width, stat.Height));
            }

            return resolutionStats;
        }

        public async Task<CameraDataStats> GetCameraDataStatsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MyPhotoHelperDbContext>();

            var stats = new CameraDataStats();

            stats.TotalImages = await dbContext.tbl_image_metadata.CountAsync();

            stats.WithCameraMake = await dbContext.tbl_image_metadata
                .Where(m => !string.IsNullOrEmpty(m.CameraMake))
                .CountAsync();

            stats.WithCameraModel = await dbContext.tbl_image_metadata
                .Where(m => !string.IsNullOrEmpty(m.CameraModel))
                .CountAsync();

            // Images with any EXIF camera data
            stats.WithExifData = await dbContext.tbl_image_metadata
                .Where(m => !string.IsNullOrEmpty(m.CameraMake) || 
                           !string.IsNullOrEmpty(m.CameraModel) ||
                           !string.IsNullOrEmpty(m.LensModel) ||
                           m.FocalLength.HasValue ||
                           m.ISO.HasValue)
                .CountAsync();

            // Likely screenshots (no camera data but reasonable size)
            stats.LikelyScreenshots = await dbContext.tbl_image_metadata
                .Where(m => string.IsNullOrEmpty(m.CameraMake) && 
                           string.IsNullOrEmpty(m.CameraModel) &&
                           m.Width.HasValue && m.Height.HasValue &&
                           (m.Width * m.Height) > 100000)
                .CountAsync();

            // Most common camera makes
            stats.CommonCameraMakes = await dbContext.tbl_image_metadata
                .Where(m => !string.IsNullOrEmpty(m.CameraMake))
                .GroupBy(m => m.CameraMake)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => $"{g.Key} ({g.Count()})")
                .ToListAsync();

            return stats;
        }

        public async Task<ScreenshotStatistics> GetScreenshotStatisticsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MyPhotoHelperDbContext>();

            var stats = new ScreenshotStatistics();

            // Get total images
            stats.TotalImages = await dbContext.tbl_images
                .Where(img => img.FileExists == 1 && img.IsDeleted == 0)
                .CountAsync();

            // Get detected screenshots from analysis table
            stats.DetectedScreenshots = await dbContext.tbl_image_analysis
                .Where(a => a.ImageCategory == "screenshot")
                .CountAsync();

            stats.RegularPhotos = stats.TotalImages - stats.DetectedScreenshots;
            stats.ScreenshotPercentage = stats.TotalImages > 0 ? 
                (stats.DetectedScreenshots * 100.0 / stats.TotalImages) : 0;

            // Get sample screenshots
            stats.SampleScreenshots = await dbContext.tbl_images
                .Where(img => img.FileExists == 1 && img.IsDeleted == 0)
                .Join(dbContext.tbl_image_analysis,
                      img => img.ImageId,
                      analysis => analysis.ImageId,
                      (img, analysis) => new { img, analysis })
                .Where(x => x.analysis.ImageCategory == "screenshot")
                .OrderByDescending(x => x.analysis.AIAnalyzedAt)
                .Take(6)
                .Select(x => x.img)
                .Include(img => img.tbl_image_metadata)
                .Include(img => img.ScanDirectory)
                .ToListAsync();

            return stats;
        }
    }
}