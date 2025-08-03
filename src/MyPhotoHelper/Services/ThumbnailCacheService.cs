using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text;
using CSnakes.Runtime;

namespace MyPhotoHelper.Services;

// Backward compatibility interface
public interface IHeicCacheService
{
    Task<byte[]?> GetCachedHeicThumbnailAsync(string originalPath, int thumbnailSize);
    Task<byte[]?> ConvertHeicToJpegAsync(string originalPath, int maxSize, int quality);
    string GetCacheDirectory();
}

public interface IThumbnailCacheService : IHeicCacheService
{
    Task<byte[]?> GetCachedThumbnailAsync(string originalPath, int thumbnailSize);
    Task ClearCacheAsync();
    Task<long> GetCacheSizeAsync();
}

public class ThumbnailCacheService : IThumbnailCacheService
{
    private readonly IPythonEnvironment? _pythonEnv;
    private readonly string _cacheDirectory;
    private readonly ILogger<ThumbnailCacheService> _logger;
    private readonly IThumbnailService _thumbnailService;

    public ThumbnailCacheService(
        IServiceProvider serviceProvider, 
        ILogger<ThumbnailCacheService> logger, 
        IPathService pathService,
        IThumbnailService thumbnailService)
    {
        _logger = logger;
        _thumbnailService = thumbnailService;
        
        // Try to get Python environment for HEIC support
        try
        {
            _pythonEnv = serviceProvider.GetService<IPythonEnvironment>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Python environment not available for HEIC conversion: {ex.Message}");
        }

        // Set up cache directory using PathService temp directory
        _cacheDirectory = Path.Combine(pathService.GetTempDirectory(), "ThumbnailCache");
        
        try
        {
            Directory.CreateDirectory(_cacheDirectory);
            _logger.LogInformation($"Thumbnail cache directory created/verified: {_cacheDirectory}");
            
            // Test write permissions
            var testFile = Path.Combine(_cacheDirectory, "test.tmp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            _logger.LogInformation("Thumbnail cache directory is writable");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to create/verify thumbnail cache directory: {_cacheDirectory}");
        }
    }

    public string GetCacheDirectory() => _cacheDirectory;

