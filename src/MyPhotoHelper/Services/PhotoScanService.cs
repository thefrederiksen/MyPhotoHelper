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
                
                // Load all existing images into memory for fast lookups
                var existingImagesLookup = new Dictionary<(string relativePath, int scanDirId), tbl_images>();
                
                _logger.LogInformation("Loading existing images from database...");
                var existingImages = await dbContext.tbl_images
                    .Where(img => scanDirectories.Select(sd => sd.ScanDirectoryId).Contains(img.ScanDirectoryId))
                    .ToListAsync(cancellationToken);
                
                foreach (var img in existingImages)
                {
                    existingImagesLookup[(img.RelativePath, img.ScanDirectoryId)] = img;
                }
                _logger.LogInformation($"Loaded {existingImages.Count} existing images into memory");
                
                // Batch for new images
                var newImagesBatch = new List<tbl_images>();
                const int BATCH_SIZE = 500;
                
                // Single-pass approach: collect all files while processing
                var totalFileCount = 0;
                
                // Process each scan directory
                foreach (var scanDir in scanDirectories)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                        
                    _currentProgress.CurrentDirectory = scanDir.DirectoryPath;
                    
                    try
                    {
                        if (Directory.Exists(scanDir.DirectoryPath))
                        {
                            var files = GetImageFiles(scanDir.DirectoryPath).ToList();
                            
                            // Update total file count as we go
                            totalFileCount += files.Count;
                            _currentProgress.TotalFiles = totalFileCount;
                            
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
                                    
                                    // Check if file already exists using in-memory lookup (O(1) instead of O(n))
                                    var lookupKey = (relativePath, scanDir.ScanDirectoryId);
                                    
                                    if (!existingImagesLookup.ContainsKey(lookupKey))
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
                                        
                                        newImagesBatch.Add(image);
                                        newFilesAdded++;
                                        
                                        // Save in batches for better performance
                                        if (newImagesBatch.Count >= BATCH_SIZE)
                                        {
                                            dbContext.tbl_images.AddRange(newImagesBatch);
                                            await dbContext.SaveChangesAsync(cancellationToken);
                                            
                                            // Add new images to lookup to avoid duplicates within this scan
                                            foreach (var newImg in newImagesBatch)
                                            {
                                                existingImagesLookup[(newImg.RelativePath, newImg.ScanDirectoryId)] = newImg;
                                            }
                                            
                                            newImagesBatch.Clear();
                                            _logger.LogDebug($"Saved batch of {BATCH_SIZE} images");
                                        }
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
                
                // Save any remaining images in the batch
                if (newImagesBatch.Count > 0)
                {
                    dbContext.tbl_images.AddRange(newImagesBatch);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    _logger.LogDebug($"Saved final batch of {newImagesBatch.Count} images");
                }
                
                // Final progress report
                ReportProgress();
                
                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation($"Photo scan completed. Processed: {totalFilesProcessed}, New: {newFilesAdded}, Errors: {errorCount}, Duration: {duration}");
                
                // Trigger metadata extraction for new images
                if (newFilesAdded > 0)
                {
                    _logger.LogInformation("Starting metadata extraction for new images...");
                    try
                    {
                        var metadataService = scope.ServiceProvider.GetRequiredService<IMetadataExtractionService>();
                        await metadataService.ExtractMetadataForNewImagesAsync(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during metadata extraction");
                    }
                }
                
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
        
        private IEnumerable<string> GetImageFiles(string directory)
        {
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { 
                ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".heic" 
            };
            
            // Use EnumerateFiles for better memory efficiency with large directories
            return Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
                .Where(file => 
                {
                    try
                    {
                        var ext = Path.GetExtension(file);
                        return extensions.Contains(ext);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Error checking file extension for {file}");
                        return false;
                    }
                });
        }
    }
}