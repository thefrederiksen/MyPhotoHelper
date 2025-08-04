using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyPhotoHelper.Data;
using MyPhotoHelper.Models;

namespace MyPhotoHelper.Services
{
    public interface IDirectoryMonitoringService
    {
        void StartMonitoring();
        void StopMonitoring();
        bool IsMonitoring { get; }
        event EventHandler<FileSystemEventArgs> FileAdded;
        event EventHandler<FileSystemEventArgs> FileDeleted;
        event EventHandler<RenamedEventArgs> FileRenamed;
    }

    public class DirectoryMonitoringService : IDirectoryMonitoringService, IHostedService, IDisposable
    {
        private readonly ILogger<DirectoryMonitoringService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IGalleryUpdateService _galleryUpdateService;
        private readonly Dictionary<string, FileSystemWatcher> _watchers = new();
        private readonly SemaphoreSlim _processingLock = new(1, 1);
        private readonly HashSet<string> _processingFiles = new();
        private readonly System.Threading.Timer _batchProcessTimer;
        private readonly Queue<FileSystemEventArgs> _pendingEvents = new();
        private readonly Queue<string> _deletedFiles = new();
        private bool _isMonitoring;

        public bool IsMonitoring => _isMonitoring;
        
        public event EventHandler<FileSystemEventArgs>? FileAdded;
        public event EventHandler<FileSystemEventArgs>? FileDeleted;
        public event EventHandler<RenamedEventArgs>? FileRenamed;

        public DirectoryMonitoringService(
            ILogger<DirectoryMonitoringService> logger,
            IServiceProvider serviceProvider,
            IGalleryUpdateService galleryUpdateService)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _galleryUpdateService = galleryUpdateService;
            
            // Process batch events every 500ms for faster response
            _batchProcessTimer = new System.Threading.Timer(ProcessBatchEvents, null, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Check if monitoring is enabled
            using var scope = _serviceProvider.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            var isEnabled = await settingsService.GetSettingAsync<bool>(SettingNames.EnableDirectoryMonitoring, true);
            
            if (isEnabled)
            {
                _logger.LogInformation("Directory monitoring service starting");
                StartMonitoring();
            }
            else
            {
                _logger.LogInformation("Directory monitoring is disabled in settings");
            }
            
            await Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Directory monitoring service stopping");
            StopMonitoring();
            _batchProcessTimer?.Change(Timeout.Infinite, 0);
            await Task.CompletedTask;
        }

        public void StartMonitoring()
        {
            try
            {
                StopMonitoring(); // Clean up any existing watchers

                using var scope = _serviceProvider.CreateScope();
                using var context = scope.ServiceProvider.GetRequiredService<MyPhotoHelperDbContext>();
                
                var scanDirectories = context.tbl_scan_directory.ToList();
                
                foreach (var scanDir in scanDirectories)
                {
                    if (Directory.Exists(scanDir.DirectoryPath))
                    {
                        CreateWatcher(scanDir.DirectoryPath);
                    }
                    else
                    {
                        _logger.LogWarning("Scan directory does not exist: {Directory}", scanDir.DirectoryPath);
                    }
                }

                _isMonitoring = true;
                _logger.LogInformation("Started monitoring {Count} directories", _watchers.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting directory monitoring");
                _isMonitoring = false;
            }
        }

        public void StopMonitoring()
        {
            foreach (var watcher in _watchers.Values)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            
            _watchers.Clear();
            _isMonitoring = false;
            _logger.LogInformation("Stopped directory monitoring");
        }

        private void CreateWatcher(string path)
        {
            try
            {
                var watcher = new FileSystemWatcher(path)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true
                };

                // Set filters for image files
                watcher.Filters.Add("*.jpg");
                watcher.Filters.Add("*.jpeg");
                watcher.Filters.Add("*.png");
                watcher.Filters.Add("*.gif");
                watcher.Filters.Add("*.bmp");
                watcher.Filters.Add("*.webp");
                watcher.Filters.Add("*.heic");
                watcher.Filters.Add("*.heif");

                // Subscribe to events
                watcher.Created += OnFileCreated;
                watcher.Deleted += OnFileDeleted;
                watcher.Renamed += OnFileRenamed;
                watcher.Changed += OnFileChanged;
                watcher.Error += OnWatcherError;

                _watchers[path] = watcher;
                _logger.LogInformation("Created file watcher for: {Path}", path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating watcher for path: {Path}", path);
            }
        }

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            _logger.LogDebug("File created: {Path}", e.FullPath);
            
            lock (_pendingEvents)
            {
                _pendingEvents.Enqueue(e);
            }
            
            FileAdded?.Invoke(this, e);
        }

