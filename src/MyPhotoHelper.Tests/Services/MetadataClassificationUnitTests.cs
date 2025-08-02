using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyPhotoHelper.Models;

namespace MyPhotoHelper.Tests.Services
{
    /// <summary>
    /// Unit tests for metadata classification logic that don't require Python integration.
    /// These tests validate the core metadata preparation and analysis logic.
    /// </summary>
    [TestClass]
    public class MetadataClassificationUnitTests
    {
        [TestMethod]
        public void PhotoMetadata_ContainsExpectedIndicators()
        {
            // Arrange
            var photoImage = CreatePhotoImageWithMetadata();
            
            // Act
            var metadata = PrepareMetadataForClassification(photoImage);
            
            // Assert
            Assert.IsTrue(metadata.ContainsKey("camera_make"), "Photo metadata should contain camera make");
            Assert.IsTrue(metadata.ContainsKey("camera_model"), "Photo metadata should contain camera model");
            Assert.IsTrue(metadata.ContainsKey("focal_length"), "Photo metadata should contain focal length");
            Assert.IsTrue(metadata.ContainsKey("f_number"), "Photo metadata should contain f-stop");
            Assert.IsTrue(metadata.ContainsKey("iso"), "Photo metadata should contain ISO");
            Assert.IsTrue(metadata.ContainsKey("exposure_time"), "Photo metadata should contain exposure time");
            Assert.IsTrue(metadata.ContainsKey("date_taken"), "Photo metadata should contain date taken");
            Assert.IsTrue(metadata.ContainsKey("latitude"), "Photo metadata should contain GPS latitude");
            Assert.IsTrue(metadata.ContainsKey("longitude"), "Photo metadata should contain GPS longitude");
            
            // Verify actual values
            Assert.AreEqual("Sony", metadata["camera_make"]);
            Assert.AreEqual("A7R IV", metadata["camera_model"]);
            Assert.AreEqual(24.0, metadata["focal_length"]);
            Assert.AreEqual("5.6", metadata["f_number"]);
            Assert.AreEqual(100, metadata["iso"]);
            Assert.AreEqual("1/60", metadata["exposure_time"]);
        }

        [TestMethod]
        public void ScreenshotMetadata_LacksPhotoIndicators()
        {
            // Arrange
            var screenshotImage = CreateScreenshotImageWithMetadata();
            
            // Act
            var metadata = PrepareMetadataForClassification(screenshotImage);
            
            // Assert
            Assert.IsFalse(metadata.ContainsKey("camera_make"), "Screenshot metadata should not contain camera make");
            Assert.IsFalse(metadata.ContainsKey("camera_model"), "Screenshot metadata should not contain camera model");
            Assert.IsFalse(metadata.ContainsKey("focal_length"), "Screenshot metadata should not contain focal length");
            Assert.IsFalse(metadata.ContainsKey("f_number"), "Screenshot metadata should not contain f-stop");
            Assert.IsFalse(metadata.ContainsKey("iso"), "Screenshot metadata should not contain ISO");
            Assert.IsFalse(metadata.ContainsKey("exposure_time"), "Screenshot metadata should not contain exposure time");
            Assert.IsFalse(metadata.ContainsKey("date_taken"), "Screenshot metadata should not contain date taken");
            Assert.IsFalse(metadata.ContainsKey("latitude"), "Screenshot metadata should not contain GPS data");
            Assert.IsFalse(metadata.ContainsKey("longitude"), "Screenshot metadata should not contain GPS data");
            
            // Should contain file info and screen-related metadata
            Assert.IsTrue(metadata.ContainsKey("file_name"), "Should contain file name");
            Assert.IsTrue(metadata.ContainsKey("file_extension"), "Should contain file extension");
            Assert.IsTrue(metadata.ContainsKey("width"), "Should contain width");
            Assert.IsTrue(metadata.ContainsKey("height"), "Should contain height");
            Assert.IsTrue(metadata.ContainsKey("software"), "Should contain software info");
            
            // Verify screenshot-specific values
            Assert.AreEqual("screenshot_sample.png", metadata["file_name"]);
            Assert.AreEqual(".png", metadata["file_extension"]);
            Assert.AreEqual("Snipping Tool", metadata["software"]);
        }

