using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyPhotoHelper.Data;
using MyPhotoHelper.Models;

namespace MyPhotoHelper.Services
{
    public interface IMetadataExtractionService
    {
        Task ExtractMetadataForNewImagesAsync(CancellationToken cancellationToken = default);
        Task<tbl_image_metadata?> ExtractMetadataAsync(tbl_images image, CancellationToken cancellationToken = default);
    }

    public class MetadataExtractionService : IMetadataExtractionService
    {
        private readonly ILogger<MetadataExtractionService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IPhotoPathService _photoPathService;

        // EXIF tag IDs for date/time
        private const int PropertyTagDateTime = 0x0132;
        private const int PropertyTagDateTimeOriginal = 0x9003;
        private const int PropertyTagDateTimeDigitized = 0x9004;

        public MetadataExtractionService(
            ILogger<MetadataExtractionService> logger,
            IServiceProvider serviceProvider,
            IPhotoPathService photoPathService)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _photoPathService = photoPathService;
        }

        public async Task ExtractMetadataForNewImagesAsync(CancellationToken cancellationToken = default)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MyPhotoHelperDbContext>();

            // Get images without metadata
            var imagesWithoutMetadata = await dbContext.tbl_images
                .Where(img => img.FileExists == 1 && img.IsDeleted == 0)
                .Where(img => !dbContext.tbl_image_metadata.Any(meta => meta.ImageId == img.ImageId))
                .Take(1000) // Process in batches
                .ToListAsync(cancellationToken);

            _logger.LogInformation($"Found {imagesWithoutMetadata.Count} images without metadata");

            var processedCount = 0;
            var errorCount = 0;

            foreach (var image in imagesWithoutMetadata)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    var metadata = await ExtractMetadataAsync(image, cancellationToken);
                    if (metadata != null)
                    {
                        dbContext.tbl_image_metadata.Add(metadata);
                        processedCount++;

                        // Save every 50 images
                        if (processedCount % 50 == 0)
                        {
                            await dbContext.SaveChangesAsync(cancellationToken);
                            _logger.LogDebug($"Saved metadata for {processedCount} images");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error extracting metadata for image {image.ImageId}: {image.FileName}");
                    errorCount++;
                }
            }

            // Save any remaining
            if (processedCount % 50 != 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation($"Metadata extraction completed. Processed: {processedCount}, Errors: {errorCount}");
        }

        public async Task<tbl_image_metadata?> ExtractMetadataAsync(tbl_images image, CancellationToken cancellationToken = default)
        {
            try
            {
                var fullPath = await _photoPathService.GetFullPathForImageAsync(image);
                if (string.IsNullOrEmpty(fullPath) || !System.IO.File.Exists(fullPath))
                {
                    _logger.LogWarning($"File not found: {fullPath}");
                    return null;
                }

                using var fileStream = System.IO.File.OpenRead(fullPath);
                using var img = Image.FromStream(fileStream, false, false);

                var metadata = new tbl_image_metadata
                {
                    ImageId = image.ImageId,
                    Width = img.Width,
                    Height = img.Height,
                    DateTaken = GetDateTaken(img) ?? image.DateModified // Fallback to file modified date
                };

                return metadata;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to extract metadata from {image.FileName}");
                
                // Return basic metadata with file dates as fallback
                return new tbl_image_metadata
                {
                    ImageId = image.ImageId,
                    Width = 0,
                    Height = 0,
                    DateTaken = image.DateModified
                };
            }
        }

        private DateTime? GetDateTaken(Image image)
        {
            try
            {
                // Try to get EXIF DateTimeOriginal first
                if (image.PropertyIdList.Contains(PropertyTagDateTimeOriginal))
                {
                    var prop = image.GetPropertyItem(PropertyTagDateTimeOriginal);
                    if (prop != null)
                    {
                        var dateTaken = ParseExifDate(prop);
                        if (dateTaken.HasValue)
                            return dateTaken;
                    }
                }

                // Try DateTimeDigitized
                if (image.PropertyIdList.Contains(PropertyTagDateTimeDigitized))
                {
                    var prop = image.GetPropertyItem(PropertyTagDateTimeDigitized);
                    if (prop != null)
                    {
                        var dateTaken = ParseExifDate(prop);
                        if (dateTaken.HasValue)
                            return dateTaken;
                    }
                }

                // Try DateTime (last modified in camera)
                if (image.PropertyIdList.Contains(PropertyTagDateTime))
                {
                    var prop = image.GetPropertyItem(PropertyTagDateTime);
                    if (prop != null)
                    {
                        var dateTaken = ParseExifDate(prop);
                        if (dateTaken.HasValue)
                            return dateTaken;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Could not extract EXIF date: {ex.Message}");
            }

            return null;
        }

        private DateTime? ParseExifDate(PropertyItem prop)
        {
            try
            {
                if (prop.Value == null || prop.Value.Length == 0)
                    return null;
                    
                var dateString = Encoding.ASCII.GetString(prop.Value).TrimEnd('\0');
                
                // EXIF date format: "yyyy:MM:dd HH:mm:ss"
                if (DateTime.TryParseExact(dateString, "yyyy:MM:dd HH:mm:ss", 
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    return date;
                }
            }
            catch
            {
                // Ignore parsing errors
            }

            return null;
        }
    }
}