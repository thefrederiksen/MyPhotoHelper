using System.Text.Json;
using CSnakes.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyPhotoHelper.Data;
using MyPhotoHelper.Models;

namespace MyPhotoHelper.Services
{
    public interface IMetadataClassificationService
    {
        Task<MetadataClassificationResult> ClassifyImageAsync(tbl_images image, string apiKey, string model = "gpt-4o-mini");
        Task<List<MetadataClassificationResult>> ClassifyBatchAsync(List<tbl_images> images, string apiKey, int batchSize = 10, string model = "gpt-4o-mini");
        Task<List<MetadataClassificationResult>> GetUnknownImagesForClassificationAsync(int limit = 100);
    }

    public class MetadataClassificationResult
    {
        public int ImageId { get; set; }
        public string FileName { get; set; } = "";
        public string Category { get; set; } = "unknown";
        public double Confidence { get; set; }
        public string Reasoning { get; set; } = "";
        public bool HasError { get; set; }
        public string ErrorMessage { get; set; } = "";
        public DateTime ClassifiedAt { get; set; } = DateTime.UtcNow;
    }

    public class MetadataClassificationService : IMetadataClassificationService
    {
        private readonly ILogger<MetadataClassificationService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IPythonEnvironment? _pythonEnv;

        public MetadataClassificationService(
            ILogger<MetadataClassificationService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;

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

        public async Task<MetadataClassificationResult> ClassifyImageAsync(tbl_images image, string apiKey, string model = "gpt-4o-mini")
        {
            try
            {
                // Prepare metadata for classification
                var metadata = PrepareMetadataForClassification(image);
                var metadataJson = JsonSerializer.Serialize(metadata);

                _logger.LogInformation($"Classifying image {image.ImageId} ({image.FileName}) using LLM metadata analysis");

                string resultJson;
                
                // Try Python/OpenAI first, fall back to rule-based if not available
                if (_pythonEnv != null && !string.IsNullOrEmpty(apiKey))
                {
                    try
                    {
                        // Use the Python module for LLM classification
                        resultJson = await Task.Run(() => 
                        {
                            return _pythonEnv.ImageAnalysisModule().ClassifyImageMetadataSingle(apiKey, metadataJson, model);
                        });
                        
                        _logger.LogInformation($"LLM classification successful for image {image.ImageId}");
                    }
                    catch (Exception pythonEx)
                    {
                        _logger.LogWarning(pythonEx, $"Python/LLM classification failed for image {image.ImageId}, falling back to rule-based");
                        
                        // Fall back to rule-based classification
                        var fallbackResult = SimulateClassification(metadata, image.FileName);
                        resultJson = JsonSerializer.Serialize(new
                        {
                            category = fallbackResult.Category,
                            confidence = fallbackResult.Confidence,
                            reasoning = $"[FALLBACK] {fallbackResult.Reasoning}"
                        });
                    }
                }
                else
                {
                    _logger.LogInformation($"No Python environment or API key, using rule-based classification for image {image.ImageId}");
                    
                    // Use rule-based classification
                    var ruleResult = SimulateClassification(metadata, image.FileName);
                    resultJson = JsonSerializer.Serialize(new
                    {
                        category = ruleResult.Category,
                        confidence = ruleResult.Confidence,
                        reasoning = $"[RULE-BASED] {ruleResult.Reasoning}"
                    });
                }

                // Parse the result
                var resultData = JsonSerializer.Deserialize<JsonElement>(resultJson);
                
                var result = new MetadataClassificationResult
                {
                    ImageId = image.ImageId,
                    FileName = image.FileName,
                    Category = resultData.GetProperty("category").GetString() ?? "unknown",
                    Confidence = resultData.TryGetProperty("confidence", out var conf) ? conf.GetDouble() : 0.0,
                    Reasoning = resultData.TryGetProperty("reasoning", out var reason) ? reason.GetString() ?? "" : "",
                    HasError = resultData.TryGetProperty("error", out var error) && error.GetBoolean(),
                    ClassifiedAt = DateTime.UtcNow
                };

                if (result.HasError)
                {
                    result.ErrorMessage = result.Reasoning;
                    _logger.LogWarning($"Classification error for image {image.ImageId}: {result.ErrorMessage}");
                }
                else
                {
                    _logger.LogInformation($"Classified image {image.ImageId} as '{result.Category}' with confidence {result.Confidence:F2}");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error classifying image {image.ImageId}");
                return new MetadataClassificationResult
                {
                    ImageId = image.ImageId,
                    FileName = image.FileName,
                    Category = "unknown",
                    Confidence = 0.0,
                    HasError = true,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<List<MetadataClassificationResult>> ClassifyBatchAsync(List<tbl_images> images, string apiKey, int batchSize = 10, string model = "gpt-4o-mini")
        {
            var results = new List<MetadataClassificationResult>();

            try
            {
                // Process in batches to avoid token limits
                for (int i = 0; i < images.Count; i += batchSize)
                {
                    var batch = images.Skip(i).Take(batchSize).ToList();
                    _logger.LogInformation($"Processing batch {i / batchSize + 1}: {batch.Count} images");

                    // Try Python/OpenAI batch processing first
                    if (_pythonEnv != null && !string.IsNullOrEmpty(apiKey))
                    {
                        try
                        {
                            // Prepare metadata for all images in batch
                            var metadataList = batch.Select(PrepareMetadataForClassification).ToList();
                            var metadataListJson = JsonSerializer.Serialize(metadataList);

                            // Use the Python module for LLM batch classification
                            var batchResultJson = await Task.Run(() => 
                            {
                                return _pythonEnv.ImageAnalysisModule().ClassifyImageMetadataBatch(apiKey, metadataListJson, model);
                            });

                            // Parse batch results
                            var batchResults = JsonSerializer.Deserialize<JsonElement[]>(batchResultJson);
                            
                            if (batchResults != null)
                            {
                                for (int j = 0; j < batch.Count && j < batchResults.Length; j++)
                                {
                                var resultData = batchResults[j];
                                var result = new MetadataClassificationResult
                                {
                                    ImageId = batch[j].ImageId,
                                    FileName = batch[j].FileName,
                                    Category = resultData.GetProperty("category").GetString() ?? "unknown",
                                    Confidence = resultData.TryGetProperty("confidence", out var conf) ? conf.GetDouble() : 0.0,
                                    Reasoning = resultData.TryGetProperty("reasoning", out var reason) ? reason.GetString() ?? "" : "",
                                    HasError = resultData.TryGetProperty("error", out var error) && error.GetBoolean(),
                                    ClassifiedAt = DateTime.UtcNow
                                };
                                    results.Add(result);
                                }
                            }
                            
                            _logger.LogInformation($"LLM batch classification successful for {batch.Count} images");
                        }
                        catch (Exception pythonEx)
                        {
                            _logger.LogWarning(pythonEx, $"Python/LLM batch classification failed, falling back to rule-based for {batch.Count} images");
                            
                            // Fall back to individual rule-based classification
                            foreach (var image in batch)
                            {
                                var metadata = PrepareMetadataForClassification(image);
                                var fallbackResult = SimulateClassification(metadata, image.FileName);
                                
                                var result = new MetadataClassificationResult
                                {
                                    ImageId = image.ImageId,
                                    FileName = image.FileName,
                                    Category = fallbackResult.Category,
                                    Confidence = fallbackResult.Confidence,
                                    Reasoning = $"[FALLBACK] {fallbackResult.Reasoning}",
                                    HasError = false,
                                    ClassifiedAt = DateTime.UtcNow
                                };
                                results.Add(result);
                            }
                        }
                    }
                    else
                    {
                        _logger.LogInformation($"No Python environment or API key, using rule-based classification for batch of {batch.Count} images");
                        
                        // Use rule-based classification for each image
                        foreach (var image in batch)
                        {
                            var metadata = PrepareMetadataForClassification(image);
                            var ruleResult = SimulateClassification(metadata, image.FileName);
                            
                            var result = new MetadataClassificationResult
                            {
                                ImageId = image.ImageId,
                                FileName = image.FileName,
                                Category = ruleResult.Category,
                                Confidence = ruleResult.Confidence,
                                Reasoning = $"[RULE-BASED] {ruleResult.Reasoning}",
                                HasError = false,
                                ClassifiedAt = DateTime.UtcNow
                            };
                            results.Add(result);
                        }
                    }

                    // Small delay between batches to be respectful to the API
                    if (i + batchSize < images.Count)
                    {
                        await Task.Delay(1000);
                    }
                }

                _logger.LogInformation($"Completed batch classification: {results.Count} images processed");
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in batch classification");
                
                // Return error results for all images that weren't processed
                foreach (var image in images.Skip(results.Count))
                {
                    results.Add(new MetadataClassificationResult
                    {
                        ImageId = image.ImageId,
                        FileName = image.FileName,
                        Category = "unknown",
                        Confidence = 0.0,
                        HasError = true,
                        ErrorMessage = ex.Message
                    });
                }

                return results;
            }
        }

        public async Task<List<MetadataClassificationResult>> GetUnknownImagesForClassificationAsync(int limit = 100)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MyPhotoHelperDbContext>();

            // Get images that are unknown or have no analysis
            var unknownImages = await dbContext.tbl_images
                .Where(img => img.FileExists == 1 && img.IsDeleted == 0)
                .Where(img => 
                    !dbContext.tbl_image_analysis.Any(a => a.ImageId == img.ImageId) ||
                    dbContext.tbl_image_analysis.Any(a => a.ImageId == img.ImageId && 
                        (a.ImageCategory == null || a.ImageCategory == "unknown"))
                )
                .Include(img => img.tbl_image_metadata)
                .Take(limit)
                .ToListAsync();

            return unknownImages.Select(img => new MetadataClassificationResult
            {
                ImageId = img.ImageId,
                FileName = img.FileName,
                Category = "unknown",
                Confidence = 0.0,
                Reasoning = "Awaiting classification"
            }).ToList();
        }

        private Dictionary<string, object?> PrepareMetadataForClassification(tbl_images image)
        {
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

        private (string Category, double Confidence, string Reasoning) SimulateClassification(Dictionary<string, object?> metadata, string fileName)
        {
            // Extract metadata values
            var hasCamera = metadata.ContainsKey("camera_make") || metadata.ContainsKey("camera_model");
            var hasCameraSettings = metadata.ContainsKey("focal_length") || metadata.ContainsKey("f_number") || metadata.ContainsKey("iso");
            var hasGPS = metadata.ContainsKey("latitude") && metadata.ContainsKey("longitude");
            var software = metadata.GetValueOrDefault("software")?.ToString() ?? "";
            var width = metadata.GetValueOrDefault("width") as int? ?? 0;
            var height = metadata.GetValueOrDefault("height") as int? ?? 0;
            var fileExtension = metadata.GetValueOrDefault("file_extension")?.ToString()?.ToLower() ?? "";
            var fileSize = metadata.GetValueOrDefault("file_size_bytes") as long? ?? 0;

            var reasoning = new List<string>();
            
            // Screenshot software detection (highest confidence)
            var screenshotSoftware = new[] { "snipping", "screenshot", "capture", "grab", "snagit", "greenshot", "lightshot", "gyazo" };
            var hasScreenshotSoftware = screenshotSoftware.Any(s => software.ToLower().Contains(s));
            
            if (hasScreenshotSoftware)
            {
                return ("screenshot", 0.95, $"Strong indicator: Software '{software}' is a known screenshot capture tool");
            }

            // Phone/tablet screenshot patterns
            var mobileScreenResolutions = new[] 
            {
                (1080, 1920), (1125, 2436), (1170, 2532), (1284, 2778), // iPhone
                (1440, 2960), (1080, 2340), (1080, 2400), // Android
                (750, 1334), (828, 1792), (1242, 2688) // iPhone older
            };
            
            var isExactMobileScreenRes = mobileScreenResolutions.Contains((width, height));
            if (isExactMobileScreenRes && !hasCamera && !hasCameraSettings)
            {
                return ("screenshot", 0.90, $"Strong indicator: Exact mobile screen resolution {width}x{height} with no camera metadata suggests mobile screenshot");
            }

            // Desktop screenshot patterns
            var desktopScreenResolutions = new[] 
            {
                (1920, 1080), (1366, 768), (1440, 900), (2560, 1440), 
                (1680, 1050), (1600, 900), (3840, 2160), (2560, 1600)
            };
            
            var isExactDesktopRes = desktopScreenResolutions.Contains((width, height));
            if (isExactDesktopRes && !hasCamera && !hasCameraSettings && !hasGPS)
            {
                return ("screenshot", 0.85, $"Likely indicator: Desktop resolution {width}x{height} with no camera metadata or GPS suggests desktop screenshot");
            }

            // Strong camera indicators (highest confidence for photos)
            if (hasCamera && hasCameraSettings)
            {
                var cameraInfo = $"{metadata.GetValueOrDefault("camera_make")} {metadata.GetValueOrDefault("camera_model")}".Trim();
                var settings = new List<string>();
                if (metadata.ContainsKey("focal_length")) settings.Add($"{metadata["focal_length"]}mm");
                if (metadata.ContainsKey("f_number")) settings.Add($"f/{metadata["f_number"]}");
                if (metadata.ContainsKey("iso")) settings.Add($"ISO {metadata["iso"]}");
                
                var confidence = hasGPS ? 0.95 : 0.90;
                var gpsInfo = hasGPS ? " + GPS location data" : "";
                reasoning.Add($"Camera: {cameraInfo}");
                reasoning.Add($"Settings: {string.Join(", ", settings)}");
                if (hasGPS) reasoning.Add("GPS coordinates present");
                
                return ("photo", confidence, $"Strong photo indicators: {string.Join(" | ", reasoning)}");
            }

            // Moderate camera indicators
            if (hasCamera && !hasCameraSettings)
            {
                var cameraInfo = $"{metadata.GetValueOrDefault("camera_make")} {metadata.GetValueOrDefault("camera_model")}".Trim();
                var confidence = hasGPS ? 0.80 : 0.70;
                reasoning.Add($"Camera: {cameraInfo}");
                if (hasGPS) reasoning.Add("GPS coordinates");
                reasoning.Add("Missing detailed camera settings");
                
                return ("photo", confidence, $"Moderate photo indicators: {string.Join(" | ", reasoning)}");
            }

            // File format analysis
            var photoFormats = new[] { ".jpg", ".jpeg", ".tiff", ".tif", ".raw", ".cr2", ".nef", ".arw" };
            var screenshotFormats = new[] { ".png", ".bmp" };
            
            if (photoFormats.Contains(fileExtension))
            {
                reasoning.Add($"Format: {fileExtension.ToUpper()} commonly used by cameras");
            }
            else if (screenshotFormats.Contains(fileExtension))
            {
                reasoning.Add($"Format: {fileExtension.ToUpper()} commonly used for screenshots");
            }

            // Resolution analysis
            var aspectRatio = height > 0 ? (double)width / height : 0;
            if (width > 0 && height > 0)
            {
                reasoning.Add($"Dimensions: {width}×{height}");
                
                // Very high resolution suggests camera
                if (width > 4000 && height > 3000)
                {
                    reasoning.Add("Very high resolution typical of modern cameras");
                    return ("photo", 0.75, $"High resolution analysis: {string.Join(" | ", reasoning)}");
                }
                
                // Unusual aspect ratios might indicate screenshots (especially very wide or tall)
                if (aspectRatio < 0.5 || aspectRatio > 3.0)
                {
                    reasoning.Add($"Unusual aspect ratio ({aspectRatio:F2}) suggests cropped content or screenshot");
                    return ("screenshot", 0.65, $"Aspect ratio analysis: {string.Join(" | ", reasoning)}");
                }
            }

            // File size analysis
            if (fileSize > 0)
            {
                var sizeMB = fileSize / (1024.0 * 1024.0);
                reasoning.Add($"File size: {sizeMB:F1}MB");
                
                // Very small files are often screenshots or heavily compressed
                if (sizeMB < 0.1)
                {
                    reasoning.Add("Very small file size suggests compressed screenshot or icon");
                    return ("screenshot", 0.60, $"File size analysis: {string.Join(" | ", reasoning)}");
                }
            }

            // Build final reasoning for unknown classification
            if (reasoning.Count == 0)
            {
                reasoning.Add("No camera metadata found");
                reasoning.Add("No screenshot software detected");
                reasoning.Add("No clear resolution patterns");
            }

            return ("unknown", 0.30, $"Insufficient evidence: {string.Join(" | ", reasoning)} - need more distinctive metadata for classification");
        }
    }
}