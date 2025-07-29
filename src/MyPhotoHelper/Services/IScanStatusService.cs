using System;

namespace MyPhotoHelper.Services
{
    public interface IScanStatusService
    {
        event EventHandler? StatusChanged;
        
        bool IsScanning { get; }
        ScanProgress? CurrentProgress { get; }
        DateTime? LastScanTime { get; }
        ScanCompletedEventArgs? LastScanResult { get; }
        
        void UpdateStatus(bool isScanning, ScanProgress? progress = null);
        void UpdateLastScan(DateTime scanTime, ScanCompletedEventArgs result);
    }
    
    public class ScanStatusService : IScanStatusService
    {
        private bool _isScanning;
        private ScanProgress? _currentProgress;
        private DateTime? _lastScanTime;
        private ScanCompletedEventArgs? _lastScanResult;
        
        public event EventHandler? StatusChanged;
        
        public bool IsScanning => _isScanning;
        public ScanProgress? CurrentProgress => _currentProgress;
        public DateTime? LastScanTime => _lastScanTime;
        public ScanCompletedEventArgs? LastScanResult => _lastScanResult;
        
        public void UpdateStatus(bool isScanning, ScanProgress? progress = null)
        {
            _isScanning = isScanning;
            _currentProgress = progress;
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
        
        public void UpdateLastScan(DateTime scanTime, ScanCompletedEventArgs result)
        {
            _lastScanTime = scanTime;
            _lastScanResult = result;
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}