        private void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            _logger.LogDebug("File deleted: {Path}", e.FullPath);
            
            lock (_deletedFiles)
            {
                _deletedFiles.Enqueue(e.FullPath);
            }
            
            // Handle deletion and notify gallery after database update
            Task.Run(async () => 
            {
                await HandleFileDeleted(e.FullPath);
                // Notify gallery after database is updated
                _galleryUpdateService.NotifyImageDeleted(e.FullPath);
            });
            
            FileDeleted?.Invoke(this, e);
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            _logger.LogDebug("File renamed: {OldPath} -> {NewPath}", e.OldFullPath, e.FullPath);
            
            // Handle rename and notify gallery after database update
            Task.Run(async () => 
            {
                await HandleFileRenamed(e.OldFullPath, e.FullPath);
                // Notify gallery after database is updated - treat as delete old and add new
                _galleryUpdateService.NotifyImagesChanged(new[] { e.FullPath }, new[] { e.OldFullPath });
            });
            
            FileRenamed?.Invoke(this, e);
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            // We'll treat changes as potential new files to re-scan
            lock (_pendingEvents)
            {
                _pendingEvents.Enqueue(e);
            }
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            _logger.LogError(e.GetException(), "FileSystemWatcher error");
            
            // Try to recreate the watcher
            if (sender is FileSystemWatcher failedWatcher)
            {
                var path = failedWatcher.Path;
                failedWatcher.EnableRaisingEvents = false;
                failedWatcher.Dispose();
                
                Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(_ => 
                {
                    if (Directory.Exists(path))
                    {
                        CreateWatcher(path);
                    }
                });
            }
        }

        private async void ProcessBatchEvents(object? state)
        {
            if (!await _processingLock.WaitAsync(0))
                return;

            try
            {
                List<FileSystemEventArgs> eventsToProcess;
                List<string> deletedFilesToNotify;
                
                lock (_pendingEvents)
                {
                    if (_pendingEvents.Count == 0 && _deletedFiles.Count == 0)
                        return;
                        
                    eventsToProcess = _pendingEvents.ToList();
                    _pendingEvents.Clear();
                    
                    deletedFilesToNotify = _deletedFiles.ToList();
                    _deletedFiles.Clear();
                }

                // Group events by file to avoid duplicates
                var uniqueFiles = eventsToProcess
                    .Where(e => e.ChangeType == WatcherChangeTypes.Created || e.ChangeType == WatcherChangeTypes.Changed)
                    .Select(e => e.FullPath)
                    .Distinct()
                    .Where(path => File.Exists(path) && !_processingFiles.Contains(path))
                    .ToList();

                if (uniqueFiles.Count > 0)
                {
                    foreach (var file in uniqueFiles)
                    {
                        _processingFiles.Add(file);
                    }

                    _logger.LogInformation("Processing {Count} new/changed files", uniqueFiles.Count);
                    
                    // Process files using the photo scan service
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var photoScanService = scope.ServiceProvider.GetRequiredService<IPhotoScanService>();
                        _logger.LogInformation("Starting scan of new files: {Files}", string.Join(", ", uniqueFiles));
                        await photoScanService.ScanSpecificFilesAsync(uniqueFiles);
                        _logger.LogInformation("Completed scan of {Count} files", uniqueFiles.Count);
                    }
                    
                    // Run additional phases on the new files
                    if (uniqueFiles.Count > 0)
                    {
                        using (var additionalScope = _serviceProvider.CreateScope())
                        {
                            await RunAdditionalPhasesOnNewFiles(additionalScope, uniqueFiles, CancellationToken.None);
                        }
                    }

                    foreach (var file in uniqueFiles)
                    {
                        _processingFiles.Remove(file);
                    }
                }
                
                // Notify gallery of changes (only for added files, deletions are handled immediately)
                if (uniqueFiles.Count > 0)
                {
                    _logger.LogInformation("Notifying gallery of {Count} new files", uniqueFiles.Count);
                    _galleryUpdateService.NotifyImagesChanged(uniqueFiles, new List<string>());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing batch events");
            }
            finally
            {
                _processingLock.Release();
            }
        }

