using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyPhotoHelper.Data;
using MyPhotoHelper.Models;
using MyPhotoHelper.Services;
using CSnakes.Runtime;

namespace MyPhotoHelper.Tests.Services
{
    [TestClass]
    public class MetadataClassificationTests
    {
        private ServiceProvider _serviceProvider = null!;
        private MyPhotoHelperDbContext _dbContext = null!;
        private string _testDirectory = null!;
        
        // Set this to your OpenAI API key for testing
        private const string TEST_API_KEY = ""; // Leave empty to skip API tests

        [TestInitialize]
        public void Setup()
        {
            var services = new ServiceCollection();
            
            // Setup in-memory database with unique name to avoid cross-test contamination
            var dbName = $"TestDb_MetadataClassification_{Guid.NewGuid()}";
            services.AddDbContext<MyPhotoHelperDbContext>(options =>
                options.UseInMemoryDatabase(dbName, b => b.EnableNullChecks(false))
                       .EnableSensitiveDataLogging()
                       .EnableDetailedErrors());
            
            // Add logging
            services.AddLogging(builder => 
            {
                builder.AddConsole();
                builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug);
            });
            
            // Add Python environment for CSnakes (if available)
            var pythonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Python");
            if (Directory.Exists(pythonPath))
            {
                try
                {
                    // Try to add Python - this may not be available in test environment
                    services.AddSingleton<IPythonEnvironment>(provider => null!);
                }
                catch
                {
                    // Python not available in test environment
                }
            }
            
            // Add test services
            services.AddScoped<IMetadataClassificationService, MetadataClassificationService>();
            services.AddScoped<IMetadataClassificationTestService, MetadataClassificationTestService>();
            
            _serviceProvider = services.BuildServiceProvider();
            _dbContext = _serviceProvider.GetRequiredService<MyPhotoHelperDbContext>();
            
            // Create test directory
            _testDirectory = Path.Combine(Path.GetTempPath(), $"MetadataClassificationTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDirectory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _dbContext?.Dispose();
            _serviceProvider?.Dispose();
            
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }

        [TestMethod]
        public void MetadataClassificationService_CanBeConstructed()
        {
            // Arrange & Act
            var service = _serviceProvider.GetService<IMetadataClassificationService>();
            
            // Assert
            Assert.IsNotNull(service, "MetadataClassificationService should be available from DI container");
        }

        [TestMethod]
        public void MetadataClassificationTestService_CanBeConstructed()
        {
            // Arrange & Act
            var service = _serviceProvider.GetService<IMetadataClassificationTestService>();
            
            // Assert
            Assert.IsNotNull(service, "MetadataClassificationTestService should be available from DI container");
        }

