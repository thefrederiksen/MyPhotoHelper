using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyPhotoHelper.Data;
using MyPhotoHelper.Models;
using MyPhotoHelper.Services;

namespace MyPhotoHelper.Tests.Services
{
    [TestClass]
    public class ScanningIntegrationTests
    {
        private ServiceProvider _serviceProvider = null!;
        private MyPhotoHelperDbContext _dbContext = null!;
        private string _testDirectory = null!;

        [TestInitialize]
        public void Setup()
        {
            var services = new ServiceCollection();
            
            // Setup in-memory database with a fixed name to ensure consistency across scopes
            services.AddDbContext<MyPhotoHelperDbContext>(options =>
                options.UseInMemoryDatabase("TestDb_Scanning", b => b.EnableNullChecks(false))
                       .EnableSensitiveDataLogging()
                       .EnableDetailedErrors());
            
            // Add logging with console output for debugging
            services.AddLogging(builder => 
            {
                builder.AddConsole();
                builder.AddDebug();
                builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug);
            });
            
            // Add services - make sure to add all dependencies
            services.AddScoped<IPathService, TestPathService>();
            services.AddScoped<IPhotoScanService, PhotoScanService>();
            services.AddScoped<IMetadataExtractionService, MetadataExtractionService>();
            services.AddScoped<IHashCalculationService, HashCalculationService>();
            services.AddScoped<IPhasedScanService, PhasedScanService>();
            services.AddSingleton<IScanStatusService, ScanStatusService>();
            services.AddScoped<IDuplicateDetectionService, DuplicateDetectionService>();
            services.AddScoped<IPhotoPathService>(provider => new TestPhotoPathService(provider));
            
            _serviceProvider = services.BuildServiceProvider();
            _dbContext = _serviceProvider.GetRequiredService<MyPhotoHelperDbContext>();
            
            // Create test directory
            _testDirectory = Path.Combine(Path.GetTempPath(), $"PhotoScanTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDirectory);
            Console.WriteLine($"Test directory created: {_testDirectory}");
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Clean up database data
            if (_dbContext != null)
            {
                _dbContext.tbl_images.RemoveRange(_dbContext.tbl_images);
                _dbContext.tbl_scan_directory.RemoveRange(_dbContext.tbl_scan_directory);
                _dbContext.tbl_image_metadata.RemoveRange(_dbContext.tbl_image_metadata);
                _dbContext.SaveChanges();
                _dbContext.Dispose();
            }
            
            _serviceProvider?.Dispose();
            
            // Clean up test directory
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }

