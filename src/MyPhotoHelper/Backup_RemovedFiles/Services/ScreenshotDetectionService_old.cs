using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyPhotoHelper.Data;
using MyPhotoHelper.Models;
using CSnakes.Runtime;

namespace MyPhotoHelper.Services
{
    public interface IScreenshotDetectionService
    {
        Task DetectScreenshotsForNewImagesAsync(CancellationToken cancellationToken = default);
        Task DetectScreenshotsForNewImagesAsync(IProgress<PhaseProgress>? progress, CancellationToken cancellationToken = default);
        Task<ScreenshotDetectionResult?> DetectScreenshotAsync(tbl_images image, CancellationToken cancellationToken = default);
        Task RescanAllScreenshotsAsync(IProgress<PhaseProgress>? progress = null, CancellationToken cancellationToken = default);
    }

    public class ScreenshotDetectionResult
    {
        public bool IsScreenshot { get; set; }
        public decimal Confidence { get; set; }
        public string Method { get; set; } = string.Empty;
        public string DetailsJson { get; set; } = string.Empty;
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    }

    public class ScreenshotDetectionService : IScreenshotDetectionService
    {
        private readonly ILogger<ScreenshotDetectionService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IPhotoPathService _photoPathService;
        private readonly IPythonEnvironment? _pythonEnv;

        public ScreenshotDetectionService(
            ILogger<ScreenshotDetectionService> logger,
            IServiceProvider serviceProvider,
            IPhotoPathService photoPathService)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _photoPathService = photoPathService;
            
            // Try to get Python environment if available
            try
            {
                _pythonEnv = serviceProvider.GetService<IPythonEnvironment>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Python environment not available: {ex.Message}");
            }
        }

        public async Task DetectScreenshotsForNewImagesAsync(CancellationToken cancellationToken = default)
        {
            await DetectScreenshotsForNewImagesAsync(null, cancellationToken);
        }

        public async Task DetectScreenshotsForNewImagesAsync(IProgress<PhaseProgress>? progress, CancellationToken cancellationToken = default)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MyPhotoHelperDbContext>();

            // Get total count of non-deleted images
            var totalImages = await dbContext.tbl_images
                .Where(img => img.FileExists == 1 && img.IsDeleted == 0)
                .CountAsync(cancellationToken);

            // Get initial count of images that already have screenshot analysis
            var initialImagesWithScreenshotAnalysis = await dbContext.tbl_images
                .Where(img => img.FileExists == 1 && img.IsDeleted == 0)
                .Where(img => dbContext.tbl_image_analysis.Any(analysis => analysis.ImageId == img.ImageId && analysis.IsScreenshot != null))
                .CountAsync(cancellationToken);

            _logger.LogInformation($"Screenshot detection starting: {initialImagesWithScreenshotAnalysis}/{totalImages} already analyzed");

            var phaseProgress = new PhaseProgress
            {
                Phase = ScanPhase.Phase3_ScreenshotDetection,
                TotalItems = totalImages,
                ProcessedItems = initialImagesWithScreenshotAnalysis,
                SuccessCount = 0,
                ErrorCount = 0,
                StartTime = DateTime.UtcNow,
                CurrentItem = $"Starting screenshot detection on {totalImages} images..."
            };

            progress?.Report(phaseProgress);
            
            // Add a small delay to ensure UI can see the initial state
            await Task.Delay(100, cancellationToken);

            var totalProcessedCount = 0;
            var totalErrorCount = 0;
            var batchSize = 1000;
            var hasMoreImages = true;
            
            // Throttle progress reporting to improve performance
            var lastProgressReport = DateTime.UtcNow;
            var progressReportInterval = TimeSpan.FromMilliseconds(500);
            var progressUpdateCounter = 0;