        [TestMethod]
        public async Task MetadataClassificationService_PrepareMetadata_CreatesCorrectStructure()
        {
            // Arrange
            var testImage = CreateTestImageWithMetadata();
            await _dbContext.tbl_images.AddAsync(testImage);
            await _dbContext.SaveChangesAsync();
            
            var service = _serviceProvider.GetRequiredService<IMetadataClassificationService>();
            
            // Use reflection to access the private method for testing
            var prepareMethod = typeof(MetadataClassificationService)
                .GetMethod("PrepareMetadataForClassification", 
                          System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            // Act
            var metadata = (Dictionary<string, object?>)prepareMethod!.Invoke(service, new object[] { testImage })!;
            
            // Assert
            Assert.IsNotNull(metadata, "Metadata should not be null");
            Assert.IsTrue(metadata.ContainsKey("file_name"), "Metadata should contain file_name");
            Assert.IsTrue(metadata.ContainsKey("file_extension"), "Metadata should contain file_extension");
            Assert.IsTrue(metadata.ContainsKey("width"), "Metadata should contain width");
            Assert.IsTrue(metadata.ContainsKey("height"), "Metadata should contain height");
            Assert.IsTrue(metadata.ContainsKey("camera_make"), "Metadata should contain camera_make");
            
            Assert.AreEqual("test_image.jpg", metadata["file_name"]);
            Assert.AreEqual(".jpg", metadata["file_extension"]);
            Assert.AreEqual(1920, metadata["width"]);
            Assert.AreEqual(1080, metadata["height"]);
            Assert.AreEqual("Canon", metadata["camera_make"]);
        }

        [TestMethod]
        public async Task GetUnknownImagesForClassification_ReturnsUnknownImages()
        {
            // Arrange - create fresh test data for this test
            var testImages = new List<tbl_images>();
            
            // Create known photo (should NOT be returned)
            var knownPhoto = new tbl_images
            {
                ImageId = 1001,
                FileName = "known_photo.jpg",
                FileExtension = ".jpg",
                FileSizeBytes = 1024000,
                DateCreated = DateTime.UtcNow,
                DateModified = DateTime.UtcNow,
                FileExists = 1,
                IsDeleted = 0,
                ScanDirectoryId = 1
            };
            knownPhoto.tbl_image_analysis = new tbl_image_analysis
            {
                ImageId = 1001,
                ImageCategory = "photo",
                AIAnalyzedAt = DateTime.UtcNow
            };
            testImages.Add(knownPhoto);
            
            // Create known screenshot (should NOT be returned)
            var knownScreenshot = new tbl_images
            {
                ImageId = 1002,
                FileName = "known_screenshot.png",
                FileExtension = ".png",
                FileSizeBytes = 512000,
                DateCreated = DateTime.UtcNow,
                DateModified = DateTime.UtcNow,
                FileExists = 1,
                IsDeleted = 0,
                ScanDirectoryId = 1
            };
            knownScreenshot.tbl_image_analysis = new tbl_image_analysis
            {
                ImageId = 1002,
                ImageCategory = "screenshot",
                AIAnalyzedAt = DateTime.UtcNow
            };
            testImages.Add(knownScreenshot);
            
            // Create unknown image with analysis (SHOULD be returned)
            var unknownImage1 = new tbl_images
            {
                ImageId = 1003,
                FileName = "unknown1.jpg",
                FileExtension = ".jpg",
                FileSizeBytes = 800000,
                DateCreated = DateTime.UtcNow,
                DateModified = DateTime.UtcNow,
                FileExists = 1,
                IsDeleted = 0,
                ScanDirectoryId = 1
            };
            unknownImage1.tbl_image_analysis = new tbl_image_analysis
            {
                ImageId = 1003,
                ImageCategory = "unknown",
                AIAnalyzedAt = DateTime.UtcNow
            };
            testImages.Add(unknownImage1);
            
            // Create image without analysis (SHOULD be returned)
            var unknownImage2 = new tbl_images
            {
                ImageId = 1004,
                FileName = "no_analysis.jpg",
                FileExtension = ".jpg",
                FileSizeBytes = 600000,
                DateCreated = DateTime.UtcNow,
                DateModified = DateTime.UtcNow,
                FileExists = 1,
                IsDeleted = 0,
                ScanDirectoryId = 1
            };
            // No tbl_image_analysis for this one
            testImages.Add(unknownImage2);
            
            await _dbContext.tbl_images.AddRangeAsync(testImages);
            await _dbContext.SaveChangesAsync();
            
            var service = _serviceProvider.GetRequiredService<IMetadataClassificationService>();
            
            // Act
            var unknownImages = await service.GetUnknownImagesForClassificationAsync(10);
            
            // Assert
            Assert.AreEqual(2, unknownImages.Count, "Should find exactly 2 unknown images");
            Assert.IsTrue(unknownImages.All(i => i.Category == "unknown"), "All results should be marked as unknown");
            
            var imageIds = unknownImages.Select(i => i.ImageId).ToList();
            Assert.IsTrue(imageIds.Contains(unknownImage1.ImageId), "Should include unknown image with 'unknown' category");
            Assert.IsTrue(imageIds.Contains(unknownImage2.ImageId), "Should include image without analysis");
            
            // Verify it does NOT include the known photo and screenshot
            Assert.IsFalse(imageIds.Contains(knownPhoto.ImageId), "Should NOT include known photo");
            Assert.IsFalse(imageIds.Contains(knownScreenshot.ImageId), "Should NOT include known screenshot");
        }

        [TestMethod]
        public async Task ClassifyImageAsync_WithoutApiKey_ReturnsError()
        {
            // Skip this test if Python environment is not available
            if (_serviceProvider.GetService<IPythonEnvironment>() == null)
            {
                Assert.Inconclusive("Python environment not available - this is expected in CI/test environments");
                return;
            }
            
            // Arrange
            var testImage = CreateTestImageWithMetadata();
            await _dbContext.tbl_images.AddAsync(testImage);
            await _dbContext.SaveChangesAsync();
            
            var service = _serviceProvider.GetRequiredService<IMetadataClassificationService>();
            
            // Act
            var result = await service.ClassifyImageAsync(testImage, "", "gpt-4o-mini");
            
            // Assert
            Assert.IsTrue(result.HasError, "Should return error when API key is empty");
            Assert.AreEqual("unknown", result.Category, "Should default to unknown on error");
            Assert.AreEqual(0.0, result.Confidence, "Should have zero confidence on error");
        }

        [TestMethod]
        public async Task TestKnownScreenshotsAsync_WithoutApiKey_ReturnsEmptyList()
        {
            // Arrange
            var testService = _serviceProvider.GetRequiredService<IMetadataClassificationTestService>();
            
            // Act
            var results = await testService.TestKnownScreenshotsAsync("", 5);
            
            // Assert
            Assert.IsNotNull(results, "Results should not be null");
            Assert.AreEqual(0, results.Count, "Should return empty list when no known screenshots exist");
        }

        [TestMethod]
        public async Task TestKnownPhotosAsync_WithoutApiKey_ReturnsEmptyList()
        {
            // Arrange
            var testService = _serviceProvider.GetRequiredService<IMetadataClassificationTestService>();
            
            // Act
            var results = await testService.TestKnownPhotosAsync("", 5);
            
            // Assert
            Assert.IsNotNull(results, "Results should not be null");
            Assert.AreEqual(0, results.Count, "Should return empty list when no known photos exist");
        }

        [TestMethod]
        [TestCategory("RequiresAPI")]
        public async Task RunAccuracyTestAsync_WithApiKey_CompletesSuccessfully()
        {
            if (string.IsNullOrEmpty(TEST_API_KEY))
            {
                Assert.Inconclusive("API key not provided - set TEST_API_KEY to run this test");
                return;
            }
            
            if (_serviceProvider.GetService<IPythonEnvironment>() == null)
            {
                Assert.Inconclusive("Python environment not available - this is expected in CI/test environments");
                return;
            }

            // Arrange
            var knownPhoto = CreateTestImageWithPhotoMetadata();
            var knownScreenshot = CreateTestImageWithScreenshotMetadata();
            
            await _dbContext.tbl_images.AddRangeAsync(knownPhoto, knownScreenshot);
            await _dbContext.SaveChangesAsync();
            
            var testService = _serviceProvider.GetRequiredService<IMetadataClassificationTestService>();
            
            // Act
            var results = await testService.RunAccuracyTestAsync(TEST_API_KEY, 2);
            
            // Assert
            Assert.IsNotNull(results, "Results should not be null");
            Assert.AreEqual(2, results.TotalTested, "Should test both images");
            Assert.IsTrue(results.Duration.TotalSeconds > 0, "Should record duration");
            Assert.IsNotNull(results.Summary, "Should generate summary");
            Assert.AreEqual(2, results.DetailedResults.Count, "Should have detailed results for both images");
            
            Console.WriteLine($"Accuracy Test Results:");
            Console.WriteLine($"Total Tested: {results.TotalTested}");
            Console.WriteLine($"Correct: {results.CorrectPredictions}");
            Console.WriteLine($"Accuracy: {results.AccuracyPercentage:F1}%");
            Console.WriteLine($"Duration: {results.Duration.TotalSeconds:F1}s");
            Console.WriteLine();
            Console.WriteLine(results.Summary);
        }

        [TestMethod]
        [TestCategory("RequiresAPI")]
        public async Task ClassifyImageAsync_WithPhotoMetadata_ReturnsPhoto()
        {
            if (string.IsNullOrEmpty(TEST_API_KEY))
            {
                Assert.Inconclusive("API key not provided - set TEST_API_KEY to run this test");
                return;
            }
            
            if (_serviceProvider.GetService<IPythonEnvironment>() == null)
            {
                Assert.Inconclusive("Python environment not available - this is expected in CI/test environments");
                return;
            }

            // Arrange
            var photoImage = CreateTestImageWithPhotoMetadata();
            await _dbContext.tbl_images.AddAsync(photoImage);
            await _dbContext.SaveChangesAsync();
            
            var service = _serviceProvider.GetRequiredService<IMetadataClassificationService>();
            
            // Act
            var result = await service.ClassifyImageAsync(photoImage, TEST_API_KEY);
            
            // Assert
            Assert.IsFalse(result.HasError, $"Classification should not error: {result.ErrorMessage}");
            Assert.IsTrue(result.Category == "photo" || result.Category == "unknown", 
                         $"Should classify as photo or unknown, got: {result.Category}");
            Assert.IsTrue(result.Confidence >= 0.0 && result.Confidence <= 1.0, 
                         "Confidence should be between 0 and 1");
            Assert.IsNotNull(result.Reasoning, "Should provide reasoning");
            
            Console.WriteLine($"Photo Classification Result:");
            Console.WriteLine($"Category: {result.Category}");
            Console.WriteLine($"Confidence: {result.Confidence:F2}");
            Console.WriteLine($"Reasoning: {result.Reasoning}");
        }

        [TestMethod]
        [TestCategory("RequiresAPI")]
        public async Task ClassifyImageAsync_WithScreenshotMetadata_ReturnsScreenshot()
        {
            if (string.IsNullOrEmpty(TEST_API_KEY))
            {
                Assert.Inconclusive("API key not provided - set TEST_API_KEY to run this test");
                return;
            }
            
            if (_serviceProvider.GetService<IPythonEnvironment>() == null)
            {
                Assert.Inconclusive("Python environment not available - this is expected in CI/test environments");
                return;
            }

            // Arrange
            var screenshotImage = CreateTestImageWithScreenshotMetadata();
            await _dbContext.tbl_images.AddAsync(screenshotImage);
            await _dbContext.SaveChangesAsync();
            
            var service = _serviceProvider.GetRequiredService<IMetadataClassificationService>();
            
            // Act
            var result = await service.ClassifyImageAsync(screenshotImage, TEST_API_KEY);
            
            // Assert
            Assert.IsFalse(result.HasError, $"Classification should not error: {result.ErrorMessage}");
            Assert.IsTrue(result.Category == "screenshot" || result.Category == "unknown", 
                         $"Should classify as screenshot or unknown, got: {result.Category}");
            Assert.IsTrue(result.Confidence >= 0.0 && result.Confidence <= 1.0, 
                         "Confidence should be between 0 and 1");
            Assert.IsNotNull(result.Reasoning, "Should provide reasoning");
            
            Console.WriteLine($"Screenshot Classification Result:");
            Console.WriteLine($"Category: {result.Category}");
            Console.WriteLine($"Confidence: {result.Confidence:F2}");
            Console.WriteLine($"Reasoning: {result.Reasoning}");
        }

        // Helper methods to create test data

        private tbl_images CreateTestImageWithMetadata()
        {
            var image = new tbl_images
            {
                ImageId = 1,
                FileName = "test_image.jpg",
                FileExtension = ".jpg",
                RelativePath = "test_image.jpg",
                FileSizeBytes = 1024000,
                DateCreated = DateTime.UtcNow.AddDays(-30),
                DateModified = DateTime.UtcNow.AddDays(-30),
                FileExists = 1,
                IsDeleted = 0,
                ScanDirectoryId = 1
            };

            image.tbl_image_metadata = new tbl_image_metadata
            {
                ImageId = 1,
                Width = 1920,
                Height = 1080,
                DateTaken = DateTime.UtcNow.AddDays(-30),
                CameraMake = "Canon",
                CameraModel = "EOS R5",
                Software = "Adobe Lightroom",
                ColorSpace = "sRGB",
                Orientation = "Horizontal",
                BitDepth = 8,
                ResolutionX = 72.0,
                ResolutionY = 72.0,
                FocalLength = 85.0,
                FNumber = "2.8",
                ISO = 400,
                ExposureTime = "1/200"
            };

            return image;
        }

        private tbl_images CreateTestImageWithPhotoMetadata()
        {
            var image = new tbl_images
            {
                ImageId = 2,
                FileName = "photo_sample.jpg",
                FileExtension = ".jpg",
                RelativePath = "photo_sample.jpg",
                FileSizeBytes = 2048000,
                DateCreated = DateTime.UtcNow.AddDays(-15),
                DateModified = DateTime.UtcNow.AddDays(-15),
                FileExists = 1,
                IsDeleted = 0,
                ScanDirectoryId = 1
            };

            image.tbl_image_metadata = new tbl_image_metadata
            {
                ImageId = 2,
                Width = 4000,
                Height = 3000,
                DateTaken = DateTime.UtcNow.AddDays(-15),
                CameraMake = "Sony",
                CameraModel = "A7R IV",
                Software = "Sony A7R IV Ver.1.00",
                ColorSpace = "Adobe RGB",
                Orientation = "Horizontal",
                BitDepth = 14,
                ResolutionX = 300.0,
                ResolutionY = 300.0,
                Latitude = 37.7749,
                Longitude = -122.4194,
                FocalLength = 24.0,
                FNumber = "5.6",
                ISO = 100,
                ExposureTime = "1/60"
            };

            image.tbl_image_analysis = new tbl_image_analysis
            {
                ImageId = 2,
                ImageCategory = "photo",
                AIAnalyzedAt = DateTime.UtcNow.AddDays(-14)
            };

            return image;
        }

        private tbl_images CreateTestImageWithScreenshotMetadata()
        {
            var image = new tbl_images
            {
                ImageId = 3,
                FileName = "screenshot_sample.png",
                FileExtension = ".png",
                RelativePath = "screenshot_sample.png",
                FileSizeBytes = 512000,
                DateCreated = DateTime.UtcNow.AddDays(-5),
                DateModified = DateTime.UtcNow.AddDays(-5),
                FileExists = 1,
                IsDeleted = 0,
                ScanDirectoryId = 1
            };

            image.tbl_image_metadata = new tbl_image_metadata
            {
                ImageId = 3,
                Width = 1920,
                Height = 1080,
                // No DateTaken (typical for screenshots)
                // No camera info (typical for screenshots)
                Software = "Snipping Tool",
                ColorSpace = "sRGB",
                Orientation = "Horizontal",
                BitDepth = 8,
                ResolutionX = 96.0,
                ResolutionY = 96.0
                // No GPS, focal length, exposure data (typical for screenshots)
            };

            image.tbl_image_analysis = new tbl_image_analysis
            {
                ImageId = 3,
                ImageCategory = "screenshot",
                AIAnalyzedAt = DateTime.UtcNow.AddDays(-4)
            };

            return image;
        }

        // Helper methods removed - using inline test data creation for better isolation
    }
}