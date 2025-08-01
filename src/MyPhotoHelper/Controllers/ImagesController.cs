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

    public ImagesController(MyPhotoHelperDbContext context, IPathService pathService, IServiceProvider serviceProvider)
    {
        _context = context;
        _pathService = pathService;
        
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

            // Normalize the path to handle any path separator issues
            var normalizedRelativePath = image.RelativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(image.ScanDirectory.DirectoryPath, normalizedRelativePath);
            
            if (!System.IO.File.Exists(fullPath))
            {
                Logger.Error($"File not found at path: {fullPath}");
                return NotFound("File not found");
            }

            // Check if this is a HEIC file and use Python converter if available
            var extension = (image.FileExtension ?? "").ToLowerInvariant();
            var isHeicFile = extension == ".heic" || extension == ".heif";

            if (isHeicFile && _pythonEnv != null)
            {
                try
                {
                    Logger.Info($"Using Python HEIC converter for thumbnail: {image.FileName}");
                    
                    // Call Python HEIC converter
                    var heicBytes = _pythonEnv.HeicConverter().GetHeicThumbnail(fullPath, 250);
                    
                    if (heicBytes != null)
                    {
                        Logger.Info($"Successfully generated HEIC thumbnail for {image.FileName}");
                        return File(heicBytes, "image/jpeg");
                    }
                    else
                    {
                        Logger.Warning($"Python HEIC converter returned null for {image.FileName}, falling back to standard method");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Python HEIC conversion failed for {image.FileName}: {ex.Message}");
                    // Fall through to standard method
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

            // Normalize the path to handle any path separator issues
            var normalizedRelativePath = image.RelativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(image.ScanDirectory.DirectoryPath, normalizedRelativePath);
            
            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound("File not found");
            }

            // Serve the raw file directly - let the browser handle sizing
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

            // Normalize the path to handle any path separator issues
            var normalizedRelativePath = image.RelativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(image.ScanDirectory.DirectoryPath, normalizedRelativePath);
            
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

    private string GetContentType(string extension)
    {
        return extension.ToLower() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            ".heic" => "image/heic",
            ".heif" => "image/heif",
            _ => "application/octet-stream"
        };
    }
}