            // Process all images in batches
            while (hasMoreImages && !cancellationToken.IsCancellationRequested)
            {
                // Get next batch of images without screenshot analysis
                var imagesWithoutScreenshotAnalysis = await dbContext.tbl_images
                    .Where(img => img.FileExists == 1 && img.IsDeleted == 0)
                    .Where(img => !dbContext.tbl_image_analysis.Any(analysis => analysis.ImageId == img.ImageId && analysis.IsScreenshot != null))
                    .Take(batchSize)
                    .ToListAsync(cancellationToken);

                if (!imagesWithoutScreenshotAnalysis.Any())
                {
                    hasMoreImages = false;
                    break;
                }

                _logger.LogDebug($"Processing batch of {imagesWithoutScreenshotAnalysis.Count} images for screenshot detection");

                var batchProcessedCount = 0;
                var batchErrorCount = 0;

                foreach (var image in imagesWithoutScreenshotAnalysis)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        var result = await DetectScreenshotAsync(image, cancellationToken);
                        if (result != null)
                        {
                            // Create or update analysis record
                            var existingAnalysis = await dbContext.tbl_image_analysis
                                .FirstOrDefaultAsync(a => a.ImageId == image.ImageId, cancellationToken);

                            if (existingAnalysis == null)
                            {
                                existingAnalysis = new tbl_image_analysis
                                {
                                    ImageId = image.ImageId
                                };
                                dbContext.tbl_image_analysis.Add(existingAnalysis);
                            }

                            // Update screenshot fields
                            existingAnalysis.IsScreenshot = result.IsScreenshot;
                            existingAnalysis.ScreenshotConfidence = result.Confidence;
                            existingAnalysis.ScreenshotDetectedAt = result.DetectedAt;
                            existingAnalysis.ScreenshotMethod = result.Method;

                            await dbContext.SaveChangesAsync(cancellationToken);
                            batchProcessedCount++;
                        }
                        else
                        {
                            batchErrorCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error processing image {image.ImageId} for screenshot detection");
                        batchErrorCount++;
                    }

                    totalProcessedCount++;
                    progressUpdateCounter++;

                    // Update progress periodically
                    var now = DateTime.UtcNow;
                    if (now - lastProgressReport >= progressReportInterval || progressUpdateCounter >= 100)
                    {
                        phaseProgress.ProcessedItems = initialImagesWithScreenshotAnalysis + totalProcessedCount;
                        phaseProgress.SuccessCount = totalProcessedCount - (totalErrorCount + batchErrorCount);
                        phaseProgress.ErrorCount = totalErrorCount + batchErrorCount;
                        phaseProgress.CurrentItem = $"Analyzing {image.FileName} for screenshots...";

                        progress?.Report(phaseProgress);
                        lastProgressReport = now;
                        progressUpdateCounter = 0;
                        
                        _logger.LogDebug($"Screenshot detection progress: {phaseProgress.ProcessedItems}/{phaseProgress.TotalItems} ({phaseProgress.ProgressPercentage:F1}%)");
                    }
                }

                totalErrorCount += batchErrorCount;
                
                _logger.LogDebug($"Batch completed: {batchProcessedCount} processed, {batchErrorCount} errors");
            }

            // Final progress update
            phaseProgress.ProcessedItems = totalImages;
            phaseProgress.SuccessCount = totalProcessedCount - totalErrorCount;
            phaseProgress.ErrorCount = totalErrorCount;
            phaseProgress.EndTime = DateTime.UtcNow;
            phaseProgress.CurrentItem = "Screenshot detection completed";

            progress?.Report(phaseProgress);

            _logger.LogInformation($"Screenshot detection completed: {totalProcessedCount - totalErrorCount} successful, {totalErrorCount} errors");
        }

        public async Task<ScreenshotDetectionResult?> DetectScreenshotAsync(tbl_images image, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_pythonEnv == null)
                {
                    _logger.LogWarning("Python environment not available for screenshot detection");
                    return null;
                }

                // Get full path to image
                var fullPath = await _photoPathService.GetFullPathForImageAsync(image);
                
                if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
                {
                    _logger.LogWarning($"Image file not found: {fullPath}");
                    return null;
                }

                // Call Python screenshot detection function  
                var result = await Task.Run(() =>
                {
                    // Temporary fallback implementation until Python integration is fixed
                    var fileName = Path.GetFileName(fullPath).ToLower();
                    var isScreenshot = fileName.Contains("screenshot") || fileName.Contains("screen shot");
                    var confidence = isScreenshot ? 0.95 : 0.1;
                    
                    return new { 
                        is_screenshot = isScreenshot, 
                        confidence = confidence, 
                        detection_method = "filename_fallback" 
                    };
                });
                
                // Handle result from screenshot detection
                if (result != null)
                {
                    // For our temporary fallback implementation
                    var isScreenshot = (bool)result.GetType().GetProperty("is_screenshot")?.GetValue(result)!;
                    var confidence = (double)result.GetType().GetProperty("confidence")?.GetValue(result)!;
                    var method = result.GetType().GetProperty("detection_method")?.GetValue(result)?.ToString() ?? "unknown";
                    
                    var detailsJson = System.Text.Json.JsonSerializer.Serialize(result);

                    return new ScreenshotDetectionResult
                    {
                        IsScreenshot = isScreenshot,
                        Confidence = (decimal)confidence,
                        Method = method,
                        DetailsJson = detailsJson,
                        DetectedAt = DateTime.UtcNow
                    };
                }
                else
                {
                    _logger.LogWarning($"No result from Python screenshot detection for {fullPath}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error detecting screenshot for image {image.ImageId}");
                return null;
            }
        }

        public async Task RescanAllScreenshotsAsync(IProgress<PhaseProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MyPhotoHelperDbContext>();

            _logger.LogInformation("Starting full screenshot rescan - clearing existing screenshot analysis");

            // Clear existing screenshot analysis
            await dbContext.tbl_image_analysis
                .Where(a => a.IsScreenshot != null)
                .ForEachAsync(a => {
                    a.IsScreenshot = null;
                    a.ScreenshotConfidence = null;
                    a.ScreenshotDetectedAt = null;
                    a.ScreenshotMethod = null;
                }, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            // Now run detection on all images
            await DetectScreenshotsForNewImagesAsync(progress, cancellationToken);
        }
    }
}