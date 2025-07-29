using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyPhotoHelper.Data;
using MyPhotoHelper.Services;
using System.Drawing;
using System.Drawing.Imaging;

namespace MyPhotoHelper.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ImagesController : ControllerBase
{
    private readonly MyPhotoHelperDbContext _context;
    private readonly IPathService _pathService;

    public ImagesController(MyPhotoHelperDbContext context, IPathService pathService)
    {
        _context = context;
        _pathService = pathService;
    }

    [HttpGet("{id}/thumbnail")]
    public async Task<IActionResult> GetThumbnail(int id)
    {
        try
        {
            var image = await _context.tbl_images
                .AsNoTracking()
                .FirstOrDefaultAsync(img => img.ImageId == id && img.IsDeleted == 0 && img.FileExists == 1);

            if (image == null)
            {
                return NotFound();
            }

            var settings = await _context.tbl_app_settings.FirstOrDefaultAsync();
            if (settings?.PhotoDirectory == null)
            {
                return NotFound("Photo directory not configured");
            }

            var fullPath = Path.Combine(settings.PhotoDirectory, image.RelativePath);
            
            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound();
            }

            // Create thumbnail
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
        catch (Exception ex)
        {
            Logger.Error($"Error generating thumbnail for image {id}: {ex.Message}");
            return StatusCode(500);
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetImage(int id)
    {
        try
        {
            var image = await _context.tbl_images
                .AsNoTracking()
                .FirstOrDefaultAsync(img => img.ImageId == id && img.IsDeleted == 0 && img.FileExists == 1);

            if (image == null)
            {
                return NotFound();
            }

            var settings = await _context.tbl_app_settings.FirstOrDefaultAsync();
            if (settings?.PhotoDirectory == null)
            {
                return NotFound("Photo directory not configured");
            }

            var fullPath = Path.Combine(settings.PhotoDirectory, image.RelativePath);
            
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