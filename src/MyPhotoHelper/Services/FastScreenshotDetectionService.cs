using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyPhotoHelper.Data;
using MyPhotoHelper.Models;

namespace MyPhotoHelper.Services
{
    public interface IFastScreenshotDetectionService
    {
        Task DetectScreenshotsWithQueriesAsync(IProgress<PhaseProgress>? progress = null, CancellationToken cancellationToken = default);
        Task<ScreenshotQueryResults> GetScreenshotStatisticsAsync();
    }

    public class ScreenshotQueryResults
    {
        public int TotalImages { get; set; }
        public int FilenameScreenshots { get; set; }
        public int ResolutionScreenshots { get; set; }
        public int NoExifScreenshots { get; set; }
        public int TotalDetectedScreenshots { get; set; }
        public List<string> CommonScreenshotPatterns { get; set; } = new();
        public List<string> CommonResolutions { get; set; } = new();
    }

    public class FastScreenshotDetectionService : IFastScreenshotDetectionService
    {
        private readonly ILogger<FastScreenshotDetectionService> _logger;
        private readonly IServiceProvider _serviceProvider;

        // Common screenshot filename patterns
        private readonly string[] _screenshotPatterns = {
            "screenshot", "screen shot", "capture", "snip", "clipboardimage", 
            "shot", "grab", "screen", "print screen", "prtsc"
        };

        // Common screenshot resolutions (width x height)
        private readonly (int width, int height)[] _screenshotResolutions = {
            (1920, 1080), (1366, 768), (1536, 864), (2560, 1440), (3840, 2160),
            (1440, 900), (1680, 1050), (1280, 720), (1280, 800), (1024, 768),
            (390, 844), (393, 852), (430, 932), (414, 896), (375, 812),
            (375, 667), (360, 800), (412, 915), (768, 1024), (834, 1194)
        };

