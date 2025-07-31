using System;

namespace MyPhotoHelper.Services
{
    public interface IImageDetailsService
    {
        event EventHandler<int>? ShowImageDetailsRequested;
        void ShowImageDetails(int imageId);
    }

    public class ImageDetailsService : IImageDetailsService
    {
        public event EventHandler<int>? ShowImageDetailsRequested;

        public void ShowImageDetails(int imageId)
        {
            ShowImageDetailsRequested?.Invoke(this, imageId);
        }
    }
}