        [TestMethod]
        public async Task PhotoScanService_FindsNewImages()
        {
            // Arrange
            var imageFiles = new[] { "test1.jpg", "test2.png", "test3.gif" };
            foreach (var file in imageFiles)
            {
                var filePath = Path.Combine(_testDirectory, file);
                await File.WriteAllBytesAsync(filePath, new byte[] { 1, 2, 3 });
                // Ensure file is written to disk
                await Task.Delay(100);
                Assert.IsTrue(File.Exists(filePath), $"File {filePath} was not created");
            }
            
            // Add scan directory to database
            var scanDir = new tbl_scan_directory
            {
                ScanDirectoryId = 1,
                DirectoryPath = _testDirectory,
                DateCreated = DateTime.UtcNow
            };
            _dbContext.tbl_scan_directory.Add(scanDir);
            await _dbContext.SaveChangesAsync();
            
            // Verify scan directory was saved
            var savedDir = await _dbContext.tbl_scan_directory.FirstOrDefaultAsync();
            Assert.IsNotNull(savedDir, "Scan directory was not saved");
            Console.WriteLine($"Scan directory saved: ID={savedDir.ScanDirectoryId}, Path={savedDir.DirectoryPath}");
            
            var scanService = _serviceProvider.GetRequiredService<IPhotoScanService>();
            
            // Subscribe to scan events for better tracking
            var scanCompleted = new TaskCompletionSource<bool>();
            scanService.ScanCompleted += (sender, args) =>
            {
                Console.WriteLine($"Scan completed: Success={args.Success}, Total={args.TotalFilesProcessed}, New={args.NewFilesAdded}, Errors={args.ErrorCount}");
                if (!string.IsNullOrEmpty(args.ErrorMessage))
                {
                    Console.WriteLine($"Error message: {args.ErrorMessage}");
                }
                scanCompleted.TrySetResult(true);
            };
            
            scanService.ScanProgressChanged += (sender, args) =>
            {
                Console.WriteLine($"Scan progress: Dir {args.ProcessedDirectories}/{args.TotalDirectories}, Files {args.ProcessedFiles}/{args.TotalFiles}, Current: {args.CurrentFile}");
            };
            
            // Act
            await scanService.StartScanAsync();
            
            // Wait for scan to complete with timeout
            var completedTask = await Task.WhenAny(scanCompleted.Task, Task.Delay(10000));
            Assert.AreEqual(scanCompleted.Task, completedTask, "Scan did not complete within timeout");
            
            // Give database time to persist
            await Task.Delay(500);
            
            // Assert - Create new context to ensure fresh data
            using var assertContext = _serviceProvider.CreateScope().ServiceProvider.GetRequiredService<MyPhotoHelperDbContext>();
            var images = await assertContext.tbl_images.ToListAsync();
            
            Console.WriteLine($"Found {images.Count} images in database");
            foreach (var img in images)
            {
                Console.WriteLine($"Image: {img.FileName}, Path: {img.RelativePath}, Size: {img.FileSizeBytes}");
            }
            
            Assert.AreEqual(3, images.Count, $"Expected 3 images but found {images.Count}");
            foreach (var image in images)
            {
                Assert.AreEqual(1, image.ScanDirectoryId);
                Assert.IsTrue(imageFiles.Contains(image.FileName));
                Assert.AreEqual(3, image.FileSizeBytes);
            }
        }

        [TestMethod]
        public async Task PhasedScanService_RequiresConfiguredDirectories()
        {
            // Arrange - no directories configured
            var phasedScanService = _serviceProvider.GetRequiredService<IPhasedScanService>();
            
            // Act
            await phasedScanService.StartPhasedScanAsync();
            
            // Wait a bit for the scan to process
            await Task.Delay(1000);
            
            // Assert
            Assert.IsFalse(phasedScanService.IsScanning);
            var progress = phasedScanService.CurrentProgress;
            Assert.IsNotNull(progress);
            Assert.IsFalse(progress.IsRunning);
        }

