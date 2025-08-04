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
        private readonly IPhotoScanService _photoScanService;
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
            IPhotoScanService photoScanService,
            IGalleryUpdateService galleryUpdateService)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _photoScanService = photoScanService;
            _galleryUpdateService = galleryUpdateService;
            
            // Process batch events every 2 seconds
            _batchProcessTimer = new System.Threading.Timer(ProcessBatchEvents, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
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
            
            Task.Run(async () => await HandleFileDeleted(e.FullPath));
            FileDeleted?.Invoke(this, e);
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            _logger.LogDebug("File renamed: {OldPath} -> {NewPath}", e.OldFullPath, e.FullPath);
            
            Task.Run(async () => await HandleFileRenamed(e.OldFullPath, e.FullPath));
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
                    await _photoScanService.ScanSpecificFilesAsync(uniqueFiles);

                    foreach (var file in uniqueFiles)
                    {
                        _processingFiles.Remove(file);
                    }
                }
                
                // Notify gallery of changes
                if (uniqueFiles.Count > 0 || deletedFilesToNotify.Count > 0)
                {
                    _galleryUpdateService.NotifyImagesChanged(uniqueFiles, deletedFilesToNotify);
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
                    _logger.LogInformation("Removing deleted file from database: {Path}", filePath);
                    
                    // Remove the image and cascade delete related records
                    context.tbl_images.Remove(image);
                    await context.SaveChangesAsync();
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
                    await _photoScanService.ScanSpecificFilesAsync(new[] { newPath });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling file rename: {OldPath} -> {NewPath}", oldPath, newPath);
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