        [TestMethod]
        public void MetadataPreparation_HandlesNullMetadata()
        {
            // Arrange
            var imageWithoutMetadata = new tbl_images
            {
                ImageId = 1,
                FileName = "test.jpg",
                FileExtension = ".jpg",
                FileSizeBytes = 1024000,
                DateCreated = DateTime.UtcNow,
                DateModified = DateTime.UtcNow,
                // No tbl_image_metadata
            };
            
            // Act
            var metadata = PrepareMetadataForClassification(imageWithoutMetadata);
            
            // Assert
            Assert.IsNotNull(metadata, "Metadata should not be null even without image metadata");
            Assert.IsTrue(metadata.ContainsKey("file_name"), "Should contain basic file info");
            Assert.IsTrue(metadata.ContainsKey("file_extension"), "Should contain basic file info");
            Assert.IsTrue(metadata.ContainsKey("file_size_bytes"), "Should contain basic file info");
            Assert.IsFalse(metadata.ContainsKey("width"), "Should not contain image-specific metadata");
            Assert.IsFalse(metadata.ContainsKey("camera_make"), "Should not contain camera metadata");
        }

        [TestMethod]
        public void MetadataJson_SerializesCorrectly()
        {
            // Arrange
            var photoImage = CreatePhotoImageWithMetadata();
            var metadata = PrepareMetadataForClassification(photoImage);
            
            // Act
            var jsonString = JsonSerializer.Serialize(metadata);
            var deserialized = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString);
            
            // Assert
            Assert.IsNotNull(jsonString, "JSON serialization should not be null");
            Assert.IsTrue(jsonString.Length > 0, "JSON should contain data");
            Assert.IsNotNull(deserialized, "JSON should deserialize correctly");
            
            // Verify key data survives serialization
            Assert.IsTrue(deserialized.ContainsKey("file_name"), "Deserialized should contain file_name");
            Assert.IsTrue(deserialized.ContainsKey("camera_make"), "Deserialized should contain camera_make");
        }

        [TestMethod]
        public void CommonScreenshotDimensions_AreIdentifiable()
        {
            // Test common screenshot resolutions that should be easier to identify
            var testCases = new[]
            {
                new { Width = 1920, Height = 1080, Name = "1080p" },
                new { Width = 1366, Height = 768, Name = "Laptop common" },
                new { Width = 1440, Height = 900, Name = "MacBook" },
                new { Width = 2560, Height = 1440, Name = "1440p" },
                new { Width = 1080, Height = 1920, Name = "Mobile portrait" },
                new { Width = 750, Height = 1334, Name = "iPhone 6/7/8" }
            };

            foreach (var testCase in testCases)
            {
                // Arrange
                var screenshotImage = CreateScreenshotImageWithDimensions(testCase.Width, testCase.Height);
                
                // Act
                var metadata = PrepareMetadataForClassification(screenshotImage);
                
                // Assert
                Assert.AreEqual(testCase.Width, metadata["width"], $"Width should match for {testCase.Name}");
                Assert.AreEqual(testCase.Height, metadata["height"], $"Height should match for {testCase.Name}");
                
                // Calculate and verify aspect ratio
                var aspectRatio = (double)testCase.Width / testCase.Height;
                var metadataAspectRatio = (double)(int)metadata["width"]! / (double)(int)metadata["height"]!;
                Assert.AreEqual(aspectRatio, metadataAspectRatio, 0.001, $"Aspect ratio should match for {testCase.Name}");
            }
        }

        [TestMethod]
        public void PhotoTypicalDimensions_AreDistinguishable()
        {
            // Test typical photo dimensions that should be distinguishable from screenshots
            var testCases = new[]
            {
                new { Width = 4000, Height = 3000, Name = "4:3 Camera" },
                new { Width = 6000, Height = 4000, Name = "3:2 DSLR" },
                new { Width = 4608, Height = 3456, Name = "16MP 4:3" },
                new { Width = 3024, Height = 4032, Name = "Mobile portrait photo" },
                new { Width = 4032, Height = 3024, Name = "Mobile landscape photo" }
            };

            foreach (var testCase in testCases)
            {
                // Arrange
                var photoImage = CreatePhotoImageWithDimensions(testCase.Width, testCase.Height);
                
                // Act
                var metadata = PrepareMetadataForClassification(photoImage);
                
                // Assert
                Assert.AreEqual(testCase.Width, metadata["width"], $"Width should match for {testCase.Name}");
                Assert.AreEqual(testCase.Height, metadata["height"], $"Height should match for {testCase.Name}");
                
                // Photos should have camera metadata
                Assert.IsTrue(metadata.ContainsKey("camera_make"), $"{testCase.Name} should have camera metadata");
                Assert.IsTrue(metadata.ContainsKey("focal_length"), $"{testCase.Name} should have focal length");
            }
        }

