using System.Drawing;
using System.Drawing.Imaging;

namespace MyPhotoHelper.Services
{
    public interface IThumbnailService
    {
        Task<byte[]> GetThumbnailAsync(string imagePath, int width = 100, int height = 100);
    }

    public class ThumbnailService : IThumbnailService
    {
        private readonly ILogger<ThumbnailService> _logger;

        public ThumbnailService(ILogger<ThumbnailService> logger)
        {
            _logger = logger;
        }

        public async Task<byte[]> GetThumbnailAsync(string imagePath, int width = 100, int height = 100)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(imagePath))
                    {
                        _logger.LogWarning($"Image file not found: {imagePath}");
                        return Array.Empty<byte>();
                    }

                    using var image = Image.FromFile(imagePath);
                    
                    // Calculate aspect ratio
                    var ratioX = (double)width / image.Width;
                    var ratioY = (double)height / image.Height;
                    var ratio = Math.Min(ratioX, ratioY);

                    var newWidth = (int)(image.Width * ratio);
                    var newHeight = (int)(image.Height * ratio);

                    using var thumbnail = new Bitmap(newWidth, newHeight);
                    using var graphics = Graphics.FromImage(thumbnail);
                    
                    graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    
                    graphics.DrawImage(image, 0, 0, newWidth, newHeight);

                    using var ms = new MemoryStream();
                    thumbnail.Save(ms, ImageFormat.Jpeg);
                    return ms.ToArray();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error creating thumbnail for {imagePath}");
                    return Array.Empty<byte>();
                }
            });
        }
    }
}