using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

namespace MyPhotoHelper.Services
{
    public interface IThumbnailService
    {
        Task<byte[]> GetThumbnailAsync(string imagePath, int width = 200, int height = 200);
    }

    public class ThumbnailService : IThumbnailService
    {
        private readonly ILogger<ThumbnailService> _logger;

        public ThumbnailService(ILogger<ThumbnailService> logger)
        {
            _logger = logger;
        }

        public async Task<byte[]> GetThumbnailAsync(string imagePath, int width = 200, int height = 200)
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
                    
                    // Apply EXIF orientation if present
                    ApplyExifOrientation(image);
                    
                    // Calculate aspect ratio
                    var ratioX = (double)width / image.Width;
                    var ratioY = (double)height / image.Height;
                    var ratio = Math.Min(ratioX, ratioY);

                    var newWidth = (int)(image.Width * ratio);
                    var newHeight = (int)(image.Height * ratio);

                    using var thumbnail = new Bitmap(newWidth, newHeight);
                    using var graphics = Graphics.FromImage(thumbnail);
                    
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.SmoothingMode = SmoothingMode.HighQuality;
                    
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

        private void ApplyExifOrientation(Image image)
        {
            const int OrientationPropertyId = 0x0112;

            if (!image.PropertyIdList.Contains(OrientationPropertyId))
                return;

            var orientationProperty = image.GetPropertyItem(OrientationPropertyId);
            if (orientationProperty?.Value == null || orientationProperty.Value.Length == 0)
                return;

            int orientation = orientationProperty.Value[0];

            switch (orientation)
            {
                case 1:
                    // Normal - no rotation needed
                    break;
                case 2:
                    // Mirror horizontal
                    image.RotateFlip(RotateFlipType.RotateNoneFlipX);
                    break;
                case 3:
                    // Rotate 180
                    image.RotateFlip(RotateFlipType.Rotate180FlipNone);
                    break;
                case 4:
                    // Mirror vertical
                    image.RotateFlip(RotateFlipType.RotateNoneFlipY);
                    break;
                case 5:
                    // Mirror horizontal and rotate 90 CW
                    image.RotateFlip(RotateFlipType.Rotate90FlipX);
                    break;
                case 6:
                    // Rotate 90 CW
                    image.RotateFlip(RotateFlipType.Rotate90FlipNone);
                    break;
                case 7:
                    // Mirror horizontal and rotate 270 CW
                    image.RotateFlip(RotateFlipType.Rotate270FlipX);
                    break;
                case 8:
                    // Rotate 270 CW
                    image.RotateFlip(RotateFlipType.Rotate270FlipNone);
                    break;
            }

            // Remove the orientation property to prevent double-rotation
            if (image.PropertyIdList.Contains(OrientationPropertyId))
            {
                image.RemovePropertyItem(OrientationPropertyId);
            }
        }
    }
}