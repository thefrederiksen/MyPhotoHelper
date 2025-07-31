namespace MyPhotoHelper.Models
{
    public enum ScanPhase
    {
        None = 0,
        Phase1_Discovery = 1,      // Find files and basic info
        Phase2_Hashing = 2,         // Calculate file hashes for duplicate detection
        Phase3_Metadata = 3,        // Extract EXIF and image metadata
        Phase4_Analysis = 4,        // AI analysis and categorization
        Completed = 5
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
                if (phase != ScanPhase.None && phase != ScanPhase.Completed)
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
        public bool IsComplete => ProcessedItems >= TotalItems && TotalItems > 0;
        public double ProgressPercentage => TotalItems > 0 ? (ProcessedItems * 100.0 / TotalItems) : 0;
    }
}