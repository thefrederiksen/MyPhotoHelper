using System;
using MyPhotoHelper.Models;

namespace MyPhotoHelper.Services
{
    public interface IScanStatusService
    {
        event EventHandler? StatusChanged;
        
        bool IsScanning { get; }
        ScanProgress? CurrentProgress { get; }
        PhasedScanProgress? CurrentPhasedProgress { get; }
        DateTime? LastScanTime { get; }
        ScanCompletedEventArgs? LastScanResult { get; }
        
        void UpdateStatus(bool isScanning, ScanProgress? progress = null);
        void UpdatePhasedStatus(PhasedScanProgress? progress);
        void UpdateLastScan(DateTime scanTime, ScanCompletedEventArgs result);
    }
    
    public class ScanStatusService : IScanStatusService
    {
        private bool _isScanning;
        private ScanProgress? _currentProgress;
        private PhasedScanProgress? _currentPhasedProgress;
        private DateTime? _lastScanTime;
        private ScanCompletedEventArgs? _lastScanResult;
        
        public event EventHandler? StatusChanged;
        
        public bool IsScanning => _isScanning || (_currentPhasedProgress?.IsRunning ?? false);
        public ScanProgress? CurrentProgress => _currentProgress;
        public PhasedScanProgress? CurrentPhasedProgress => _currentPhasedProgress;
        public DateTime? LastScanTime => _lastScanTime;
        public ScanCompletedEventArgs? LastScanResult => _lastScanResult;
        
        public void UpdateStatus(bool isScanning, ScanProgress? progress = null)
        {
            _isScanning = isScanning;
            _currentProgress = progress;
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
        
        public void UpdatePhasedStatus(PhasedScanProgress? progress)
        {
            _currentPhasedProgress = progress;
            _isScanning = progress?.IsRunning ?? false;
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