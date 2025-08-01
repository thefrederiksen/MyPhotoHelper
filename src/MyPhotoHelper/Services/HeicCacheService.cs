using System.Security.Cryptography;
using System.Text;
using CSnakes.Runtime;

namespace MyPhotoHelper.Services;

public interface IHeicCacheService
{
    Task<byte[]?> GetCachedHeicThumbnailAsync(string originalPath, int thumbnailSize);
    Task<byte[]?> ConvertHeicToJpegAsync(string originalPath, int maxSize, int quality);
    string GetCacheDirectory();
}

public class HeicCacheService : IHeicCacheService
{
    private readonly IPythonEnvironment? _pythonEnv;
    private readonly string _cacheDirectory;
    private readonly ILogger<HeicCacheService> _logger;

    public HeicCacheService(IServiceProvider serviceProvider, ILogger<HeicCacheService> logger, IPathService pathService)
    {
        _logger = logger;
        
        // Try to get Python environment
        try
        {
            _pythonEnv = serviceProvider.GetService<IPythonEnvironment>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Python environment not available for HEIC conversion: {ex.Message}");
        }

        // Set up cache directory using PathService temp directory
        _cacheDirectory = Path.Combine(pathService.GetTempDirectory(), "HeicCache");
        
        try
        {
            Directory.CreateDirectory(_cacheDirectory);
            _logger.LogInformation($"HEIC cache directory created/verified: {_cacheDirectory}");
            
            // Test write permissions
            var testFile = Path.Combine(_cacheDirectory, "test.tmp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            _logger.LogInformation("HEIC cache directory is writable");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to create/verify HEIC cache directory: {_cacheDirectory}");
        }
    }

    public string GetCacheDirectory() => _cacheDirectory;

    public async Task<byte[]?> GetCachedHeicThumbnailAsync(string originalPath, int thumbnailSize)
    {
        _logger.LogInformation($"GetCachedHeicThumbnailAsync called for: {originalPath}, size: {thumbnailSize}");
        
        if (_pythonEnv == null)
        {
            _logger.LogWarning("Python environment not available for HEIC conversion");
            return null;
        }

        try
        {
            // Generate cache key for thumbnails (always use quality 80 for thumbnails)
            var cacheKey = GetCacheKey(originalPath, thumbnailSize, 80);
            var cachedFilePath = GetCachedFilePath(cacheKey);
            
            _logger.LogInformation($"Cache file path: {cachedFilePath}");

            // Check if cached file exists and is newer than original
            if (File.Exists(cachedFilePath))
            {
                var originalFileInfo = new FileInfo(originalPath);
                var cachedFileInfo = new FileInfo(cachedFilePath);

                if (cachedFileInfo.LastWriteTimeUtc >= originalFileInfo.LastWriteTimeUtc)
                {
                    _logger.LogInformation($"Returning cached HEIC thumbnail: {cachedFilePath}");
                    return await File.ReadAllBytesAsync(cachedFilePath);
                }
                else
                {
                    _logger.LogInformation($"Cache file is older than original, will regenerate");
                }
            }
            else
            {
                _logger.LogInformation($"Cache file does not exist, will generate");
            }

            // Convert HEIC to JPEG thumbnail
            _logger.LogInformation($"Creating HEIC thumbnail: {originalPath} (size: {thumbnailSize})");
            
            var jpegBytes = _pythonEnv.HeicConverter().GetHeicThumbnail(originalPath, thumbnailSize);
            
            if (jpegBytes == null)
            {
                _logger.LogWarning($"HEIC thumbnail conversion returned null for: {originalPath}");
                return null;
            }

            // Save thumbnail to cache
            try
            {
                _logger.LogInformation($"Saving thumbnail to cache: {cachedFilePath} ({jpegBytes.Length} bytes)");
                await File.WriteAllBytesAsync(cachedFilePath, jpegBytes);
                
                // Verify the file was written
                if (File.Exists(cachedFilePath))
                {
                    var fileInfo = new FileInfo(cachedFilePath);
                    _logger.LogInformation($"Successfully cached HEIC thumbnail: {cachedFilePath} (size: {fileInfo.Length} bytes)");
                }
                else
                {
                    _logger.LogError($"Cache file was not created: {cachedFilePath}");
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
        var subDir1 = cacheKey.Substring(0, 2).ToLower();
        
        // Use next 2 characters for second level subdirectory (65536 possible paths)
        var subDir2 = cacheKey.Substring(2, 2).ToLower();
        
        // Create subdirectory path
        var subDirPath = Path.Combine(_cacheDirectory, subDir1, subDir2);
        
        // Ensure subdirectory exists
        if (!Directory.Exists(subDirPath))
        {
            Directory.CreateDirectory(subDirPath);
            _logger.LogDebug($"Created cache subdirectory: {subDirPath}");
        }
        
        // Return full path with filename
        return Path.Combine(subDirPath, $"thumb_{cacheKey}.jpg");
    }
}