        private async Task HandleFileDeleted(string filePath)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                using var context = scope.ServiceProvider.GetRequiredService<MyPhotoHelperDbContext>();
                
                // Load scan directories to determine which directory this file belongs to
                var scanDirectories = await context.tbl_scan_directory.ToListAsync();
                
                // Find which scan directory this file belongs to
                var scanDir = scanDirectories
                    .Where(sd => filePath.StartsWith(sd.DirectoryPath, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(sd => sd.DirectoryPath.Length)
                    .FirstOrDefault();
                    
                if (scanDir == null)
                {
                    _logger.LogWarning("Deleted file {FilePath} is not in any scan directory", filePath);
                    return;
                }
                
                var relativePath = Path.GetRelativePath(scanDir.DirectoryPath, filePath);
                
                // Find the image in the database
                var image = await context.tbl_images
                    .FirstOrDefaultAsync(i => i.RelativePath == relativePath && i.ScanDirectoryId == scanDir.ScanDirectoryId);
                
                if (image != null)
                {
                    _logger.LogInformation("Removing deleted file from database: {Path} (ImageId: {ImageId})", filePath, image.ImageId);
                    
                    // Remove the image and cascade delete related records
                    context.tbl_images.Remove(image);
                    await context.SaveChangesAsync();
                    
                    _logger.LogInformation("Successfully removed image {ImageId} from database", image.ImageId);
                }
                else
                {
                    _logger.LogWarning("Deleted file not found in database: {Path}", filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling file deletion: {Path}", filePath);
            }
        }

        private async Task HandleFileRenamed(string oldPath, string newPath)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                using var context = scope.ServiceProvider.GetRequiredService<MyPhotoHelperDbContext>();
                
                // Load scan directories to determine which directory this file belongs to
                var scanDirectories = await context.tbl_scan_directory.ToListAsync();
                
                // Find which scan directory the old file belonged to
                var scanDir = scanDirectories
                    .Where(sd => oldPath.StartsWith(sd.DirectoryPath, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(sd => sd.DirectoryPath.Length)
                    .FirstOrDefault();
                    
                if (scanDir == null)
                {
                    _logger.LogWarning("Renamed file {OldPath} is not in any scan directory", oldPath);
                    return;
                }
                
                var oldRelativePath = Path.GetRelativePath(scanDir.DirectoryPath, oldPath);
                var newRelativePath = Path.GetRelativePath(scanDir.DirectoryPath, newPath);
                
                // Find the image in the database
                var image = await context.tbl_images
                    .FirstOrDefaultAsync(i => i.RelativePath == oldRelativePath && i.ScanDirectoryId == scanDir.ScanDirectoryId);
                
                if (image != null)
                {
                    _logger.LogInformation("Updating renamed file in database: {OldPath} -> {NewPath}", oldPath, newPath);
                    
                    // Update the relative path and name
                    image.RelativePath = newRelativePath;
                    image.FileName = Path.GetFileName(newPath);
                    
                    await context.SaveChangesAsync();
                }
                else
                {
                    // Treat as a new file if not found
                    using (var innerScope = _serviceProvider.CreateScope())
                    {
                        var photoScanService = innerScope.ServiceProvider.GetRequiredService<IPhotoScanService>();
                        await photoScanService.ScanSpecificFilesAsync(new[] { newPath });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling file rename: {OldPath} -> {NewPath}", oldPath, newPath);
            }
        }

        private async Task RunAdditionalPhasesOnNewFiles(IServiceScope scope, List<string> filePaths, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Running additional scan phases on {Count} new files", filePaths.Count);
                
                var dbContext = scope.ServiceProvider.GetRequiredService<MyPhotoHelperDbContext>();
                var scanDirectories = await dbContext.tbl_scan_directory.ToListAsync(cancellationToken);
                
                // Process each new file
                foreach (var filePath in filePaths)
                {
                    try
                    {
                        // Find which scan directory this file belongs to
                        var scanDir = scanDirectories
                            .Where(sd => filePath.StartsWith(sd.DirectoryPath, StringComparison.OrdinalIgnoreCase))
                            .OrderByDescending(sd => sd.DirectoryPath.Length)
                            .FirstOrDefault();
                            
                        if (scanDir == null) continue;
                        
                        var relativePath = Path.GetRelativePath(scanDir.DirectoryPath, filePath);
                        var image = await dbContext.tbl_images
                            .FirstOrDefaultAsync(i => i.RelativePath == relativePath && i.ScanDirectoryId == scanDir.ScanDirectoryId, cancellationToken);
                            
                        if (image == null) continue;
                        
                        // Phase 3: Screenshot Detection using filename patterns
                        _logger.LogInformation("Running screenshot detection on: {Path}", filePath);
                        
                        var fileName = Path.GetFileName(filePath).ToLower();
                        var isScreenshot = false;
                        var confidence = 0.0;
                        
                        // Check common screenshot patterns
                        var screenshotPatterns = new[] { "screenshot", "screen shot", "screen capture", "screencap", "snip", "screenshot_" };
                        foreach (var pattern in screenshotPatterns)
                        {
                            if (fileName.Contains(pattern))
                            {
                                isScreenshot = true;
                                confidence = 0.95;
                                break;
                            }
                        }
                        
                        // Check metadata for resolution if not detected by filename
                        if (!isScreenshot)
                        {
                            var metadata = await dbContext.tbl_image_metadata
                                .FirstOrDefaultAsync(m => m.ImageId == image.ImageId, cancellationToken);
                                
                            if (metadata != null && metadata.Width.HasValue && metadata.Height.HasValue)
                            {
                                // Common screenshot resolutions
                                var screenshotResolutions = new[]
                                {
                                    (1920, 1080), (2560, 1440), (3840, 2160), (1366, 768),
                                    (1440, 900), (1536, 864), (1280, 720), (1600, 900)
                                };
                                
                                foreach (var (width, height) in screenshotResolutions)
                                {
                                    if (metadata.Width == width && metadata.Height == height)
                                    {
                                        isScreenshot = true;
                                        confidence = 0.80;
                                        break;
                                    }
                                }
                            }
                        }
                        
                        // Create analysis record
                        var existingAnalysis = await dbContext.tbl_image_analysis
                            .FirstOrDefaultAsync(a => a.ImageId == image.ImageId, cancellationToken);
                            
                        if (existingAnalysis == null)
                        {
                            var analysis = new tbl_image_analysis
                            {
                                ImageId = image.ImageId,
                                ImageCategory = isScreenshot ? "screenshot" : "photo",
                                AIAnalyzedAt = DateTime.UtcNow,
                                AIModelUsed = "fast_categorizer",
                                AIAnalysisJson = $"{{\"confidence\": {confidence}, \"method\": \"directory_monitor\"}}",
                                AIDescription = isScreenshot ? "Detected as screenshot" : "Detected as photo"
                            };
                            dbContext.tbl_image_analysis.Add(analysis);
                            
                            _logger.LogInformation("Image categorized as: {Category} with confidence {Confidence}", 
                                analysis.ImageCategory, confidence);
                        }
                        
                        // Phase 4: Hash Calculation
                        _logger.LogInformation("Calculating hash for: {Path}", filePath);
                        var hashService = scope.ServiceProvider.GetRequiredService<IHashCalculationService>();
                        var hash = await hashService.CalculateFileHashAsync(filePath, cancellationToken);
                        
                        if (!string.IsNullOrEmpty(hash))
                        {
                            image.FileHash = hash;
                            _logger.LogInformation("Hash calculated: {Hash}", hash);
                        }
                        
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing file through additional phases: {Path}", filePath);
                    }
                }
                
                _logger.LogInformation("Completed additional phases for new files");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running additional phases on new files");
            }
        }

        public void Dispose()
        {
            StopMonitoring();
            _batchProcessTimer?.Dispose();
            _processingLock?.Dispose();
        }
    }
}