using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CSnakes.Runtime;
using Microsoft.Extensions.Logging;

namespace MyPhotoHelper.Services
{
    public interface IImageDetailsService
    {
        event EventHandler<int>? ShowImageDetailsRequested;
        void ShowImageDetails(int imageId);
        Task<Dictionary<string, object>?> GetImageDetailsFromFileAsync(string imagePath);
    }

    public class ImageDetailsService : IImageDetailsService
    {
        private readonly ILogger<ImageDetailsService> _logger;
        private readonly IPythonEnvironment? _pythonEnv;
        
        public event EventHandler<int>? ShowImageDetailsRequested;

        public ImageDetailsService(ILogger<ImageDetailsService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            
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

        public void ShowImageDetails(int imageId)
        {
            ShowImageDetailsRequested?.Invoke(this, imageId);
        }

        public async Task<Dictionary<string, object>?> GetImageDetailsFromFileAsync(string imagePath)
        {
            return await Task.Run(() =>
            {
                if (_pythonEnv == null)
                {
                    _logger.LogWarning("Python environment is not available for metadata extraction");
                    return null;
                }

                if (string.IsNullOrEmpty(imagePath) || !System.IO.File.Exists(imagePath))
                {
                    _logger.LogWarning($"Image file not found: {imagePath}");
                    return null;
                }

                try
                {
                    _logger.LogInformation($"Extracting metadata from file: {imagePath}");
                    
                    // Call Python metadata_extractor module
                    var pythonResult = _pythonEnv.MetadataExtractor().ExtractImageMetadata(imagePath);
                    
                    if (pythonResult != null)
                    {
                        // Convert Python dictionary to C# Dictionary
                        var result = new Dictionary<string, object>();
                        
                        foreach (var key in pythonResult.Keys)
                        {
                            var value = pythonResult[key];
                            if (value != null && value.ToString() != "None")
                            {
                                result[key] = value;
                            }
                        }
                        
                        _logger.LogInformation($"Successfully extracted {result.Count} metadata fields from {imagePath}");
                        return result;
                    }
                    
                    return null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error extracting metadata from file {imagePath}");
                    return null;
                }
            });
        }
    }
}