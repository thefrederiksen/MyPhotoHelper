using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyPhotoHelper.Data;
using MyPhotoHelper.Models;

namespace MyPhotoHelper.Services
{
    public interface IMetadataClassificationTestService
    {
        Task<MetadataClassificationTestResults> RunAccuracyTestAsync(string apiKey, int sampleSize = 50, string model = "gpt-4o-mini");
        Task<List<MetadataClassificationResult>> TestKnownScreenshotsAsync(string apiKey, int limit = 20, string model = "gpt-4o-mini");
        Task<List<MetadataClassificationResult>> TestKnownPhotosAsync(string apiKey, int limit = 20, string model = "gpt-4o-mini");
        Task<MetadataClassificationTestResults> TestSpecificImagesAsync(string apiKey, List<int> imageIds, string model = "gpt-4o-mini");
    }

    public class MetadataClassificationTestResults
    {
        public int TotalTested { get; set; }
        public int CorrectPredictions { get; set; }
        public int IncorrectPredictions { get; set; }
        public double AccuracyPercentage => TotalTested > 0 ? (double)CorrectPredictions / TotalTested * 100 : 0;
        
        public int TruePositiveScreenshots { get; set; }
        public int FalsePositiveScreenshots { get; set; }
        public int TrueNegativeScreenshots { get; set; }
        public int FalseNegativeScreenshots { get; set; }
        
        public int TruePositivePhotos { get; set; }
        public int FalsePositivePhotos { get; set; }
        public int TrueNegativePhotos { get; set; }
        public int FalseNegativePhotos { get; set; }
        
        public List<MetadataClassificationTestResult> DetailedResults { get; set; } = new();
        public string Summary { get; set; } = "";
        public DateTime TestDate { get; set; } = DateTime.UtcNow;
        public string Model { get; set; } = "";
        public TimeSpan Duration { get; set; }
    }

    public class MetadataClassificationTestResult : MetadataClassificationResult
    {
        public string ActualCategory { get; set; } = "";
        public bool IsCorrect => Category.Equals(ActualCategory, StringComparison.OrdinalIgnoreCase);
        public string TestNotes { get; set; } = "";
    }

    public class MetadataClassificationTestService : IMetadataClassificationTestService
    {
        private readonly ILogger<MetadataClassificationTestService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IMetadataClassificationService _classificationService;

        public MetadataClassificationTestService(
            ILogger<MetadataClassificationTestService> logger,
            IServiceProvider serviceProvider,
            IMetadataClassificationService classificationService)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _classificationService = classificationService;
        }

