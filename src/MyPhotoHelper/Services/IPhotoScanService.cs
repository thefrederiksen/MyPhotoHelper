using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyPhotoHelper.Services
{
    public interface IPhotoScanService
    {
        event EventHandler<ScanProgressEventArgs>? ScanProgressChanged;
        event EventHandler<ScanCompletedEventArgs>? ScanCompleted;
        
        bool IsScanning { get; }
        ScanProgress? CurrentProgress { get; }
        
        Task StartScanAsync(CancellationToken cancellationToken = default);
        void CancelScan();
    }
    
    public class ScanProgressEventArgs : EventArgs
    {
        public int TotalDirectories { get; set; }
        public int ProcessedDirectories { get; set; }
        public int TotalFiles { get; set; }
        public int ProcessedFiles { get; set; }
        public string CurrentDirectory { get; set; } = "";
        public string? CurrentFile { get; set; }
        public int ErrorCount { get; set; }
    }
    
    public class ScanCompletedEventArgs : EventArgs
    {
        public bool Success { get; set; }
        public int TotalFilesProcessed { get; set; }
        public int NewFilesAdded { get; set; }
        public int ErrorCount { get; set; }
        public TimeSpan Duration { get; set; }
        public string? ErrorMessage { get; set; }
    }
    
    public class ScanProgress
    {
        public int TotalDirectories { get; set; }
        public int ProcessedDirectories { get; set; }
        public int TotalFiles { get; set; }
        public int ProcessedFiles { get; set; }
        public string CurrentDirectory { get; set; } = "";
        public string? CurrentFile { get; set; }
        public int ErrorCount { get; set; }
        public DateTime StartTime { get; set; }
        
        public double PercentComplete => TotalFiles > 0 ? (double)ProcessedFiles / TotalFiles * 100 : 0;
    }
}