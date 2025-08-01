namespace MyPhotoHelper.Models
{
    public enum ScanPhase
    {
        None = 0,
        Phase1_Discovery = 1,           // Find files and basic info
        Phase2_Metadata = 2,            // Extract EXIF and image metadata
        Phase3_ScreenshotDetection = 3, // Detect and filter screenshots
        Phase4_Hashing = 4,             // Calculate file hashes for duplicate detection
        Phase5_Analysis = 5,            // AI analysis and categorization
        Completed = 6,
        Failed = 7                      // Scan failed with errors
    }

    public class PhasedScanProgress
    {
        public ScanPhase CurrentPhase { get; set; } = ScanPhase.None;
        public Dictionary<ScanPhase, PhaseProgress> PhaseProgress { get; set; } = new();
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public bool IsRunning { get; set; }
        
        public PhasedScanProgress()
        {
            // Initialize all phases
            foreach (ScanPhase phase in Enum.GetValues<ScanPhase>())
            {
                if (phase != ScanPhase.None && phase != ScanPhase.Completed && phase != ScanPhase.Failed)
                {
                    PhaseProgress[phase] = new PhaseProgress { Phase = phase };
                }
            }
        }
    }

    public class PhaseProgress
    {
        public ScanPhase Phase { get; set; }
        public int TotalItems { get; set; }
        public int ProcessedItems { get; set; }
        public int SuccessCount { get; set; }
        public int ErrorCount { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? CurrentItem { get; set; }
        public List<string> RecentErrors { get; set; } = new();
        public bool IsComplete => ProcessedItems >= TotalItems && TotalItems > 0;
        public double ProgressPercentage => TotalItems > 0 ? (ProcessedItems * 100.0 / TotalItems) : 0;
        
        public void AddError(string error)
        {
            ErrorCount++;
            RecentErrors.Add($"[{DateTime.Now:HH:mm:ss}] {error}");
            
            // Keep only the last 10 errors to prevent memory bloat
            if (RecentErrors.Count > 10)
            {
                RecentErrors.RemoveAt(0);
            }
        }
    }
}