        public async Task<MetadataClassificationTestResults> RunAccuracyTestAsync(string apiKey, int sampleSize = 50, string model = "gpt-4o-mini")
        {
            var startTime = DateTime.UtcNow;
            _logger.LogInformation($"Starting metadata classification accuracy test with {sampleSize} samples");

            var results = new MetadataClassificationTestResults
            {
                Model = model,
                TestDate = startTime
            };

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<MyPhotoHelperDbContext>();

                // Get known screenshots
                var knownScreenshots = await GetKnownScreenshots(dbContext, sampleSize / 2);
                var knownPhotos = await GetKnownPhotos(dbContext, sampleSize / 2);

                _logger.LogInformation($"Found {knownScreenshots.Count} known screenshots and {knownPhotos.Count} known photos for testing");

                // Test screenshots
                foreach (var image in knownScreenshots)
                {
                    var classificationResult = await _classificationService.ClassifyImageAsync(image, apiKey, model);
                    
                    var testResult = new MetadataClassificationTestResult
                    {
                        ImageId = classificationResult.ImageId,
                        FileName = classificationResult.FileName,
                        Category = classificationResult.Category,
                        Confidence = classificationResult.Confidence,
                        Reasoning = classificationResult.Reasoning,
                        HasError = classificationResult.HasError,
                        ErrorMessage = classificationResult.ErrorMessage,
                        ActualCategory = "screenshot",
                        TestNotes = "Known screenshot from existing analysis"
                    };

                    results.DetailedResults.Add(testResult);
                    
                    if (!testResult.HasError)
                    {
                        if (testResult.IsCorrect)
                        {
                            results.CorrectPredictions++;
                            results.TruePositiveScreenshots++;
                        }
                        else
                        {
                            results.IncorrectPredictions++;
                            if (testResult.Category == "photo")
                                results.FalseNegativeScreenshots++;
                        }
                    }
                }

                // Test photos
                foreach (var image in knownPhotos)
                {
                    var classificationResult = await _classificationService.ClassifyImageAsync(image, apiKey, model);
                    
                    var testResult = new MetadataClassificationTestResult
                    {
                        ImageId = classificationResult.ImageId,
                        FileName = classificationResult.FileName,
                        Category = classificationResult.Category,
                        Confidence = classificationResult.Confidence,
                        Reasoning = classificationResult.Reasoning,
                        HasError = classificationResult.HasError,
                        ErrorMessage = classificationResult.ErrorMessage,
                        ActualCategory = "photo",
                        TestNotes = "Known photo from existing analysis"
                    };

                    results.DetailedResults.Add(testResult);
                    
                    if (!testResult.HasError)
                    {
                        if (testResult.IsCorrect)
                        {
                            results.CorrectPredictions++;
                            results.TruePositivePhotos++;
                        }
                        else
                        {
                            results.IncorrectPredictions++;
                            if (testResult.Category == "screenshot")
                                results.FalsePositiveScreenshots++;
                        }
                    }
                }

                results.TotalTested = results.CorrectPredictions + results.IncorrectPredictions;
                results.Duration = DateTime.UtcNow - startTime;
                
                // Calculate additional metrics
                results.TrueNegativeScreenshots = results.TruePositivePhotos;
                results.TrueNegativePhotos = results.TruePositiveScreenshots;
                
                // Generate summary
                results.Summary = GenerateTestSummary(results);
                
                _logger.LogInformation($"Test completed: {results.AccuracyPercentage:F1}% accuracy ({results.CorrectPredictions}/{results.TotalTested})");
                
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running accuracy test");
                results.Summary = $"Test failed: {ex.Message}";
                results.Duration = DateTime.UtcNow - startTime;
                return results;
            }
        }

        public async Task<List<MetadataClassificationResult>> TestKnownScreenshotsAsync(string apiKey, int limit = 20, string model = "gpt-4o-mini")
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MyPhotoHelperDbContext>();
            
            var knownScreenshots = await GetKnownScreenshots(dbContext, limit);
            var results = new List<MetadataClassificationResult>();
            
            foreach (var image in knownScreenshots)
            {
                var result = await _classificationService.ClassifyImageAsync(image, apiKey, model);
                results.Add(result);
            }
            
            return results;
        }

        public async Task<List<MetadataClassificationResult>> TestKnownPhotosAsync(string apiKey, int limit = 20, string model = "gpt-4o-mini")
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MyPhotoHelperDbContext>();
            
            var knownPhotos = await GetKnownPhotos(dbContext, limit);
            var results = new List<MetadataClassificationResult>();
            
            foreach (var image in knownPhotos)
            {
                var result = await _classificationService.ClassifyImageAsync(image, apiKey, model);
                results.Add(result);
            }
            
            return results;
        }

        public async Task<MetadataClassificationTestResults> TestSpecificImagesAsync(string apiKey, List<int> imageIds, string model = "gpt-4o-mini")
        {
            var startTime = DateTime.UtcNow;
            var results = new MetadataClassificationTestResults
            {
                Model = model,
                TestDate = startTime
            };

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<MyPhotoHelperDbContext>();

                var images = await dbContext.tbl_images
                    .Where(img => imageIds.Contains(img.ImageId))
                    .Include(img => img.tbl_image_metadata)
                    .Include(img => img.tbl_image_analysis)
                    .ToListAsync();

                foreach (var image in images)
                {
                    var classificationResult = await _classificationService.ClassifyImageAsync(image, apiKey, model);
                    
                    var actualCategory = image.tbl_image_analysis?.ImageCategory ?? "unknown";
                    
                    var testResult = new MetadataClassificationTestResult
                    {
                        ImageId = classificationResult.ImageId,
                        FileName = classificationResult.FileName,
                        Category = classificationResult.Category,
                        Confidence = classificationResult.Confidence,
                        Reasoning = classificationResult.Reasoning,
                        HasError = classificationResult.HasError,
                        ErrorMessage = classificationResult.ErrorMessage,
                        ActualCategory = actualCategory,
                        TestNotes = $"Manual test of specific image ID {image.ImageId}"
                    };

                    results.DetailedResults.Add(testResult);
                    
                    if (!testResult.HasError && actualCategory != "unknown")
                    {
                        if (testResult.IsCorrect)
                        {
                            results.CorrectPredictions++;
                        }
                        else
                        {
                            results.IncorrectPredictions++;
                        }
                    }
                }

                results.TotalTested = results.CorrectPredictions + results.IncorrectPredictions;
                results.Duration = DateTime.UtcNow - startTime;
                results.Summary = GenerateTestSummary(results);
                
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing specific images");
                results.Summary = $"Test failed: {ex.Message}";
                results.Duration = DateTime.UtcNow - startTime;
                return results;
            }
        }

        private async Task<List<tbl_images>> GetKnownScreenshots(MyPhotoHelperDbContext dbContext, int limit)
        {
            return await dbContext.tbl_images
                .Where(img => img.FileExists == 1 && img.IsDeleted == 0)
                .Where(img => img.tbl_image_analysis != null && img.tbl_image_analysis.ImageCategory == "screenshot")
                .Include(img => img.tbl_image_metadata)
                .Include(img => img.tbl_image_analysis)
                .OrderBy(img => Guid.NewGuid()) // Random sample
                .Take(limit)
                .ToListAsync();
        }

        private async Task<List<tbl_images>> GetKnownPhotos(MyPhotoHelperDbContext dbContext, int limit)
        {
            return await dbContext.tbl_images
                .Where(img => img.FileExists == 1 && img.IsDeleted == 0)
                .Where(img => img.tbl_image_analysis != null && img.tbl_image_analysis.ImageCategory == "photo")
                .Include(img => img.tbl_image_metadata)
                .Include(img => img.tbl_image_analysis)
                .OrderBy(img => Guid.NewGuid()) // Random sample
                .Take(limit)
                .ToListAsync();
        }

        private string GenerateTestSummary(MetadataClassificationTestResults results)
        {
            var summary = $"""
                Metadata Classification Test Results
                ===================================
                Model: {results.Model}
                Test Date: {results.TestDate:yyyy-MM-dd HH:mm:ss}
                Duration: {results.Duration.TotalSeconds:F1} seconds
                
                Overall Performance:
                - Total Images Tested: {results.TotalTested}
                - Correct Predictions: {results.CorrectPredictions}
                - Incorrect Predictions: {results.IncorrectPredictions}
                - Accuracy: {results.AccuracyPercentage:F1}%
                
                Screenshot Detection:
                - True Positives: {results.TruePositiveScreenshots}
                - False Positives: {results.FalsePositiveScreenshots}
                - False Negatives: {results.FalseNegativeScreenshots}
                
                Photo Detection:
                - True Positives: {results.TruePositivePhotos}
                - False Positives: {results.FalsePositivePhotos}
                - False Negatives: {results.FalseNegativePhotos}
                
                Errors: {results.DetailedResults.Count(r => r.HasError)}
                """;

            return summary;
        }
    }
}