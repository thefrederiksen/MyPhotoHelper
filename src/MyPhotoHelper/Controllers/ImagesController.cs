using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyPhotoHelper.Data;
using MyPhotoHelper.Services;
using System.Drawing;
using System.Drawing.Imaging;
using CSnakes.Runtime;

namespace MyPhotoHelper.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ImagesController : ControllerBase
{
    private readonly MyPhotoHelperDbContext _context;
    private readonly IPathService _pathService;
    private readonly IPythonEnvironment? _pythonEnv;
    private readonly IThumbnailCacheService _thumbnailCacheService;

    public ImagesController(MyPhotoHelperDbContext context, IPathService pathService, IServiceProvider serviceProvider, IThumbnailCacheService thumbnailCacheService)
    {
        _context = context;
        _pathService = pathService;
        _thumbnailCacheService = thumbnailCacheService;
        
        // Try to get Python environment for HEIC support
        try
        {
            _pythonEnv = serviceProvider.GetService<IPythonEnvironment>();
        }
        catch (Exception ex)
        {
            Logger.Warning($"Python environment not available for HEIC thumbnails: {ex.Message}");
        }
    }

    [HttpGet("photo/{id}")]
    public async Task<IActionResult> GetPhoto(int id)
    {
        try
        {
            var image = await _context.tbl_images
                .Include(img => img.ScanDirectory)
                .AsNoTracking()
                .FirstOrDefaultAsync(img => img.ImageId == id && img.IsDeleted == 0 && img.FileExists == 1);

            if (image == null)
            {
                Logger.Error($"Image not found with ID: {id}");
                return NotFound();
            }

            if (image.ScanDirectory == null)
            {
                Logger.Error($"Scan directory not loaded for image {id}");
                return NotFound("Scan directory not found");
            }

            // Normalize the path to handle any path separator issues and prevent directory traversal
            var normalizedRelativePath = image.RelativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            
            // Validate against path traversal attacks
            if (normalizedRelativePath.Contains("..") || Path.IsPathRooted(normalizedRelativePath))
            {
                Logger.Error($"Potential path traversal attempt detected for image {id}: {normalizedRelativePath}");
                return NotFound("Invalid path");
            }
            
            var fullPath = Path.Combine(image.ScanDirectory.DirectoryPath, normalizedRelativePath);
            
            // Ensure the resolved path is still within the scan directory
            var resolvedPath = Path.GetFullPath(fullPath);
            var scanDirPath = Path.GetFullPath(image.ScanDirectory.DirectoryPath);
            if (!resolvedPath.StartsWith(scanDirPath, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Error($"Path traversal detected: resolved path {resolvedPath} is outside scan directory {scanDirPath}");
                return NotFound("Invalid path");
            }
            
            if (!System.IO.File.Exists(fullPath))
            {
                Logger.Error($"File not found at path: {fullPath}");
                return NotFound("File not found");
            }

            // Check if this is a HEIC file and convert if necessary
            var extension = (image.FileExtension ?? "").ToLowerInvariant();
            var isHeicFile = extension == ".heic" || extension == ".heif";

            if (isHeicFile && _pythonEnv != null)
            {
                try
                {
                    Logger.Info($"Converting HEIC file for full view: {image.FileName}");
                    
                    // Convert HEIC to JPEG for display
                    var heicBytes = await _thumbnailCacheService.GetCachedHeicThumbnailAsync(fullPath, 2000); // Larger size for full view
                    
                    if (heicBytes != null)
                    {
                        Logger.Info($"Successfully converted HEIC file for {image.FileName}");
                        return File(heicBytes, "image/jpeg");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"HEIC conversion failed for {image.FileName}: {ex.Message}");
                }
            }

            // Return the original file
            var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPath);
            var contentType = GetContentType(extension);
            return File(fileBytes, contentType);
        }
        catch (Exception ex)
        {
            Logger.Error($"Error serving photo {id}: {ex.Message}");
            return StatusCode(500);
        }
    }