    public async Task<byte[]?> GetCachedThumbnailAsync(string originalPath, int thumbnailSize)
    {
        _logger.LogDebug($"GetCachedThumbnailAsync called for: {originalPath}, size: {thumbnailSize}");
        
        try
        {
            // Check if this is a HEIC file
            var extension = Path.GetExtension(originalPath).ToLowerInvariant();
            if (extension == ".heic" || extension == ".heif")
            {
                return await GetCachedHeicThumbnailAsync(originalPath, thumbnailSize);
            }

            // For non-HEIC files, use standard thumbnail generation with caching
            var cacheKey = GetCacheKey(originalPath, thumbnailSize, 85); // Quality 85 for regular thumbnails
            var cachedFilePath = GetCachedFilePath(cacheKey);
            
            _logger.LogDebug($"Cache file path: {cachedFilePath}");

            // Check if cached file exists and is newer than original
            if (File.Exists(cachedFilePath))
            {
                var originalFileInfo = new FileInfo(originalPath);
                var cachedFileInfo = new FileInfo(cachedFilePath);

                if (cachedFileInfo.LastWriteTimeUtc >= originalFileInfo.LastWriteTimeUtc)
                {
                    _logger.LogDebug($"Returning cached thumbnail: {cachedFilePath}");
                    return await File.ReadAllBytesAsync(cachedFilePath);
                }
                else
                {
                    _logger.LogDebug($"Cache file is older than original, will regenerate");
                }
            }

            // Generate thumbnail
            _logger.LogDebug($"Creating thumbnail: {originalPath} (size: {thumbnailSize})");
            
            var thumbnailBytes = await _thumbnailService.GetThumbnailAsync(originalPath, thumbnailSize, thumbnailSize);
            
            if (thumbnailBytes == null || thumbnailBytes.Length == 0)
            {
                _logger.LogWarning($"Thumbnail generation returned empty for: {originalPath}");
                return null;
            }

            // Save thumbnail to cache
            try
            {
                _logger.LogDebug($"Saving thumbnail to cache: {cachedFilePath} ({thumbnailBytes.Length} bytes)");
                await File.WriteAllBytesAsync(cachedFilePath, thumbnailBytes);
                
                // Verify the file was written
                if (File.Exists(cachedFilePath))
                {
                    var fileInfo = new FileInfo(cachedFilePath);
                    _logger.LogDebug($"Successfully cached thumbnail: {cachedFilePath} (size: {fileInfo.Length} bytes)");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to cache thumbnail: {cachedFilePath}");
                // Continue even if caching fails
            }

            return thumbnailBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating cached thumbnail: {originalPath}");
            return null;
        }
    }

    public async Task<byte[]?> GetCachedHeicThumbnailAsync(string originalPath, int thumbnailSize)
    {
        _logger.LogDebug($"GetCachedHeicThumbnailAsync called for: {originalPath}, size: {thumbnailSize}");
        
        if (_pythonEnv == null)
        {
            _logger.LogWarning("Python environment not available for HEIC conversion");
            return null;
        }

        try
        {
            // Generate cache key for HEIC thumbnails (always use quality 80 for HEIC thumbnails)
            var cacheKey = GetCacheKey(originalPath, thumbnailSize, 80);
            var cachedFilePath = GetCachedFilePath(cacheKey);
            
            _logger.LogDebug($"HEIC cache file path: {cachedFilePath}");

            // Check if cached file exists and is newer than original
            if (File.Exists(cachedFilePath))
            {
                var originalFileInfo = new FileInfo(originalPath);
                var cachedFileInfo = new FileInfo(cachedFilePath);

                if (cachedFileInfo.LastWriteTimeUtc >= originalFileInfo.LastWriteTimeUtc)
                {
                    _logger.LogDebug($"Returning cached HEIC thumbnail: {cachedFilePath}");
                    return await File.ReadAllBytesAsync(cachedFilePath);
                }
                else
                {
                    _logger.LogDebug($"HEIC cache file is older than original, will regenerate");
                }
            }

            // Convert HEIC to JPEG thumbnail
            _logger.LogDebug($"Creating HEIC thumbnail: {originalPath} (size: {thumbnailSize})");
            
            var jpegBytes = _pythonEnv.HeicConverter().GetHeicThumbnail(originalPath, thumbnailSize);
            
            if (jpegBytes == null)
            {
                _logger.LogWarning($"HEIC thumbnail conversion returned null for: {originalPath}");
                return null;
            }

            // Save thumbnail to cache
            try
            {
                _logger.LogDebug($"Saving HEIC thumbnail to cache: {cachedFilePath} ({jpegBytes.Length} bytes)");
                await File.WriteAllBytesAsync(cachedFilePath, jpegBytes);
                
                // Verify the file was written
                if (File.Exists(cachedFilePath))
                {
                    var fileInfo = new FileInfo(cachedFilePath);
                    _logger.LogDebug($"Successfully cached HEIC thumbnail: {cachedFilePath} (size: {fileInfo.Length} bytes)");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to cache HEIC thumbnail: {cachedFilePath}");
                // Continue even if caching fails
            }

            return jpegBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating HEIC thumbnail: {originalPath}");
            return null;
        }
    }

    public async Task<byte[]?> ConvertHeicToJpegAsync(string originalPath, int maxSize, int quality)
    {
        if (_pythonEnv == null)
        {
            _logger.LogWarning("Python environment not available for HEIC conversion");
            return null;
        }

        try
        {
            // For full-size images, don't cache - convert on demand
            _logger.LogInformation($"Converting HEIC to JPEG on demand: {originalPath} (max: {maxSize}, quality: {quality})");
            
            // Run the conversion on a background thread to avoid blocking
            var jpegBytes = await Task.Run(() => 
                _pythonEnv.HeicConverter().ConvertHeicToJpeg(originalPath, maxSize, quality));
            
            if (jpegBytes == null)
            {
                _logger.LogWarning($"HEIC conversion returned null for: {originalPath}");
                return null;
            }

            return jpegBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error converting HEIC file: {originalPath}");
            return null;
        }
    }

    private string GetCacheKey(string filePath, int maxSize, int quality)
    {
        // Create a unique cache key based on file path, size, and quality
        var input = $"{filePath.ToLowerInvariant()}|{maxSize}|{quality}";
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes).Substring(0, 16); // Use first 16 chars of hash
    }
    
    private string GetCachedFilePath(string cacheKey)
    {
        // Use first 2 characters of hash for subdirectory (256 possible subdirs)
        var subDir = cacheKey.Substring(0, 2).ToLower();
        
        // Create subdirectory path
        var subDirPath = Path.Combine(_cacheDirectory, subDir);
        
        // Ensure subdirectory exists
        if (!Directory.Exists(subDirPath))
        {
            Directory.CreateDirectory(subDirPath);
            _logger.LogDebug($"Created cache subdirectory: {subDirPath}");
        }
        
        // Return full path with filename
        return Path.Combine(subDirPath, $"thumb_{cacheKey}.jpg");
    }

    public async Task ClearCacheAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                if (Directory.Exists(_cacheDirectory))
                {
                    _logger.LogInformation($"Clearing thumbnail cache: {_cacheDirectory}");
                    
                    // Delete all files in subdirectories
                    var di = new DirectoryInfo(_cacheDirectory);
                    foreach (var file in di.GetFiles("*", SearchOption.AllDirectories))
                    {
                        file.Delete();
                    }
                    
                    // Delete subdirectories
                    foreach (var dir in di.GetDirectories())
                    {
                        dir.Delete(true);
                    }
                    
                    _logger.LogInformation("Thumbnail cache cleared successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing thumbnail cache");
            }
        });
    }

    public async Task<long> GetCacheSizeAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!Directory.Exists(_cacheDirectory))
                    return 0L;

                var di = new DirectoryInfo(_cacheDirectory);
                var size = di.GetFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
                
                return size;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating cache size");
                return 0L;
            }
        });
    }
}