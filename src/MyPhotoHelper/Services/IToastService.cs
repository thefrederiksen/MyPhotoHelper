namespace MyPhotoHelper.Services
{
    public interface IToastService
    {
        event Action<ToastMessage> OnShow;
        void ShowToast(string message, ToastType type = ToastType.Info, int durationMs = 3000);
        void ShowSuccess(string message, int durationMs = 3000);
        void ShowError(string message, int durationMs = 5000);
        void ShowWarning(string message, int durationMs = 4000);
        void ShowInfo(string message, int durationMs = 3000);
    }

    public enum ToastType
    {
        Success,
        Error,
        Warning,
        Info
    }

    public class ToastMessage
    {
        public string Message { get; set; } = string.Empty;
        public ToastType Type { get; set; }
        public int DurationMs { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Id { get; set; } = Guid.NewGuid().ToString();
    }
}