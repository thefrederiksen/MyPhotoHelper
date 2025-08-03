using System.Threading.Tasks;

namespace MyPhotoHelper.Services
{
    public interface IImageDisplayService
    {
        /// <summary>
        /// Gets a base64 data URI for displaying a thumbnail in Blazor components
        /// </summary>
        /// <param name="imageId">The database ID of the image</param>
        /// <returns>Data URI string for the thumbnail, or null if not found</returns>
        Task<string?> GetThumbnailDataUriAsync(int imageId);
        
        /// <summary>
        /// Gets the full file path for an image after validating it exists in the database
        /// </summary>
        /// <param name="imageId">The database ID of the image</param>
        /// <returns>Full file path, or null if not found</returns>
        Task<string?> GetImageFullPathAsync(int imageId);
        
        /// <summary>
        /// Gets a base64 data URI for displaying a thumbnail from a file path (for startup wizard)
        /// </summary>
        /// <param name="filePath">The file path</param>
        /// <param name="maxSize">Maximum thumbnail size</param>
        /// <returns>Data URI string for the thumbnail, or null if not found</returns>
        Task<string?> GetThumbnailDataUriFromPathAsync(string filePath, int maxSize = 250);
    }
}