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
            
            // Setup in-memory database
            services.AddDbContext<MyPhotoHelperDbContext>(options =>
                options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
            
            // Add logging
            services.AddLogging(builder => builder.AddDebug());
            
            // Add services
            services.AddScoped<IPathService, TestPathService>();
            services.AddScoped<IPhotoScanService, PhotoScanService>();
            services.AddScoped<IMetadataExtractionService, MetadataExtractionService>();
            services.AddScoped<IHashCalculationService, HashCalculationService>();
            services.AddScoped<IPhasedScanService, PhasedScanService>();
            services.AddSingleton<IScanStatusService, ScanStatusService>();
            
            _serviceProvider = services.BuildServiceProvider();
            _dbContext = _serviceProvider.GetRequiredService<MyPhotoHelperDbContext>();
            
            // Create test directory
            _testDirectory = Path.Combine(Path.GetTempPath(), $"PhotoScanTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDirectory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _dbContext?.Dispose();
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
                await File.WriteAllBytesAsync(Path.Combine(_testDirectory, file), new byte[] { 1, 2, 3 });
            }
            
            _dbContext.tbl_scan_directory.Add(new tbl_scan_directory
            {
                ScanDirectoryId = 1,
                DirectoryPath = _testDirectory,
                DateCreated = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync();
            
            var scanService = _serviceProvider.GetRequiredService<IPhotoScanService>();
            
            // Act
            await scanService.StartScanAsync();
            
            // Wait for scan to complete
            var timeout = DateTime.UtcNow.AddSeconds(5);
            while (scanService.IsScanning && DateTime.UtcNow < timeout)
            {
                await Task.Delay(100);
            }
            
            // Assert
            var images = await _dbContext.tbl_images.ToListAsync();
            Assert.AreEqual(3, images.Count);
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
            await File.WriteAllBytesAsync(testImage, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }); // JPEG header
            
            _dbContext.tbl_scan_directory.Add(new tbl_scan_directory
            {
                ScanDirectoryId = 1,
                DirectoryPath = _testDirectory,
                DateCreated = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync();
            
            var phasedScanService = _serviceProvider.GetRequiredService<IPhasedScanService>();
            
            // Act
            await phasedScanService.StartPhasedScanAsync();
            
            // Wait for scan to complete
            var timeout = DateTime.UtcNow.AddSeconds(10);
            while (phasedScanService.IsScanning && DateTime.UtcNow < timeout)
            {
                await Task.Delay(100);
            }
            
            // Assert
            Assert.IsFalse(phasedScanService.IsScanning);
            
            // Check Phase 1 - Discovery
            var images = await _dbContext.tbl_images.ToListAsync();
            Assert.AreEqual(1, images.Count);
            
            // Check Phase 2 - Metadata
            var metadata = await _dbContext.tbl_image_metadata.ToListAsync();
            Assert.AreEqual(1, metadata.Count);
            
            // Check Phase 3 - Hashing (should have calculated hash)
            var image = images[0];
            Assert.IsNotNull(image.FileHash);
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
}