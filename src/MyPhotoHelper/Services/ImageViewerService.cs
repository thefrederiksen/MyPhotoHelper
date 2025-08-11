using MyPhotoHelper.Models;

namespace MyPhotoHelper.Services
{
    public interface IImageViewerService
    {
        event Action? OnStateChanged;
        
        bool IsViewerOpen { get; }
        int CurrentImageIndex { get; }
        List<tbl_images>? PhotoCollection { get; }
        tbl_images? CurrentImage { get; }
        
        void OpenViewer(List<tbl_images> photos, int startIndex = 0);
        void CloseViewer();
        void NavigateNext();
        void NavigatePrevious();
        void NavigateToIndex(int index);
    }
    
    public class ImageViewerService : IImageViewerService
    {
        public event Action? OnStateChanged;
        
        private bool _isViewerOpen;
        private int _currentImageIndex;
        private List<tbl_images>? _photoCollection;
        
        public bool IsViewerOpen 
        { 
            get => _isViewerOpen;
            private set
            {
                if (_isViewerOpen != value)
                {
                    _isViewerOpen = value;
                    OnStateChanged?.Invoke();
                }
            }
        }
        
        public int CurrentImageIndex 
        { 
            get => _currentImageIndex;
            private set
            {
                if (_currentImageIndex != value)
                {
                    _currentImageIndex = value;
                    OnStateChanged?.Invoke();
                }
            }
        }
        
        public List<tbl_images>? PhotoCollection 
        { 
            get => _photoCollection;
            private set
            {
                _photoCollection = value;
                OnStateChanged?.Invoke();
            }
        }
        
        public tbl_images? CurrentImage 
        {
            get
            {
                if (PhotoCollection == null || CurrentImageIndex < 0 || CurrentImageIndex >= PhotoCollection.Count)
                    return null;
                return PhotoCollection[CurrentImageIndex];
            }
        }
        
        public void OpenViewer(List<tbl_images> photos, int startIndex = 0)
        {
            if (photos == null || photos.Count == 0)
                return;
                
            PhotoCollection = photos;
            CurrentImageIndex = Math.Max(0, Math.Min(startIndex, photos.Count - 1));
            IsViewerOpen = true;
        }
        
        public void CloseViewer()
        {
            IsViewerOpen = false;
            PhotoCollection = null;
            CurrentImageIndex = 0;
        }
        
        public void NavigateNext()
        {
            if (PhotoCollection == null || PhotoCollection.Count == 0)
                return;
                
            if (CurrentImageIndex < PhotoCollection.Count - 1)
            {
                CurrentImageIndex++;
            }
        }
        
        public void NavigatePrevious()
        {
            if (PhotoCollection == null || PhotoCollection.Count == 0)
                return;
                
            if (CurrentImageIndex > 0)
            {
                CurrentImageIndex--;
            }
        }
        
        public void NavigateToIndex(int index)
        {
            if (PhotoCollection == null || PhotoCollection.Count == 0)
                return;
                
            if (index >= 0 && index < PhotoCollection.Count)
            {
                CurrentImageIndex = index;
            }
        }
    }
}