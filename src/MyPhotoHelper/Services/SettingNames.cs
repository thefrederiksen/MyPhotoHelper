namespace MyPhotoHelper.Services;

/// <summary>
/// Constants for all application setting names to ensure consistency
/// </summary>
public static class SettingNames
{
    // Feature Toggles
    public const string EnableDuplicateDetection = "EnableDuplicateDetection";
    public const string EnableAIImageAnalysis = "EnableAIImageAnalysis";
    public const string AutoScanOnStartup = "AutoScanOnStartup";
    public const string RunOnWindowsStartup = "RunOnWindowsStartup";
    public const string ScanSubdirectories = "ScanSubdirectories";
    public const string EnableDirectoryMonitoring = "EnableDirectoryMonitoring";
    
    // File Type Support
    public const string SupportJpeg = "SupportJpeg";
    public const string SupportPng = "SupportPng";
    public const string SupportHeic = "SupportHeic";
    public const string SupportGif = "SupportGif";
    public const string SupportBmp = "SupportBmp";
    public const string SupportWebp = "SupportWebp";
    
    // Performance Settings
    public const string BatchSize = "BatchSize";
    public const string MaxConcurrentTasks = "MaxConcurrentTasks";
    
    // AI Settings
    public const string AIProvider = "AIProvider";
    public const string AIApiKey = "AIApiKey";
    public const string AIApiEndpoint = "AIApiEndpoint";
    public const string AIModel = "AIModel";
    public const string AITemperature = "AITemperature";
    
    // Other Settings
    public const string ThemeName = "ThemeName";
    public const string LastScanDate = "LastScanDate";
}