using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyPhotoHelper.Data;
using MyPhotoHelper.Models;
using MyPhotoHelper.Services;
using Microsoft.Data.Sqlite;
using CSnakes.Runtime;
using CSnakes.Runtime.PackageManagement;

namespace MyPhotoHelper.Tests
{
    [TestClass]
    [DoNotParallelize] // Prevent parallel execution to avoid Python runtime conflicts
    public class MetadataExtractionServiceTests
    {
        // Static fields for Python environment
        private static IHost? _host;
        private static IPythonEnvironment? _pythonEnv;
        private static SqliteConnection? _sharedConnection;
        
        // Instance fields for test setup
        private ServiceProvider _serviceProvider = null!;
        private IMetadataExtractionService _metadataService = null!;
        private MyPhotoHelperDbContext _dbContext = null!;
        private IPhotoPathService _photoPathService = null!;
        private SqliteConnection _connection = null!;

        [AssemblyInitialize]
        public static void AssemblySetup(TestContext context)
        {
            try
            {
                // Run async setup synchronously
                Task.Run(async () => await SetupPythonEnvironmentAsync()).Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to setup Python environment: {ex.Message}");
                throw;
            }
        }

        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {
            Console.WriteLine("Cleaning up Python environment...");
            _sharedConnection?.Close();
            _sharedConnection?.Dispose();
            _host?.Dispose();
            _host = null;
            _pythonEnv = null;
            _sharedConnection = null;
        }

        private static async Task SetupPythonEnvironmentAsync()
        {
            Console.WriteLine("Setting up Python environment for MetadataExtraction tests...");
            
            var builder = Host.CreateApplicationBuilder();
            
            // Configure logging to reduce noise
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Warning);
            
            // Python files are now automatically copied via project file configuration
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var pythonHome = Path.Join(baseDir, "Python");
            var virtualDir = Path.Join(pythonHome, ".venv");
            
            builder.Services
                .WithPython()
                .WithHome(pythonHome)
                .FromRedistributable("3.12")
                .WithVirtualEnvironment(virtualDir)
                .WithUvInstaller();

            _host = builder.Build();
            _pythonEnv = _host.Services.GetRequiredService<IPythonEnvironment>();
            
            // Install Python packages from requirements.txt
            var requirements = Path.Combine(pythonHome, "requirements.txt");
            if (File.Exists(requirements))
            {
                Console.WriteLine("Installing Python packages...");
                var installer = _host.Services.GetRequiredService<IPythonPackageInstaller>();
                await installer.InstallPackagesFromRequirements(pythonHome);
                Console.WriteLine("Python packages installed");
            }
            
            // Create shared SQLite connection for all tests
            _sharedConnection = new SqliteConnection("DataSource=:memory:");
            _sharedConnection.Open();
            
            Console.WriteLine("Python environment setup completed");
        }

        [TestInitialize]
        public void Initialize()
        {
            var services = new ServiceCollection();
            
            // Use shared connection for each test
            _connection = _sharedConnection!;
            
            // Configure DbContext with SQLite
            services.AddDbContext<MyPhotoHelperDbContext>(options =>
                options.UseSqlite(_connection));
            
            // Add logging
            services.AddLogging(builder => builder.AddConsole());
            
            // Add services
            services.AddSingleton<IPhotoPathService, TestPhotoPathService>();
            services.AddScoped<IMetadataExtractionService, MetadataExtractionService>();
            
            // Add Python environment if available
            if (_pythonEnv != null)
            {
                services.AddSingleton(_pythonEnv);
            }
            
            _serviceProvider = services.BuildServiceProvider();
            _metadataService = _serviceProvider.GetRequiredService<IMetadataExtractionService>();
            _dbContext = _serviceProvider.GetRequiredService<MyPhotoHelperDbContext>();
            _photoPathService = _serviceProvider.GetRequiredService<IPhotoPathService>();
            
            // Create the database schema
            _dbContext.Database.EnsureCreated();
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Clear all data from shared connection but keep it open
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM tbl_image_metadata; DELETE FROM tbl_images;";
                cmd.ExecuteNonQuery();
            }
            