        public FastScreenshotDetectionService(
            ILogger<FastScreenshotDetectionService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task DetectScreenshotsWithQueriesAsync(IProgress<PhaseProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MyPhotoHelperDbContext>();

            var phaseProgress = new PhaseProgress
            {
                Phase = ScanPhase.Phase3_ScreenshotDetection,
                StartTime = DateTime.UtcNow,
                CurrentItem = "Analyzing screenshot patterns..."
            };

            _logger.LogInformation("Starting fast screenshot detection using database queries");

            try
            {
                // Step 1: Count total images to process
                phaseProgress.CurrentItem = "Counting images to analyze...";
                progress?.Report(phaseProgress);

                var totalImages = await dbContext.tbl_images
                    .Where(img => img.FileExists == 1 && img.IsDeleted == 0)
                    .CountAsync(cancellationToken);

                phaseProgress.TotalItems = totalImages;
                _logger.LogInformation($"Found {totalImages} images to analyze for screenshot detection");
                
                if (totalImages == 0)
                {
                    _logger.LogWarning("No images found to process - screenshot detection will be skipped");
                    phaseProgress.ProcessedItems = 0;
                    phaseProgress.EndTime = DateTime.UtcNow;
                    phaseProgress.CurrentItem = "No images to process";
                    progress?.Report(phaseProgress);
                    return;
                }

                // Step 2: Detect screenshots by filename patterns (VERY fast)
                phaseProgress.CurrentItem = "Detecting screenshots by filename...";
                phaseProgress.ProcessedItems = totalImages / 4;
                progress?.Report(phaseProgress);

                var filenameScreenshots = 0;
                foreach (var pattern in _screenshotPatterns)
                {
                    try
                    {
                        var sql = $@"
                            INSERT OR IGNORE INTO tbl_image_analysis (ImageId, ImageCategory, PhotoSubcategory, AIAnalyzedAt, AIModelUsed, AIAnalysisJson)
                            SELECT i.ImageId, 'screenshot', 'filename_pattern', datetime('now'), 'screenshot_detector', json_object('confidence', 0.95, 'method', 'filename_pattern', 'pattern', '{pattern}')
                            FROM tbl_images i
                            WHERE i.FileExists = 1 AND i.IsDeleted = 0
                            AND LOWER(i.FileName) LIKE '%{pattern.ToLower()}%'
                            AND NOT EXISTS (
                                SELECT 1 FROM tbl_image_analysis a 
                                WHERE a.ImageId = i.ImageId AND a.ImageCategory IS NOT NULL
                            )";

                        var count = await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
                        filenameScreenshots += count;
                        _logger.LogDebug($"Pattern '{pattern}' matched {count} images");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error processing filename pattern '{pattern}': {ex.Message}");
                        phaseProgress.AddError($"Pattern '{pattern}' failed: {ex.Message}");
                        progress?.Report(phaseProgress);
                    }
                }

                _logger.LogInformation($"Found {filenameScreenshots} screenshots by filename patterns");
                _logger.LogInformation($"Step 2 completed - filename detection finished");

                // Step 3: Detect screenshots by common resolutions (fast)
                _logger.LogInformation("Starting Step 3: Resolution-based detection");
                phaseProgress.CurrentItem = "Detecting screenshots by resolution...";
                phaseProgress.ProcessedItems = totalImages / 2;
                progress?.Report(phaseProgress);

                var resolutionScreenshots = 0;
                foreach (var (width, height) in _screenshotResolutions)
                {
                    try
                    {
                        var sql = $@"
                            INSERT OR IGNORE INTO tbl_image_analysis (ImageId, ImageCategory, PhotoSubcategory, AIAnalyzedAt, AIModelUsed, AIAnalysisJson)
                            SELECT i.ImageId, 'screenshot', 'resolution_match', datetime('now'), 'screenshot_detector', json_object('confidence', 0.80, 'method', 'resolution_match', 'width', {width}, 'height', {height})
                            FROM tbl_images i
                            INNER JOIN tbl_image_metadata m ON i.ImageId = m.ImageId
                            WHERE i.FileExists = 1 AND i.IsDeleted = 0
                            AND m.Width = {width} AND m.Height = {height}
                            AND NOT EXISTS (
                                SELECT 1 FROM tbl_image_analysis a 
                                WHERE a.ImageId = i.ImageId AND a.ImageCategory IS NOT NULL
                            )";

                        var count = await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
                        resolutionScreenshots += count;
                        _logger.LogDebug($"Resolution {width}x{height} matched {count} images");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error processing resolution {width}x{height}: {ex.Message}");
                        phaseProgress.AddError($"Resolution {width}x{height} failed: {ex.Message}");
                        progress?.Report(phaseProgress);
                    }
                }

                _logger.LogInformation($"Found {resolutionScreenshots} additional screenshots by resolution");
                _logger.LogInformation($"Step 3 completed - resolution detection finished");

                // Step 4: Detect screenshots by missing camera info (medium confidence)
                _logger.LogInformation("Starting Step 4: No-camera-data detection");
                phaseProgress.CurrentItem = "Detecting screenshots by missing camera data...";
                phaseProgress.ProcessedItems = (totalImages * 3) / 4;
                progress?.Report(phaseProgress);

                var noExifScreenshots = 0;
                try
                {
                    var noExifSql = @"
                        INSERT OR IGNORE INTO tbl_image_analysis (ImageId, ImageCategory, PhotoSubcategory, AIAnalyzedAt, AIModelUsed, AIAnalysisJson)
                        SELECT i.ImageId, 'screenshot', 'no_camera_data', datetime('now'), 'screenshot_detector', json_object('confidence', 0.60, 'method', 'no_camera_data', 'pixel_count', m.Width * m.Height)
                        FROM tbl_images i
                        INNER JOIN tbl_image_metadata m ON i.ImageId = m.ImageId
                        WHERE i.FileExists = 1 AND i.IsDeleted = 0
                        AND (m.CameraMake IS NULL OR m.CameraMake = '')
                        AND (m.CameraModel IS NULL OR m.CameraModel = '')
                        AND (m.Width * m.Height) > 100000  -- Reasonable size
                        AND NOT EXISTS (
                            SELECT 1 FROM tbl_image_analysis a 
                            WHERE a.ImageId = i.ImageId AND a.ImageCategory IS NOT NULL
                        )";

                    noExifScreenshots = await dbContext.Database.ExecuteSqlRawAsync(noExifSql, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing no-camera-data detection: {ex.Message}");
                    phaseProgress.AddError($"No-camera-data detection failed: {ex.Message}");
                    progress?.Report(phaseProgress);
                }
                _logger.LogInformation($"Found {noExifScreenshots} additional screenshots by missing camera data");
                _logger.LogInformation($"Step 4 completed - no-camera-data detection finished");

                // Step 5: Complete detection (no need to mark non-screenshots)
                _logger.LogInformation("Step 5: Screenshot detection completed - only detected screenshots have analysis records");
                phaseProgress.CurrentItem = "Screenshot detection completed";
                phaseProgress.ProcessedItems = totalImages;
                progress?.Report(phaseProgress);

                var totalDetected = filenameScreenshots + resolutionScreenshots + noExifScreenshots;
                
                _logger.LogInformation($"Screenshot detection summary:");
                _logger.LogInformation($"  - By filename patterns: {filenameScreenshots}");
                _logger.LogInformation($"  - By resolution match: {resolutionScreenshots}");
                _logger.LogInformation($"  - By missing camera data: {noExifScreenshots}");
                _logger.LogInformation($"  - Total screenshots detected: {totalDetected}");
                _logger.LogInformation($"  - Total images scanned: {totalImages}");
                _logger.LogInformation($"  - Regular photos: {totalImages - totalDetected} (no analysis record created)");
                
                phaseProgress.SuccessCount = totalDetected;
                phaseProgress.EndTime = DateTime.UtcNow;
                phaseProgress.CurrentItem = "Screenshot detection completed";
                progress?.Report(phaseProgress);

                _logger.LogInformation($"Fast screenshot detection completed successfully in {(phaseProgress.EndTime - phaseProgress.StartTime)?.TotalSeconds:F1} seconds");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during fast screenshot detection");
                phaseProgress.ErrorCount = 1;
                phaseProgress.EndTime = DateTime.UtcNow;
                phaseProgress.CurrentItem = "Screenshot detection failed";
                progress?.Report(phaseProgress);
                throw;
            }
        }

        public async Task<ScreenshotQueryResults> GetScreenshotStatisticsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MyPhotoHelperDbContext>();

            var results = new ScreenshotQueryResults();

            // Total images
            results.TotalImages = await dbContext.tbl_images
                .Where(img => img.FileExists == 1 && img.IsDeleted == 0)
                .CountAsync();

            // Screenshots by filename
            results.FilenameScreenshots = await dbContext.tbl_image_analysis
                .Where(a => a.ImageCategory == "screenshot" && a.PhotoSubcategory == "filename_pattern")
                .CountAsync();

            // Screenshots by resolution
            results.ResolutionScreenshots = await dbContext.tbl_image_analysis
                .Where(a => a.ImageCategory == "screenshot" && a.PhotoSubcategory == "resolution_match")
                .CountAsync();

            // Screenshots by missing EXIF
            results.NoExifScreenshots = await dbContext.tbl_image_analysis
                .Where(a => a.ImageCategory == "screenshot" && a.PhotoSubcategory == "no_camera_data")
                .CountAsync();

            // Total detected screenshots
            results.TotalDetectedScreenshots = await dbContext.tbl_image_analysis
                .Where(a => a.ImageCategory == "screenshot")
                .CountAsync();

            // Common patterns found (samples)
            results.CommonScreenshotPatterns = await dbContext.tbl_images
                .Where(img => img.FileExists == 1 && img.IsDeleted == 0)
                .Join(dbContext.tbl_image_analysis, 
                    img => img.ImageId, 
                    analysis => analysis.ImageId,
                    (img, analysis) => new { img.FileName, analysis.ImageCategory })
                .Where(x => x.ImageCategory == "screenshot")
                .Select(x => x.FileName)
                .Take(10)
                .ToListAsync();

            // Common resolutions found
            results.CommonResolutions = await dbContext.tbl_images
                .Where(img => img.FileExists == 1 && img.IsDeleted == 0)
                .Join(dbContext.tbl_image_metadata,
                    img => img.ImageId,
                    meta => meta.ImageId,
                    (img, meta) => new { img.ImageId, meta.Width, meta.Height })
                .Join(dbContext.tbl_image_analysis,
                    x => x.ImageId,
                    analysis => analysis.ImageId, 
                    (x, analysis) => new { x.Width, x.Height, analysis.ImageCategory })
                .Where(x => x.ImageCategory == "screenshot" && x.Width.HasValue && x.Height.HasValue)
                .Select(x => $"{x.Width}x{x.Height}")
                .Distinct()
                .Take(10)
                .ToListAsync();

            return results;
        }
    }
}