    private string GetContentType(string extension)
    {
        return extension?.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".heic" or ".heif" => "image/heic",
            _ => "application/octet-stream"
        };
    }

    [HttpGet("{id}/thumbnail")]
    public async Task<IActionResult> GetThumbnail(int id)
    {
        try
        {
            var image = await _context.tbl_images
                .Include(img => img.ScanDirectory)
                .AsNoTracking()
                .FirstOrDefaultAsync(img => img.ImageId == id && img.IsDeleted == 0 && img.FileExists == 1);

            if (image == null)
            {
                Logger.Error($"Image not found with ID: {id}");
                return NotFound();
            }

            if (image.ScanDirectory == null)
            {
                Logger.Error($"Scan directory not loaded for image {id}");
                return NotFound("Scan directory not found");
            }

            // Normalize the path to handle any path separator issues and prevent directory traversal
            var normalizedRelativePath = image.RelativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            
            // Validate against path traversal attacks
            if (normalizedRelativePath.Contains("..") || Path.IsPathRooted(normalizedRelativePath))
            {
                Logger.Error($"Potential path traversal attempt detected for image {id}: {normalizedRelativePath}");
                return NotFound("Invalid path");
            }
            
            var fullPath = Path.Combine(image.ScanDirectory.DirectoryPath, normalizedRelativePath);
            
            // Ensure the resolved path is still within the scan directory
            var resolvedPath = Path.GetFullPath(fullPath);
            var scanDirPath = Path.GetFullPath(image.ScanDirectory.DirectoryPath);
            if (!resolvedPath.StartsWith(scanDirPath, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Error($"Path traversal detected: resolved path {resolvedPath} is outside scan directory {scanDirPath}");
                return NotFound("Invalid path");
            }
            
            if (!System.IO.File.Exists(fullPath))
            {
                Logger.Error($"File not found at path: {fullPath}");
                return NotFound("File not found");
            }

            // Check if this is a HEIC file and use Python converter if available
            var extension = (image.FileExtension ?? "").ToLowerInvariant();
            var isHeicFile = extension == ".heic" || extension == ".heif";

            if (isHeicFile)
            {
                try
                {
                    Logger.Info($"Using cached HEIC converter for thumbnail: {image.FileName}");
                    
                    // Use cached HEIC thumbnail
                    var heicBytes = await _thumbnailCacheService.GetCachedHeicThumbnailAsync(fullPath, 250);
                    
                    if (heicBytes != null)
                    {
                        Logger.Info($"Successfully generated HEIC thumbnail for {image.FileName}");
                        return File(heicBytes, "image/jpeg");
                    }
                    else
                    {
                        Logger.Warning($"HEIC converter returned null for {image.FileName}, falling back to placeholder");
                        return GetPlaceholderThumbnail(isHeicFile);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"HEIC conversion failed for {image.FileName}: {ex.Message}");
                    return GetPlaceholderThumbnail(isHeicFile);
                }
            }

            // Standard thumbnail generation for non-HEIC files or fallback
            try
            {
                using var originalImage = Image.FromFile(fullPath);
                var thumbnailSize = 200;
                var aspectRatio = (double)originalImage.Width / originalImage.Height;
                
                int thumbnailWidth, thumbnailHeight;
                if (aspectRatio > 1)
                {
                    thumbnailWidth = thumbnailSize;
                    thumbnailHeight = (int)(thumbnailSize / aspectRatio);
                }
                else
                {
                    thumbnailWidth = (int)(thumbnailSize * aspectRatio);
                    thumbnailHeight = thumbnailSize;
                }

                using var thumbnail = new Bitmap(thumbnailWidth, thumbnailHeight);
                using var graphics = Graphics.FromImage(thumbnail);
                
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(originalImage, 0, 0, thumbnailWidth, thumbnailHeight);

                using var stream = new MemoryStream();
                thumbnail.Save(stream, ImageFormat.Jpeg);
                stream.Position = 0;

                return File(stream.ToArray(), "image/jpeg");
            }
            catch (OutOfMemoryException ex)
            {
                Logger.Error($"Out of memory error creating thumbnail for {image.FileName}: {ex.Message}");
                
                // Return a placeholder image for problematic files
                return GetPlaceholderThumbnail(isHeicFile);
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Error generating thumbnail for image {id}: {ex.Message}");
            return StatusCode(500);
        }
    }

    private IActionResult GetPlaceholderThumbnail(bool isHeicFile)
    {
        try
        {
            // Create a simple placeholder thumbnail
            var size = 200;
            using var placeholder = new Bitmap(size, size);
            using var graphics = Graphics.FromImage(placeholder);
            
            // Fill with light gray background
            graphics.Clear(Color.FromArgb(248, 249, 250));
            
            // Draw icon in center
            var iconSize = size / 3;
            var iconX = (size - iconSize) / 2;
            var iconY = (size - iconSize) / 2;
            
            using var brush = new SolidBrush(Color.FromArgb(108, 117, 125));
            using var font = new Font("Arial", iconSize / 4, FontStyle.Bold);
            
            var text = isHeicFile ? "HEIC" : "IMG";
            var textSize = graphics.MeasureString(text, font);
            var textX = (size - textSize.Width) / 2;
            var textY = (size - textSize.Height) / 2;
            
            graphics.DrawString(text, font, brush, textX, textY);
            
            using var stream = new MemoryStream();
            placeholder.Save(stream, ImageFormat.Jpeg);
            stream.Position = 0;
            
            return File(stream.ToArray(), "image/jpeg");
        }
        catch
        {
            // If even placeholder creation fails, return 404
            return NotFound();
        }
    }

    [HttpGet("{id}/raw")]
    public async Task<IActionResult> GetRawImage(int id)
    {
        try
        {
            var image = await _context.tbl_images
                .Include(img => img.ScanDirectory)
                .AsNoTracking()
                .FirstOrDefaultAsync(img => img.ImageId == id && img.IsDeleted == 0 && img.FileExists == 1);

            if (image == null)
            {
                return NotFound();
            }

            if (image.ScanDirectory == null)
            {
                return NotFound("Scan directory not found");
            }

            // Normalize the path to handle any path separator issues and prevent directory traversal
            var normalizedRelativePath = image.RelativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            
            // Validate against path traversal attacks
            if (normalizedRelativePath.Contains("..") || Path.IsPathRooted(normalizedRelativePath))
            {
                Logger.Error($"Potential path traversal attempt detected for image {id}: {normalizedRelativePath}");
                return NotFound("Invalid path");
            }
            
            var fullPath = Path.Combine(image.ScanDirectory.DirectoryPath, normalizedRelativePath);
            
            // Ensure the resolved path is still within the scan directory
            var resolvedPath = Path.GetFullPath(fullPath);
            var scanDirPath = Path.GetFullPath(image.ScanDirectory.DirectoryPath);
            if (!resolvedPath.StartsWith(scanDirPath, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Error($"Path traversal detected: resolved path {resolvedPath} is outside scan directory {scanDirPath}");
                return NotFound("Invalid path");
            }
            
            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound("File not found");
            }

            // Check if this is a HEIC file and convert it to JPEG for browser compatibility
            var extension = (image.FileExtension ?? "").ToLowerInvariant();
            var isHeicFile = extension == ".heic" || extension == ".heif";

            if (isHeicFile)
            {
                try
                {
                    Logger.Info($"Converting HEIC file to JPEG for browser display: {image.FileName}");
                    
                    // Convert HEIC on-demand (not cached for full size)
                    // Using 2000 as max dimension to get high quality while keeping file size reasonable
                    var jpegBytes = await _thumbnailCacheService.ConvertHeicToJpegAsync(fullPath, 2000, 85);
                    
                    if (jpegBytes != null)
                    {
                        Logger.Info($"Successfully converted HEIC to JPEG for {image.FileName}");
                        return File(jpegBytes, "image/jpeg");
                    }
                    else
                    {
                        Logger.Warning($"HEIC conversion returned null for {image.FileName}, returning placeholder");
                        return GetPlaceholderThumbnail(isHeicFile);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"HEIC conversion failed for {image.FileName}: {ex.Message}");
                    // Return placeholder for HEIC files that fail to convert
                    return GetPlaceholderThumbnail(isHeicFile);
                }
            }

            // Serve non-HEIC files directly
            var contentType = GetContentType(image.FileExtension ?? "");
            
            // Use FileStream for better memory efficiency
            var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
            return File(fileStream, contentType, enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            Logger.Error($"Error serving raw image {id}: {ex.Message}");
            return StatusCode(500);
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetImage(int id)
    {
        try
        {
            var image = await _context.tbl_images
                .Include(img => img.ScanDirectory)
                .AsNoTracking()
                .FirstOrDefaultAsync(img => img.ImageId == id && img.IsDeleted == 0 && img.FileExists == 1);

            if (image == null)
            {
                return NotFound();
            }

            if (image.ScanDirectory == null)
            {
                return NotFound("Scan directory not found");
            }

            // Normalize the path to handle any path separator issues and prevent directory traversal
            var normalizedRelativePath = image.RelativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            
            // Validate against path traversal attacks
            if (normalizedRelativePath.Contains("..") || Path.IsPathRooted(normalizedRelativePath))
            {
                Logger.Error($"Potential path traversal attempt detected for image {id}: {normalizedRelativePath}");
                return NotFound("Invalid path");
            }
            
            var fullPath = Path.Combine(image.ScanDirectory.DirectoryPath, normalizedRelativePath);
            
            // Ensure the resolved path is still within the scan directory
            var resolvedPath = Path.GetFullPath(fullPath);
            var scanDirPath = Path.GetFullPath(image.ScanDirectory.DirectoryPath);
            if (!resolvedPath.StartsWith(scanDirPath, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Error($"Path traversal detected: resolved path {resolvedPath} is outside scan directory {scanDirPath}");
                return NotFound("Invalid path");
            }
            
            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound();
            }

            var contentType = GetContentType(image.FileExtension ?? "");
            var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPath);
            
            return File(fileBytes, contentType);
        }
        catch (Exception ex)
        {
            Logger.Error($"Error serving image {id}: {ex.Message}");
            return StatusCode(500);
        }
    }
}