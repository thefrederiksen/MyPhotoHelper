namespace MyPhotoHelper.Services
{
    public class ToastService : IToastService
    {
        public event Action<ToastMessage>? OnShow;

        public void ShowToast(string message, ToastType type = ToastType.Info, int durationMs = 3000)
        {
            var toast = new ToastMessage
            {
                Message = message,
                Type = type,
                DurationMs = durationMs
            };
            OnShow?.Invoke(toast);
        }

        public void ShowSuccess(string message, int durationMs = 3000)
        {
            ShowToast(message, ToastType.Success, durationMs);
        }

        public void ShowError(string message, int durationMs = 5000)
        {
            ShowToast(message, ToastType.Error, durationMs);
        }

        public void ShowWarning(string message, int durationMs = 4000)
        {
            ShowToast(message, ToastType.Warning, durationMs);
        }

        public void ShowInfo(string message, int durationMs = 3000)
        {
            ShowToast(message, ToastType.Info, durationMs);
        }
    }
}