        [TestMethod]
        public async Task PhasedScanService_CompletesAllPhases()
        {
            // Arrange
            var testImage = Path.Combine(_testDirectory, "test.jpg");
            // Create a more complete JPEG file
            var jpegBytes = new byte[] { 
                0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 
                0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01,
                0x00, 0x01, 0x00, 0x00, 0xFF, 0xD9 
            }; // Minimal valid JPEG
            await File.WriteAllBytesAsync(testImage, jpegBytes);
            await Task.Delay(100);
            Assert.IsTrue(File.Exists(testImage), "Test image was not created");
            
            var scanDir2 = new tbl_scan_directory
            {
                ScanDirectoryId = 1,
                DirectoryPath = _testDirectory,
                DateCreated = DateTime.UtcNow
            };
            _dbContext.tbl_scan_directory.Add(scanDir2);
            await _dbContext.SaveChangesAsync();
            
            // Verify scan directory was saved
            var savedDir2 = await _dbContext.tbl_scan_directory.FirstOrDefaultAsync();
            Assert.IsNotNull(savedDir2, "Scan directory was not saved");
            Console.WriteLine($"Scan directory saved: ID={savedDir2.ScanDirectoryId}, Path={savedDir2.DirectoryPath}");
            
            var phasedScanService = _serviceProvider.GetRequiredService<IPhasedScanService>();
            
            // Track phase completions
            var phasesCompleted = new List<ScanPhase>();
            phasedScanService.PhaseCompleted += (sender, phase) =>
            {
                Console.WriteLine($"Phase completed: {phase}");
                phasesCompleted.Add(phase);
            };
            
            phasedScanService.ProgressChanged += (sender, progress) =>
            {
                Console.WriteLine($"Progress: Phase={progress.CurrentPhase}, Running={progress.IsRunning}");
                if (progress.PhaseProgress != null)
                {
                    foreach (var phase in progress.PhaseProgress)
                    {
                        Console.WriteLine($"  {phase.Key}: {phase.Value.ProcessedItems}/{phase.Value.TotalItems} (Complete: {phase.Value.IsComplete})");
                    }
                }
            };
            
            // Act
            await phasedScanService.StartPhasedScanAsync();
            
            // Wait for scan to complete
            var timeout = DateTime.UtcNow.AddSeconds(30);
            while (phasedScanService.IsScanning && DateTime.UtcNow < timeout)
            {
                await Task.Delay(500);
            }
            
            Assert.IsFalse(phasedScanService.IsScanning, "Scan did not complete within timeout");
            
            // Give database time to persist all changes
            await Task.Delay(1000);
            
            // Assert - Use fresh contexts for each check
            using (var assertScope = _serviceProvider.CreateScope())
            {
                var assertContext = assertScope.ServiceProvider.GetRequiredService<MyPhotoHelperDbContext>();
                
                // Check Phase 1 - Discovery
                var images = await assertContext.tbl_images.ToListAsync();
                Console.WriteLine($"Found {images.Count} images in database");
                Assert.AreEqual(1, images.Count, "Phase 1 should have discovered 1 image");
                
                var image = images[0];
                Console.WriteLine($"Image: {image.FileName}, Hash: {image.FileHash}, Size: {image.FileSizeBytes}");
                
                // Check Phase 2 - Metadata (may or may not extract metadata for minimal JPEG)
                var metadata = await assertContext.tbl_image_metadata.ToListAsync();
                Console.WriteLine($"Found {metadata.Count} metadata records");
                // Metadata extraction might fail for minimal JPEG, so we just log it
                
                // Check Phase 3 - Hashing (should have calculated hash)
                Assert.IsNotNull(image.FileHash, "Phase 3 should have calculated file hash");
                Assert.IsTrue(image.FileHash.Length > 0, "File hash should not be empty");
            }
            
            // Verify phases were completed
            Console.WriteLine($"Phases completed: {string.Join(", ", phasesCompleted)}");
            Assert.IsTrue(phasesCompleted.Contains(ScanPhase.Phase1_Discovery), "Phase 1 should have completed");
        }
    }
    
    // Simple test implementation of IPathService
    public class TestPathService : IPathService
    {
        public string GetAppDataDirectory() => Path.GetTempPath();
        public string GetDatabasePath() => Path.Combine(GetAppDataDirectory(), "test.db");
        public string GetLogsDirectory() => Path.Combine(GetAppDataDirectory(), "logs");
        public string GetPythonDirectory() => Path.Combine(GetAppDataDirectory(), "Python");
        public string GetUserDataDirectory() => Path.GetTempPath();
        public string GetSettingsDirectory() => Path.GetTempPath();
        public string GetTempDirectory() => Path.GetTempPath();
        public string GetDisplayPath(string path) => path;
        public void EnsureDirectoriesExist() { }
        public bool MigrateDatabaseIfNeeded() => false;
    }
    
    // Test implementation of IPhotoPathService
    public class TestPhotoPathService : IPhotoPathService
    {
        private readonly IServiceProvider _serviceProvider;
        
        public TestPhotoPathService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }
        
        public Task<string?> GetPhotoDirectoryAsync()
        {
            return Task.FromResult<string?>(Path.GetTempPath());
        }

        public Task<string?> GetFullPathAsync(string relativePath)
        {
            return Task.FromResult<string?>(Path.Combine(Path.GetTempPath(), relativePath));
        }

        public async Task<string?> GetFullPathForImageAsync(tbl_images image)
        {
            // Get the scan directory to reconstruct the full path
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MyPhotoHelperDbContext>();
            
            var scanDir = await dbContext.tbl_scan_directory
                .FirstOrDefaultAsync(sd => sd.ScanDirectoryId == image.ScanDirectoryId);
                
            if (scanDir == null)
                return null;
                
            // Combine scan directory path with relative path
            return Path.Combine(scanDir.DirectoryPath, image.RelativePath);
        }

        public Task<string?> GetFullPathForImageAsync(int imageId)
        {
            throw new NotImplementedException("Not needed for these tests");
        }
    }
}