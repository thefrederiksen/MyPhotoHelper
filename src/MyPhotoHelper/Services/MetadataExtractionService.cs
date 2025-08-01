using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyPhotoHelper.Data;
using MyPhotoHelper.Models;
using CSnakes.Runtime;

namespace MyPhotoHelper.Services
{
    public interface IMetadataExtractionService
    {
        Task ExtractMetadataForNewImagesAsync(CancellationToken cancellationToken = default);
        Task ExtractMetadataForNewImagesAsync(IProgress<PhaseProgress>? progress, CancellationToken cancellationToken = default);
        Task<tbl_image_metadata?> ExtractMetadataAsync(tbl_images image, CancellationToken cancellationToken = default);
        Task RescanAllMetadataAsync(IProgress<PhaseProgress>? progress = null, CancellationToken cancellationToken = default);
    }

    public class MetadataExtractionService : IMetadataExtractionService
    {
        private readonly ILogger<MetadataExtractionService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IPhotoPathService _photoPathService;
        private readonly IPythonEnvironment? _pythonEnv;


        public MetadataExtractionService(
            ILogger<MetadataExtractionService> logger,
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

        public async Task ExtractMetadataForNewImagesAsync(CancellationToken cancellationToken = default)
        {
            await ExtractMetadataForNewImagesAsync(null, cancellationToken);
        }

        public async Task ExtractMetadataForNewImagesAsync(IProgress<PhaseProgress>? progress, CancellationToken cancellationToken = default)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MyPhotoHelperDbContext>();

            // Get total count of non-deleted images
            var totalImages = await dbContext.tbl_images
                .Where(img => img.FileExists == 1 && img.IsDeleted == 0)
                .CountAsync(cancellationToken);

            // Get initial count of images that already have metadata
            var initialImagesWithMetadata = await dbContext.tbl_images
                .Where(img => img.FileExists == 1 && img.IsDeleted == 0)
                .Where(img => dbContext.tbl_image_metadata.Any(meta => meta.ImageId == img.ImageId))
                .CountAsync(cancellationToken);

            _logger.LogInformation($"Metadata extraction starting: {initialImagesWithMetadata}/{totalImages} already have metadata");

            var phaseProgress = new PhaseProgress
            {
                Phase = ScanPhase.Phase2_Metadata,
                TotalItems = totalImages,
                ProcessedItems = initialImagesWithMetadata,
                SuccessCount = 0,
                ErrorCount = 0,
                StartTime = DateTime.UtcNow
            };

            progress?.Report(phaseProgress);

            var totalProcessedCount = 0;
            var totalErrorCount = 0;
            var batchSize = 1000;
            var hasMoreImages = true;
            
            // Throttle progress reporting to improve performance
            var lastProgressReport = DateTime.UtcNow;
            var progressReportInterval = TimeSpan.FromMilliseconds(500); // Report at most every 500ms
            var progressUpdateCounter = 0;

            // Process all images in batches
            while (hasMoreImages && !cancellationToken.IsCancellationRequested)
            {
                // Get next batch of images without metadata
                var imagesWithoutMetadata = await dbContext.tbl_images
                    .Where(img => img.FileExists == 1 && img.IsDeleted == 0)
                    .Where(img => !dbContext.tbl_image_metadata.Any(meta => meta.ImageId == img.ImageId))
                    .Take(batchSize)
                    .ToListAsync(cancellationToken);

                if (imagesWithoutMetadata.Count == 0)
                {
                    hasMoreImages = false;
                    break;
                }

                _logger.LogInformation($"Processing batch of {imagesWithoutMetadata.Count} images");

                foreach (var image in imagesWithoutMetadata)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    phaseProgress.CurrentItem = image.FileName;
                    
                    // Only report progress periodically to reduce UI overhead
                    progressUpdateCounter++;
                    var now = DateTime.UtcNow;
                    if (progressUpdateCounter % 10 == 0 || now - lastProgressReport >= progressReportInterval)
                    {
                        lastProgressReport = now;
                        progress?.Report(phaseProgress);
                    }

                    try
                    {
                        var metadata = await ExtractMetadataAsync(image, cancellationToken);
                        if (metadata != null)
                        {
                            dbContext.tbl_image_metadata.Add(metadata);
                            totalProcessedCount++;
                            phaseProgress.SuccessCount = totalProcessedCount;

                            // Save every 50 images
                            if (totalProcessedCount % 50 == 0)
                            {
                                await dbContext.SaveChangesAsync(cancellationToken);
                                _logger.LogDebug($"Saved metadata for {totalProcessedCount} images");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error extracting metadata for image {image.ImageId}: {image.FileName}");
                        totalErrorCount++;
                        phaseProgress.ErrorCount = totalErrorCount;
                    }

                    phaseProgress.ProcessedItems = initialImagesWithMetadata + totalProcessedCount + totalErrorCount;
                }

                // Save any remaining in this batch
                await dbContext.SaveChangesAsync(cancellationToken);
                
                // Report progress after batch completion
                progress?.Report(phaseProgress);

                // Check if we've processed fewer images than the batch size
                if (imagesWithoutMetadata.Count < batchSize)
                {
                    hasMoreImages = false;
                }
            }

            phaseProgress.EndTime = DateTime.UtcNow;
            progress?.Report(phaseProgress);

            _logger.LogInformation($"Metadata extraction completed. Processed: {totalProcessedCount}, Errors: {totalErrorCount}");
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

                // Use Python for all image metadata extraction
                if (_pythonEnv != null)
                {
                    try
                    {
                        _logger.LogInformation($"Using Python metadata extraction for: {image.FileName}");
                        
                        // Call Python metadata_extractor module
                        var pythonResult = _pythonEnv.MetadataExtractor().ExtractImageMetadata(fullPath);
                        
                        if (pythonResult != null)
                        {
                            // Helper method to safely extract values from Python result
                            T? GetValue<T>(string key, Func<string, T> converter) where T : struct
                            {
                                if (pythonResult.ContainsKey(key) && pythonResult[key] != null && pythonResult[key].ToString() != "None")
                                {
                                    try { return converter(pythonResult[key].ToString()!); }
                                    catch { return null; }
                                }
                                return null;
                            }
                            
                            string? GetString(string key)
                            {
                                if (pythonResult.ContainsKey(key) && pythonResult[key] != null && pythonResult[key].ToString() != "None")
                                {
                                    var value = pythonResult[key].ToString();
                                    return string.IsNullOrEmpty(value) ? null : value;
                                }
                                return null;
                            }
                            
                            DateTime? GetDateTime(string key)
                            {
                                var dateStr = GetString(key);
                                if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var parsedDate))
                                {
                                    return parsedDate;
                                }
                                return null;
                            }
                            
                            var metadata = new tbl_image_metadata
                            {
                                ImageId = image.ImageId,
                                
                                // Basic Image Properties
                                Width = GetValue("width", int.Parse),
                                Height = GetValue("height", int.Parse),
                                ColorSpace = GetString("color_space"),
                                BitDepth = GetValue("bit_depth", int.Parse),
                                Orientation = GetString("orientation"),
                                ResolutionX = GetValue("resolution_x", double.Parse),
                                ResolutionY = GetValue("resolution_y", double.Parse),
                                ResolutionUnit = GetString("resolution_unit"),
                                
                                // Date/Time Information
                                DateTaken = GetDateTime("date_taken") ?? image.DateModified,
                                DateDigitized = GetDateTime("date_digitized"),
                                DateModified = GetDateTime("date_modified"),
                                TimeZone = GetString("time_zone"),
                                
                                // GPS/Location Data
                                Latitude = GetValue("latitude", double.Parse),
                                Longitude = GetValue("longitude", double.Parse),
                                Altitude = GetValue("altitude", double.Parse),
                                GPSDirection = GetString("gps_direction"),
                                GPSSpeed = GetValue("gps_speed", double.Parse),
                                GPSProcessingMethod = GetString("gps_processing_method"),
                                LocationName = GetString("location_name"),
                                
                                // Camera Information
                                CameraMake = GetString("camera_make"),
                                CameraModel = GetString("camera_model"),
                                CameraSerial = GetString("camera_serial"),
                                LensModel = GetString("lens_model"),
                                LensMake = GetString("lens_make"),
                                LensSerial = GetString("lens_serial"),
                                
                                // Camera Settings
                                FocalLength = GetValue("focal_length", double.Parse),
                                FocalLength35mm = GetValue("focal_length_35mm", double.Parse),
                                FNumber = GetString("f_number"),
                                ExposureTime = GetString("exposure_time"),
                                ISO = GetValue("iso", int.Parse),
                                ExposureMode = GetString("exposure_mode"),
                                ExposureProgram = GetString("exposure_program"),
                                MeteringMode = GetString("metering_mode"),
                                Flash = GetString("flash"),
                                WhiteBalance = GetString("white_balance"),
                                SceneCaptureType = GetString("scene_capture_type"),
                                
                                // Software/Processing
                                Software = GetString("software"),
                                ProcessingSoftware = GetString("processing_software"),
                                Artist = GetString("artist"),
                                Copyright = GetString("copyright"),
                                
                                // Technical Details
                                ColorProfile = GetString("color_profile"),
                                ExposureBias = GetValue("exposure_bias", double.Parse),
                                MaxAperture = GetValue("max_aperture", double.Parse),
                                SubjectDistance = GetString("subject_distance"),
                                LightSource = GetString("light_source"),
                                SensingMethod = GetString("sensing_method"),
                                FileSource = GetString("file_source"),
                                SceneType = GetString("scene_type"),
                                
                                // Additional Properties
                                ImageDescription = GetString("image_description"),
                                UserComment = GetString("user_comment"),
                                Keywords = GetString("keywords"),
                                Subject = GetString("subject")
                            };
                            
                            var hasGps = metadata.Latitude.HasValue && metadata.Longitude.HasValue;
                            _logger.LogInformation($"Python extracted metadata for {image.FileName} - GPS: {(hasGps ? $"{metadata.Latitude}, {metadata.Longitude}" : "None")}");
                            return metadata;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Python metadata extraction failed for {image.FileName} (ID: {image.ImageId}): {ex.Message}");
                        _logger.LogError($"  File path: {fullPath}");
                        _logger.LogError($"  File exists: {System.IO.File.Exists(fullPath)}");
                        _logger.LogError($"  Error type: {ex.GetType().Name}");
                        if (ex.InnerException != null)
                        {
                            _logger.LogError($"  Inner exception: {ex.InnerException.Message}");
                        }
                    }
                }
                
                // Fallback to basic metadata if Python fails
                _logger.LogWarning($"Python metadata extraction failed for {image.FileName}. Using basic fallback.");
                return new tbl_image_metadata
                {
                    ImageId = image.ImageId,
                    Width = 0,
                    Height = 0,
                    DateTaken = image.DateModified,
                    Latitude = null,
                    Longitude = null,
                    LocationName = null
                };
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
                    DateTaken = image.DateModified,
                    Latitude = null,
                    Longitude = null,
                    LocationName = null
                };
            }
        }

        public async Task RescanAllMetadataAsync(IProgress<PhaseProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MyPhotoHelperDbContext>();

            _logger.LogInformation("Starting rescan of all image metadata");

            try
            {
                // First, delete all existing metadata
                _logger.LogInformation("Clearing existing image metadata");
                await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM tbl_image_metadata", cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Existing metadata cleared");

                // Then extract metadata for all images
                await ExtractMetadataForNewImagesAsync(progress, cancellationToken);
                
                _logger.LogInformation("Metadata rescan completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during metadata rescan");
                throw;
            }
        }

        // Note: All metadata extraction is now handled by Python for consistency and HEIC support
    }
}