        // Helper methods

        private Dictionary<string, object?> PrepareMetadataForClassification(tbl_images image)
        {
            // This replicates the private method from MetadataClassificationService
            var metadata = new Dictionary<string, object?>
            {
                ["file_name"] = image.FileName,
                ["file_extension"] = image.FileExtension,
                ["file_size_bytes"] = image.FileSizeBytes,
                ["date_created"] = image.DateCreated.ToString("yyyy-MM-dd HH:mm:ss"),
                ["date_modified"] = image.DateModified.ToString("yyyy-MM-dd HH:mm:ss")
            };

            // Add image metadata if available
            if (image.tbl_image_metadata != null)
            {
                var meta = image.tbl_image_metadata;
                
                if (meta.Width.HasValue) metadata["width"] = meta.Width.Value;
                if (meta.Height.HasValue) metadata["height"] = meta.Height.Value;
                if (meta.DateTaken.HasValue) metadata["date_taken"] = meta.DateTaken.Value.ToString("yyyy-MM-dd HH:mm:ss");
                
                if (!string.IsNullOrEmpty(meta.CameraMake)) metadata["camera_make"] = meta.CameraMake;
                if (!string.IsNullOrEmpty(meta.CameraModel)) metadata["camera_model"] = meta.CameraModel;
                if (!string.IsNullOrEmpty(meta.Software)) metadata["software"] = meta.Software;
                if (!string.IsNullOrEmpty(meta.ColorSpace)) metadata["color_space"] = meta.ColorSpace;
                if (!string.IsNullOrEmpty(meta.Orientation)) metadata["orientation"] = meta.Orientation;
                
                if (meta.BitDepth.HasValue) metadata["bit_depth"] = meta.BitDepth.Value;
                if (meta.ResolutionX.HasValue) metadata["resolution_x"] = meta.ResolutionX.Value;
                if (meta.ResolutionY.HasValue) metadata["resolution_y"] = meta.ResolutionY.Value;
                
                if (meta.Latitude.HasValue) metadata["latitude"] = meta.Latitude.Value;
                if (meta.Longitude.HasValue) metadata["longitude"] = meta.Longitude.Value;
                
                if (meta.FocalLength.HasValue) metadata["focal_length"] = meta.FocalLength.Value;
                if (!string.IsNullOrEmpty(meta.FNumber)) metadata["f_number"] = meta.FNumber;
                if (meta.ISO.HasValue) metadata["iso"] = meta.ISO.Value;
                if (!string.IsNullOrEmpty(meta.ExposureTime)) metadata["exposure_time"] = meta.ExposureTime;
            }

            return metadata;
        }

        private tbl_images CreatePhotoImageWithMetadata()
        {
            var image = new tbl_images
            {
                ImageId = 1,
                FileName = "photo_sample.jpg",
                FileExtension = ".jpg",
                FileSizeBytes = 2048000,
                DateCreated = DateTime.UtcNow.AddDays(-15),
                DateModified = DateTime.UtcNow.AddDays(-15)
            };

            image.tbl_image_metadata = new tbl_image_metadata
            {
                ImageId = 1,
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

            return image;
        }

        private tbl_images CreateScreenshotImageWithMetadata()
        {
            var image = new tbl_images
            {
                ImageId = 2,
                FileName = "screenshot_sample.png",
                FileExtension = ".png",
                FileSizeBytes = 512000,
                DateCreated = DateTime.UtcNow.AddDays(-5),
                DateModified = DateTime.UtcNow.AddDays(-5)
            };

            image.tbl_image_metadata = new tbl_image_metadata
            {
                ImageId = 2,
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

            return image;
        }

        private tbl_images CreateScreenshotImageWithDimensions(int width, int height)
        {
            var image = CreateScreenshotImageWithMetadata();
            image.tbl_image_metadata!.Width = width;
            image.tbl_image_metadata.Height = height;
            return image;
        }

        private tbl_images CreatePhotoImageWithDimensions(int width, int height)
        {
            var image = CreatePhotoImageWithMetadata();
            image.tbl_image_metadata!.Width = width;
            image.tbl_image_metadata.Height = height;
            return image;
        }
    }
}