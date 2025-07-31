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

            // Get count of images that already have metadata
            var imagesWithMetadata = await dbContext.tbl_images
                .Where(img => img.FileExists == 1 && img.IsDeleted == 0)
                .Where(img => dbContext.tbl_image_metadata.Any(meta => meta.ImageId == img.ImageId))
                .CountAsync(cancellationToken);

            // Get images without metadata
            var imagesWithoutMetadata = await dbContext.tbl_images
                .Where(img => img.FileExists == 1 && img.IsDeleted == 0)
                .Where(img => !dbContext.tbl_image_metadata.Any(meta => meta.ImageId == img.ImageId))
                .Take(1000) // Process in batches
                .ToListAsync(cancellationToken);

            _logger.LogInformation($"Metadata extraction: {imagesWithMetadata} completed, {imagesWithoutMetadata.Count} remaining, {totalImages} total");

            var phaseProgress = new PhaseProgress
            {
                Phase = ScanPhase.Phase3_Metadata,
                TotalItems = totalImages,
                ProcessedItems = imagesWithMetadata,
                SuccessCount = 0,
                ErrorCount = 0,
                StartTime = DateTime.UtcNow
            };

            progress?.Report(phaseProgress);

            var processedCount = 0;
            var errorCount = 0;
            var currentIndex = 0;

            foreach (var image in imagesWithoutMetadata)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                currentIndex++;

                phaseProgress.CurrentItem = image.FileName;
                progress?.Report(phaseProgress);

                try
                {
                    var metadata = await ExtractMetadataAsync(image, cancellationToken);
                    if (metadata != null)
                    {
                        dbContext.tbl_image_metadata.Add(metadata);
                        processedCount++;
                        phaseProgress.SuccessCount = processedCount;

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
                    phaseProgress.ErrorCount = errorCount;
                }

                phaseProgress.ProcessedItems = imagesWithMetadata + currentIndex;
                progress?.Report(phaseProgress);
            }

            // Save any remaining
            if (processedCount % 50 != 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            phaseProgress.EndTime = DateTime.UtcNow;
            progress?.Report(phaseProgress);

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
                            // Convert Python dictionary to C# objects
                            var width = pythonResult.ContainsKey("width") ? int.Parse(pythonResult["width"].ToString()!) : 0;
                            var height = pythonResult.ContainsKey("height") ? int.Parse(pythonResult["height"].ToString()!) : 0;
                            var dateTakenStr = pythonResult.ContainsKey("date_taken") && pythonResult["date_taken"] != null 
                                && pythonResult["date_taken"].ToString() != "None"
                                ? pythonResult["date_taken"].ToString() : null;
                            var pythonLat = pythonResult.ContainsKey("latitude") && pythonResult["latitude"] != null 
                                && pythonResult["latitude"].ToString() != "None"
                                ? double.Parse(pythonResult["latitude"].ToString()!) : (double?)null;
                            var pythonLon = pythonResult.ContainsKey("longitude") && pythonResult["longitude"] != null 
                                && pythonResult["longitude"].ToString() != "None"
                                ? double.Parse(pythonResult["longitude"].ToString()!) : (double?)null;
                            
                            // Parse date if available
                            DateTime? dateTaken = null;
                            if (!string.IsNullOrEmpty(dateTakenStr))
                            {
                                if (DateTime.TryParse(dateTakenStr, out var parsedDate))
                                    dateTaken = parsedDate;
                            }
                            
                            var metadata = new tbl_image_metadata
                            {
                                ImageId = image.ImageId,
                                Width = width,
                                Height = height,
                                DateTaken = dateTaken ?? image.DateModified,
                                Latitude = pythonLat,
                                Longitude = pythonLon,
                                LocationName = null
                            };
                            
                            _logger.LogInformation($"Python extracted metadata - GPS: {metadata.Latitude}, {metadata.Longitude}");
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