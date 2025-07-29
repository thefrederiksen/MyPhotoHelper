using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyPhotoHelper.Data;
using MyPhotoHelper.Models;

namespace MyPhotoHelper.Services
{
    public class PhotoScanService : IPhotoScanService
    {
        private readonly ILogger<PhotoScanService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IPathService _pathService;
        
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _scanTask;
        private ScanProgress? _currentProgress;
        
        public event EventHandler<ScanProgressEventArgs>? ScanProgressChanged;
        public event EventHandler<ScanCompletedEventArgs>? ScanCompleted;
        
        public bool IsScanning => _scanTask != null && !_scanTask.IsCompleted;
        public ScanProgress? CurrentProgress => _currentProgress;
        
        public PhotoScanService(
            ILogger<PhotoScanService> logger,
            IServiceProvider serviceProvider,
            IPathService pathService)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _pathService = pathService;
        }
        
        public async Task StartScanAsync(CancellationToken cancellationToken = default)
        {
            if (IsScanning)
            {
                _logger.LogWarning("Scan already in progress");
                return;
            }
            
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _scanTask = Task.Run(() => PerformScanAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
            
            await Task.CompletedTask;
        }
        
        public void CancelScan()
        {
            _cancellationTokenSource?.Cancel();
        }
        
        private async Task PerformScanAsync(CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            var totalFilesProcessed = 0;
            var newFilesAdded = 0;
            var errorCount = 0;
            
            try
            {
                _logger.LogInformation("Starting photo scan");
                
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<MyPhotoHelperDbContext>();
                
                // Get scan directories
                var scanDirectories = await dbContext.tbl_scan_directory.ToListAsync(cancellationToken);
                
                if (!scanDirectories.Any())
                {
                    _logger.LogWarning("No scan directories configured");
                    ScanCompleted?.Invoke(this, new ScanCompletedEventArgs
                    {
                        Success = true,
                        TotalFilesProcessed = 0,
                        NewFilesAdded = 0,
                        ErrorCount = 0,
                        Duration = DateTime.UtcNow - startTime,
                        ErrorMessage = "No directories configured for scanning"
                    });
                    return;
                }
                
                _currentProgress = new ScanProgress
                {
                    TotalDirectories = scanDirectories.Count,
                    ProcessedDirectories = 0,
                    StartTime = startTime
                };
                
                // First pass: count total files
                var allFiles = new List<string>();
                foreach (var scanDir in scanDirectories)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                        
                    _currentProgress.CurrentDirectory = scanDir.DirectoryPath;
                    
                    try
                    {
                        if (Directory.Exists(scanDir.DirectoryPath))
                        {
                            var files = GetImageFiles(scanDir.DirectoryPath);
                            allFiles.AddRange(files);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error scanning directory: {scanDir.DirectoryPath}");
                        errorCount++;
                    }
                }
                
                _currentProgress.TotalFiles = allFiles.Count;
                
                // Second pass: process files
                foreach (var scanDir in scanDirectories)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                        
                    _currentProgress.CurrentDirectory = scanDir.DirectoryPath;
                    
                    try
                    {
                        if (Directory.Exists(scanDir.DirectoryPath))
                        {
                            var files = GetImageFiles(scanDir.DirectoryPath);
                            
                            foreach (var file in files)
                            {
                                if (cancellationToken.IsCancellationRequested)
                                    break;
                                    
                                _currentProgress.CurrentFile = file;
                                _currentProgress.ProcessedFiles++;
                                
                                try
                                {
                                    // Calculate relative path
                                    var relativePath = Path.GetRelativePath(scanDir.DirectoryPath, file);
                                    
                                    // Check if file already exists in database
                                    var existingImage = await dbContext.tbl_images
                                        .FirstOrDefaultAsync(i => i.RelativePath == relativePath && i.ScanDirectoryId == scanDir.ScanDirectoryId, cancellationToken);
                                        
                                    if (existingImage == null)
                                    {
                                        // Add new image
                                        var fileInfo = new FileInfo(file);
                                        var image = new tbl_images
                                        {
                                            RelativePath = relativePath,
                                            FileName = fileInfo.Name,
                                            FileExtension = fileInfo.Extension,
                                            FileHash = "", // Will be calculated later if needed
                                            FileSizeBytes = (int)Math.Min(fileInfo.Length, int.MaxValue),
                                            DateCreated = DateTime.UtcNow,
                                            DateModified = fileInfo.LastWriteTime,
                                            FileExists = 1,
                                            IsDeleted = 0,
                                            ScanDirectoryId = scanDir.ScanDirectoryId
                                        };
                                        
                                        dbContext.tbl_images.Add(image);
                                        await dbContext.SaveChangesAsync(cancellationToken);
                                        newFilesAdded++;
                                    }
                                    
                                    totalFilesProcessed++;
                                    
                                    // Report progress
                                    if (totalFilesProcessed % 10 == 0) // Report every 10 files
                                    {
                                        ReportProgress();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, $"Error processing file: {file}");
                                    errorCount++;
                                    _currentProgress.ErrorCount = errorCount;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error processing directory: {scanDir.DirectoryPath}");
                        errorCount++;
                    }
                    
                    _currentProgress.ProcessedDirectories++;
                    ReportProgress();
                }
                
                // Final progress report
                ReportProgress();
                
                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation($"Photo scan completed. Processed: {totalFilesProcessed}, New: {newFilesAdded}, Errors: {errorCount}, Duration: {duration}");
                
                ScanCompleted?.Invoke(this, new ScanCompletedEventArgs
                {
                    Success = true,
                    TotalFilesProcessed = totalFilesProcessed,
                    NewFilesAdded = newFilesAdded,
                    ErrorCount = errorCount,
                    Duration = duration
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Photo scan failed");
                
                ScanCompleted?.Invoke(this, new ScanCompletedEventArgs
                {
                    Success = false,
                    TotalFilesProcessed = totalFilesProcessed,
                    NewFilesAdded = newFilesAdded,
                    ErrorCount = errorCount,
                    Duration = DateTime.UtcNow - startTime,
                    ErrorMessage = ex.Message
                });
            }
            finally
            {
                _currentProgress = null;
                _cancellationTokenSource = null;
                _scanTask = null;
            }
        }
        
        private void ReportProgress()
        {
            if (_currentProgress != null)
            {
                ScanProgressChanged?.Invoke(this, new ScanProgressEventArgs
                {
                    TotalDirectories = _currentProgress.TotalDirectories,
                    ProcessedDirectories = _currentProgress.ProcessedDirectories,
                    TotalFiles = _currentProgress.TotalFiles,
                    ProcessedFiles = _currentProgress.ProcessedFiles,
                    CurrentDirectory = _currentProgress.CurrentDirectory,
                    CurrentFile = _currentProgress.CurrentFile,
                    ErrorCount = _currentProgress.ErrorCount
                });
            }
        }
        
        private List<string> GetImageFiles(string directory)
        {
            var extensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".heic" };
            var files = new List<string>();
            
            foreach (var ext in extensions)
            {
                try
                {
                    files.AddRange(Directory.GetFiles(directory, $"*{ext}", SearchOption.AllDirectories));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Error getting files with extension {ext} in {directory}");
                }
            }
            
            return files;
        }
    }
}