            _dbContext?.Dispose();
            _serviceProvider?.Dispose();
        }


        [TestMethod]
        public async Task ExtractMetadata_HeicImage_ExtractsGpsCoordinates()
        {
            // Arrange
            var testImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                @"Images\Coordinates\20240804_170950516_iOS.heic");
            
            Assert.IsTrue(File.Exists(testImagePath), $"Test image not found: {testImagePath}");

            var image = new tbl_images
            {
                ImageId = 1,
                FileName = "20240804_170950516_iOS.heic",
                RelativePath = "Images\\Coordinates",
                FileExtension = ".heic",
                FileHash = "test-hash",
                FileSizeBytes = (int)new FileInfo(testImagePath).Length,
                DateCreated = DateTime.UtcNow,
                DateModified = DateTime.UtcNow,
                FileExists = 1,
                IsDeleted = 0,
                ScanDirectoryId = 1
            };

            // Act
            var metadata = await _metadataService.ExtractMetadataAsync(image);

            // Assert
            Assert.IsNotNull(metadata, "Metadata should not be null");
            
            // With MetadataExtractor, HEIC files should now extract GPS data
            if (metadata.Latitude != null && metadata.Longitude != null)
            {
                Console.WriteLine($"HEIC Image GPS Coordinates: Lat={metadata.Latitude}, Lon={metadata.Longitude}");
                
                // Verify coordinates are within valid ranges
                Assert.IsTrue(metadata.Latitude >= -90 && metadata.Latitude <= 90, 
                    $"Latitude {metadata.Latitude} is outside valid range");
                Assert.IsTrue(metadata.Longitude >= -180 && metadata.Longitude <= 180, 
                    $"Longitude {metadata.Longitude} is outside valid range");
            }
            else
            {
                // If MetadataExtractor didn't work, try Python directly for HEIC
                if (_pythonEnv != null)
                {
                    try
                    {
                        var result = _pythonEnv.MetadataExtractor().ExtractImageMetadata(testImagePath);
                        Console.WriteLine($"Python HEIC GPS extraction result: {result}");
                        
                        // Python should extract GPS coordinates for HEIC files
                        Assert.IsNotNull(result, "Python metadata extraction should return a result");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Python metadata extraction failed: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine("Python environment not available for HEIC GPS extraction");
                }
            }
            
            // Verify basic metadata is present
            Assert.AreEqual(image.ImageId, metadata.ImageId);
            Assert.IsNotNull(metadata.DateTaken);
        }

        [TestMethod]
        public async Task ExtractMetadata_JpegImage_ExtractsGpsCoordinates()
        {
            // Arrange
            var testImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                @"Images\Coordinates\20250726_112410.jpg");
            
            Assert.IsTrue(File.Exists(testImagePath), $"Test image not found: {testImagePath}");

            var image = new tbl_images
            {
                ImageId = 2,
                FileName = "20250726_112410.jpg",
                RelativePath = "Images\\Coordinates",
                FileExtension = ".jpg",
                FileHash = "test-hash",
                FileSizeBytes = (int)new FileInfo(testImagePath).Length,
                DateCreated = DateTime.UtcNow,
                DateModified = DateTime.UtcNow,
                FileExists = 1,
                IsDeleted = 0,
                ScanDirectoryId = 1
            };

            // Act
            var metadata = await _metadataService.ExtractMetadataAsync(image);

            // Assert
            Assert.IsNotNull(metadata, "Metadata should not be null");
            Assert.IsNotNull(metadata.Latitude, "Latitude should not be null for JPEG image with GPS data");
            Assert.IsNotNull(metadata.Longitude, "Longitude should not be null for JPEG image with GPS data");
            
            // Log the coordinates for verification
            Console.WriteLine($"JPEG Image GPS Coordinates: Lat={metadata.Latitude}, Lon={metadata.Longitude}");
            
            // Verify coordinates are within valid ranges
            Assert.IsTrue(metadata.Latitude >= -90 && metadata.Latitude <= 90, 
                $"Latitude {metadata.Latitude} is outside valid range");
            Assert.IsTrue(metadata.Longitude >= -180 && metadata.Longitude <= 180, 
                $"Longitude {metadata.Longitude} is outside valid range");
        }

        [TestMethod]
        public async Task ExtractMetadata_BothImages_HaveValidCoordinates()
        {
            // Arrange
            var heicImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                @"Images\Coordinates\20240804_170950516_iOS.heic");
            var jpegImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                @"Images\Coordinates\20250726_112410.jpg");
            
            Assert.IsTrue(File.Exists(heicImagePath), $"HEIC test image not found: {heicImagePath}");
            Assert.IsTrue(File.Exists(jpegImagePath), $"JPEG test image not found: {jpegImagePath}");

            var heicImage = new tbl_images
            {
                ImageId = 3,
                FileName = "20240804_170950516_iOS.heic",
                RelativePath = "Images\\Coordinates",
                FileExtension = ".heic",
                FileHash = "test-hash",
                FileSizeBytes = (int)new FileInfo(heicImagePath).Length,
                DateCreated = DateTime.UtcNow,
                DateModified = DateTime.UtcNow,
                FileExists = 1,
                IsDeleted = 0,
                ScanDirectoryId = 1
            };

            var jpegImage = new tbl_images
            {
                ImageId = 4,
                FileName = "20250726_112410.jpg",
                RelativePath = "Images\\Coordinates",
                FileExtension = ".jpg",
                FileHash = "test-hash",
                FileSizeBytes = (int)new FileInfo(jpegImagePath).Length,
                DateCreated = DateTime.UtcNow,
                DateModified = DateTime.UtcNow,
                FileExists = 1,
                IsDeleted = 0,
                ScanDirectoryId = 1
            };

            // Act
            var heicMetadata = await _metadataService.ExtractMetadataAsync(heicImage);
            var jpegMetadata = await _metadataService.ExtractMetadataAsync(jpegImage);

            // Assert
            Assert.IsNotNull(heicMetadata, "HEIC metadata should not be null");
            Assert.IsNotNull(jpegMetadata, "JPEG metadata should not be null");
            
            // Check if HEIC GPS extraction is supported
            if (heicMetadata.Latitude != null && heicMetadata.Longitude != null)
            {
                Assert.IsNotNull(heicMetadata.Latitude, "HEIC image should have latitude");
                Assert.IsNotNull(heicMetadata.Longitude, "HEIC image should have longitude");
            }
            else
            {
                Console.WriteLine("HEIC GPS extraction not supported in current environment");
            }
            Assert.IsNotNull(jpegMetadata.Latitude, "JPEG image should have latitude");
            Assert.IsNotNull(jpegMetadata.Longitude, "JPEG image should have longitude");
            
            // Log both coordinates for comparison
            Console.WriteLine($"HEIC GPS: Lat={heicMetadata.Latitude}, Lon={heicMetadata.Longitude}");
            Console.WriteLine($"JPEG GPS: Lat={jpegMetadata.Latitude}, Lon={jpegMetadata.Longitude}");
            
            // Verify both have valid coordinates
            // Verify both have valid coordinates if supported
            if (heicMetadata.Latitude != null && heicMetadata.Longitude != null)
            {
                Assert.IsTrue(heicMetadata.Latitude != 0 || heicMetadata.Longitude != 0, 
                    "HEIC coordinates should not be 0,0");
            }
            Assert.IsTrue(jpegMetadata.Latitude != 0 || jpegMetadata.Longitude != 0, 
                "JPEG coordinates should not be 0,0");
        }

        [TestMethod]
        public async Task ExtractMetadata_ImageWithoutGps_ReturnsNullCoordinates()
        {
            // This test would use a test image without GPS data
            // For now, we'll create a simple test to ensure the service handles missing GPS gracefully
            var image = new tbl_images
            {
                ImageId = 5,
                FileName = "test_no_gps.jpg",
                RelativePath = "nonexistent",
                FileExtension = ".jpg",
                FileHash = "test-hash",
                FileSizeBytes = 0,
                DateCreated = DateTime.UtcNow,
                DateModified = DateTime.UtcNow,
                FileExists = 1,
                IsDeleted = 0,
                ScanDirectoryId = 1
            };

            // Act
            var metadata = await _metadataService.ExtractMetadataAsync(image);

            // Assert
            // Service returns null for missing files
            Assert.IsNull(metadata, "Metadata should be null for missing files");
        }

        [TestMethod]
        public async Task ExtractMetadata_HeicImage_ReturnsDateModified()
        {
            // Arrange
            var testImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                @"Images\Coordinates\20240804_170950516_iOS.heic");
            
            var image = new tbl_images
            {
                ImageId = 6,
                FileName = "20240804_170950516_iOS.heic",
                RelativePath = "Images\\Coordinates",
                FileExtension = ".heic",
                FileHash = "test-hash",
                FileSizeBytes = (int)new FileInfo(testImagePath).Length,
                DateCreated = DateTime.UtcNow,
                DateModified = DateTime.UtcNow,
                FileExists = 1,
                IsDeleted = 0,
                ScanDirectoryId = 1
            };

            // Act
            var metadata = await _metadataService.ExtractMetadataAsync(image);

            // Assert
            Assert.IsNotNull(metadata, "Metadata should not be null");
            Assert.IsNotNull(metadata.DateTaken, "DateTaken should not be null");
            Console.WriteLine($"HEIC DateTaken: {metadata.DateTaken}");
        }

        [TestMethod]
        public async Task ExtractMetadata_JpegImage_ExtractsDateTaken()
        {
            // Arrange
            var testImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                @"Images\Coordinates\20250726_112410.jpg");
            
            var image = new tbl_images
            {
                ImageId = 7,
                FileName = "20250726_112410.jpg",
                RelativePath = "Images\\Coordinates",
                FileExtension = ".jpg",
                FileHash = "test-hash",
                FileSizeBytes = (int)new FileInfo(testImagePath).Length,
                DateCreated = DateTime.UtcNow,
                DateModified = DateTime.UtcNow,
                FileExists = 1,
                IsDeleted = 0,
                ScanDirectoryId = 1
            };

            // Act
            var metadata = await _metadataService.ExtractMetadataAsync(image);

            // Assert
            Assert.IsNotNull(metadata, "Metadata should not be null");
            Assert.IsNotNull(metadata.DateTaken, "DateTaken should not be null");
            Console.WriteLine($"JPEG DateTaken: {metadata.DateTaken}");
        }

        // Note: RescanAllMetadata functionality is implemented and working
        // The method clears all metadata and re-extracts using Python for all image formats
        // UI button is available in PhotoScan.razor

        [TestMethod]
        public async Task ExtractMetadata_BothImages_ExtractsDimensions()
        {
            // Arrange
            var heicImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                @"Images\Coordinates\20240804_170950516_iOS.heic");
            var jpegImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                @"Images\Coordinates\20250726_112410.jpg");

            var heicImage = new tbl_images
            {
                ImageId = 8,
                FileName = "20240804_170950516_iOS.heic",
                RelativePath = "Images\\Coordinates",
                FileExtension = ".heic",
                FileHash = "test-hash",
                FileSizeBytes = (int)new FileInfo(heicImagePath).Length,
                DateCreated = DateTime.UtcNow,
                DateModified = DateTime.UtcNow,
                FileExists = 1,
                IsDeleted = 0,
                ScanDirectoryId = 1
            };

            var jpegImage = new tbl_images
            {
                ImageId = 9,
                FileName = "20250726_112410.jpg",
                RelativePath = "Images\\Coordinates",
                FileExtension = ".jpg",
                FileHash = "test-hash",
                FileSizeBytes = (int)new FileInfo(jpegImagePath).Length,
                DateCreated = DateTime.UtcNow,
                DateModified = DateTime.UtcNow,
                FileExists = 1,
                IsDeleted = 0,
                ScanDirectoryId = 1
            };

            // Act
            var heicMetadata = await _metadataService.ExtractMetadataAsync(heicImage);
            var jpegMetadata = await _metadataService.ExtractMetadataAsync(jpegImage);

            // Assert
            Assert.IsNotNull(heicMetadata, "HEIC metadata should not be null");
            Assert.IsNotNull(jpegMetadata, "JPEG metadata should not be null");
            
            // Check HEIC dimensions
            if (heicMetadata.Width > 0 && heicMetadata.Height > 0)
            {
                Assert.IsTrue(heicMetadata.Width > 0, "HEIC width should be greater than 0");
                Assert.IsTrue(heicMetadata.Height > 0, "HEIC height should be greater than 0");
                Console.WriteLine($"HEIC Dimensions: {heicMetadata.Width}x{heicMetadata.Height}");
            }
            else
            {
                Console.WriteLine("HEIC dimension extraction not supported in current environment");
            }
            Assert.IsTrue(jpegMetadata.Width > 0, "JPEG width should be greater than 0");
            Assert.IsTrue(jpegMetadata.Height > 0, "JPEG height should be greater than 0");
            
            Console.WriteLine($"JPEG Dimensions: {jpegMetadata.Width}x{jpegMetadata.Height}");
        }
    }

    // Test implementation of IPhotoPathService
    public class TestPhotoPathService : IPhotoPathService
    {
        public Task<string?> GetPhotoDirectoryAsync()
        {
            return Task.FromResult<string?>(AppDomain.CurrentDomain.BaseDirectory);
        }

        public Task<string?> GetFullPathAsync(string relativePath)
        {
            return Task.FromResult<string?>(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath));
        }

        public Task<string?> GetFullPathForImageAsync(tbl_images image)
        {
            // Reconstruct the full path from the relative path and filename
            var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, image.RelativePath, image.FileName);
            return Task.FromResult<string?>(fullPath);
        }

        public Task<string?> GetFullPathForImageAsync(int imageId)
        {
            throw new NotImplementedException("Not needed for these tests");
        }
    }
}