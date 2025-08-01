using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyPhotoHelper.Data;
using MyPhotoHelper.Models;

namespace MyPhotoHelper.Services
{
    public interface IFastImageCategorizationService
    {
        Task CategorizeImagesAsync(IProgress<PhaseProgress>? progress = null, CancellationToken cancellationToken = default);
        Task<ImageCategorizationResults> GetCategorizationStatisticsAsync();
    }

    public class ImageCategorizationResults
    {
        public int TotalImages { get; set; }
        public int CategorizedImages { get; set; }
        public int UncategorizedImages { get; set; }
        
        // Screenshot stats
        public int FilenameScreenshots { get; set; }
        public int ResolutionScreenshots { get; set; }
        public int TotalScreenshots { get; set; }
        
        // Photo stats
        public int PhotosWithCamera { get; set; }
        public int TotalPhotos { get; set; }
        
        public List<string> CommonScreenshotPatterns { get; set; } = new();
        public List<string> CommonResolutions { get; set; } = new();
        public List<string> CommonCameraMakes { get; set; } = new();
    }

    public class FastImageCategorizationService : IFastImageCategorizationService
    {
        private readonly ILogger<FastImageCategorizationService> _logger;
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

        public FastImageCategorizationService(
            ILogger<FastImageCategorizationService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task CategorizeImagesAsync(IProgress<PhaseProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MyPhotoHelperDbContext>();

            var phaseProgress = new PhaseProgress
            {
                Phase = ScanPhase.Phase3_ScreenshotDetection,
                StartTime = DateTime.UtcNow,
                CurrentItem = "Analyzing image categories..."
            };

            _logger.LogInformation("Starting fast image categorization using database queries");

            try
            {
                // Step 1: Count total images to process
                phaseProgress.CurrentItem = "Counting images to analyze...";
                progress?.Report(phaseProgress);

                var totalImages = await dbContext.tbl_images
                    .Where(img => img.FileExists == 1 && img.IsDeleted == 0)
                    .CountAsync(cancellationToken);

                phaseProgress.TotalItems = totalImages;
                _logger.LogInformation($"Found {totalImages} images to analyze for categorization");
                
                if (totalImages == 0)
                {
                    _logger.LogWarning("No images found to process - categorization will be skipped");
                    phaseProgress.ProcessedItems = 0;
                    phaseProgress.EndTime = DateTime.UtcNow;
                    phaseProgress.CurrentItem = "No images to process";
                    progress?.Report(phaseProgress);
                    return;
                }

                // Step 2: Detect screenshots by filename patterns (VERY fast)
                phaseProgress.CurrentItem = "Detecting screenshots by filename...";
                phaseProgress.ProcessedItems = totalImages / 5;
                progress?.Report(phaseProgress);

                var filenameScreenshots = 0;
                foreach (var pattern in _screenshotPatterns)
                {
                    try
                    {
                        var sql = $@"
                            INSERT OR IGNORE INTO tbl_image_analysis (ImageId, ImageCategory, PhotoSubcategory, AIAnalyzedAt, AIModelUsed, AIAnalysisJson, AIDescription)
                            SELECT i.ImageId, 'screenshot', 'filename_pattern', datetime('now'), 'fast_categorizer', 
                                   json_object('confidence', 0.95, 'method', 'filename_pattern', 'pattern', '{pattern}'),
                                   'Detected as screenshot: filename contains ''{pattern}'''
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

                // Step 3: Detect screenshots by common resolutions (fast)
                phaseProgress.CurrentItem = "Detecting screenshots by resolution...";
                phaseProgress.ProcessedItems = (totalImages * 2) / 5;
                progress?.Report(phaseProgress);

                var resolutionScreenshots = 0;
                foreach (var (width, height) in _screenshotResolutions)
                {
                    try
                    {
                        var sql = $@"
                            INSERT OR IGNORE INTO tbl_image_analysis (ImageId, ImageCategory, PhotoSubcategory, AIAnalyzedAt, AIModelUsed, AIAnalysisJson, AIDescription)
                            SELECT i.ImageId, 'screenshot', 'resolution_match', datetime('now'), 'fast_categorizer', 
                                   json_object('confidence', 0.80, 'method', 'resolution_match', 'width', {width}, 'height', {height}),
                                   'Detected as screenshot: resolution {width}×{height} matches common screen size'
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

                // Step 4: Detect photos by camera information (high confidence)
                phaseProgress.CurrentItem = "Detecting photos with camera information...";
                phaseProgress.ProcessedItems = (totalImages * 3) / 5;
                progress?.Report(phaseProgress);

                var photosWithCamera = 0;
                try
                {
                    var photoSql = @"
                        INSERT OR IGNORE INTO tbl_image_analysis (ImageId, ImageCategory, PhotoSubcategory, AIAnalyzedAt, AIModelUsed, AIAnalysisJson, AIDescription)
                        SELECT i.ImageId, 'photo', 'has_camera_info', datetime('now'), 'fast_categorizer', 
                               json_object('confidence', 0.95, 'method', 'camera_detection', 'camera_make', m.CameraMake, 'camera_model', m.CameraModel),
                               'Detected as photo: contains camera metadata (' || m.CameraMake || ' ' || m.CameraModel || ')'
                        FROM tbl_images i
                        INNER JOIN tbl_image_metadata m ON i.ImageId = m.ImageId
                        WHERE i.FileExists = 1 AND i.IsDeleted = 0
                        AND m.CameraMake IS NOT NULL AND m.CameraMake != ''
                        AND m.CameraModel IS NOT NULL AND m.CameraModel != ''
                        AND NOT EXISTS (
                            SELECT 1 FROM tbl_image_analysis a 
                            WHERE a.ImageId = i.ImageId AND a.ImageCategory IS NOT NULL
                        )";

                    photosWithCamera = await dbContext.Database.ExecuteSqlRawAsync(photoSql, cancellationToken);
                    _logger.LogInformation($"Found {photosWithCamera} photos with camera information");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing camera detection: {ex.Message}");
                    phaseProgress.AddError($"Camera detection failed: {ex.Message}");
                    progress?.Report(phaseProgress);
                }

                // Complete categorization
                _logger.LogInformation("Image categorization completed");
                phaseProgress.CurrentItem = "Image categorization completed";
                phaseProgress.ProcessedItems = totalImages;
                progress?.Report(phaseProgress);

                var totalCategorized = filenameScreenshots + resolutionScreenshots + photosWithCamera;
                
                _logger.LogInformation($"Image categorization summary:");
                _logger.LogInformation($"  - Screenshots by filename: {filenameScreenshots}");
                _logger.LogInformation($"  - Screenshots by resolution: {resolutionScreenshots}");
                _logger.LogInformation($"  - Photos with camera info: {photosWithCamera}");
                _logger.LogInformation($"  - Total categorized: {totalCategorized}");
                _logger.LogInformation($"  - Total images scanned: {totalImages}");
                _logger.LogInformation($"  - Uncategorized images: {totalImages - totalCategorized} (no analysis record created)");
                
                phaseProgress.SuccessCount = totalCategorized;
                phaseProgress.EndTime = DateTime.UtcNow;
                phaseProgress.CurrentItem = "Image categorization completed";
                progress?.Report(phaseProgress);

                _logger.LogInformation($"Fast image categorization completed successfully in {(phaseProgress.EndTime - phaseProgress.StartTime)?.TotalSeconds:F1} seconds");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during fast image categorization");
                phaseProgress.ErrorCount = 1;
                phaseProgress.EndTime = DateTime.UtcNow;
                phaseProgress.CurrentItem = "Image categorization failed";
                progress?.Report(phaseProgress);
                throw;
            }
        }

        public async Task<ImageCategorizationResults> GetCategorizationStatisticsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MyPhotoHelperDbContext>();

            var results = new ImageCategorizationResults();

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

            // Total screenshots
            results.TotalScreenshots = await dbContext.tbl_image_analysis
                .Where(a => a.ImageCategory == "screenshot")
                .CountAsync();

            // Photos with camera info
            results.PhotosWithCamera = await dbContext.tbl_image_analysis
                .Where(a => a.ImageCategory == "photo" && a.PhotoSubcategory == "has_camera_info")
                .CountAsync();

            // Total photos
            results.TotalPhotos = await dbContext.tbl_image_analysis
                .Where(a => a.ImageCategory == "photo")
                .CountAsync();

            // Total categorized
            results.CategorizedImages = await dbContext.tbl_image_analysis
                .Where(a => a.ImageCategory != null)
                .CountAsync();

            results.UncategorizedImages = results.TotalImages - results.CategorizedImages;

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

            // Common camera makes
            results.CommonCameraMakes = await dbContext.tbl_image_metadata
                .Where(m => m.CameraMake != null && m.CameraMake != "")
                .Join(dbContext.tbl_image_analysis,
                    m => m.ImageId,
                    a => a.ImageId,
                    (m, a) => new { m.CameraMake, a.ImageCategory })
                .Where(x => x.ImageCategory == "photo")
                .GroupBy(x => x.CameraMake)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => $"{g.Key} ({g.Count()})")
                .ToListAsync();

            return results;
        }
    }
}