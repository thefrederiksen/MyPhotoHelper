using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyPhotoHelper.Services
{
    public interface IGalleryUpdateService
    {
        event EventHandler<GalleryUpdateEventArgs> GalleryUpdated;
        void NotifyImageAdded(string filePath);
        void NotifyImageDeleted(string filePath);
        void NotifyImagesChanged(IEnumerable<string> addedPaths, IEnumerable<string> deletedPaths);
    }

    public class GalleryUpdateService : IGalleryUpdateService
    {
        private readonly ILogger<GalleryUpdateService> _logger;
        
        public event EventHandler<GalleryUpdateEventArgs>? GalleryUpdated;

        public GalleryUpdateService(ILogger<GalleryUpdateService> logger)
        {
            _logger = logger;
        }

        public void NotifyImageAdded(string filePath)
        {
            _logger.LogDebug("Notifying gallery of added image: {FilePath}", filePath);
            GalleryUpdated?.Invoke(this, new GalleryUpdateEventArgs
            {
                UpdateType = GalleryUpdateType.ImageAdded,
                AddedPaths = new[] { filePath }
            });
        }

        public void NotifyImageDeleted(string filePath)
        {
            _logger.LogDebug("Notifying gallery of deleted image: {FilePath}", filePath);
            GalleryUpdated?.Invoke(this, new GalleryUpdateEventArgs
            {
                UpdateType = GalleryUpdateType.ImageDeleted,
                DeletedPaths = new[] { filePath }
            });
        }

        public void NotifyImagesChanged(IEnumerable<string> addedPaths, IEnumerable<string> deletedPaths)
        {
            var added = addedPaths?.ToList() ?? new List<string>();
            var deleted = deletedPaths?.ToList() ?? new List<string>();
            
            _logger.LogDebug("Notifying gallery of changes. Added: {AddedCount}, Deleted: {DeletedCount}", 
                added.Count, deleted.Count);
                
            GalleryUpdated?.Invoke(this, new GalleryUpdateEventArgs
            {
                UpdateType = GalleryUpdateType.Multiple,
                AddedPaths = added,
                DeletedPaths = deleted
            });
        }
    }

    public class GalleryUpdateEventArgs : EventArgs
    {
        public GalleryUpdateType UpdateType { get; set; }
        public IEnumerable<string> AddedPaths { get; set; } = new List<string>();
        public IEnumerable<string> DeletedPaths { get; set; } = new List<string>();
    }

    public enum GalleryUpdateType
    {
        ImageAdded,
        ImageDeleted,
        Multiple
    }
}