using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyPhotoHelper.Models;
using MyPhotoHelper.Data;
using Microsoft.EntityFrameworkCore;

namespace MyPhotoHelper.Services
{
    public interface IPhasedScanService
    {
        Task StartPhasedScanAsync(CancellationToken cancellationToken = default);
        void CancelScan();
        PhasedScanProgress? CurrentProgress { get; }
        bool IsScanning { get; }
        event EventHandler<PhasedScanProgress>? ProgressChanged;
        event EventHandler<ScanPhase>? PhaseCompleted;
    }

    public class PhasedScanService : IPhasedScanService
    {
        private readonly ILogger<PhasedScanService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IScanStatusService _scanStatusService;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _scanTask;
        private PhasedScanProgress? _currentProgress;

        public PhasedScanProgress? CurrentProgress => _currentProgress;
        public bool IsScanning => _scanTask != null && !_scanTask.IsCompleted;

        public event EventHandler<PhasedScanProgress>? ProgressChanged;
        public event EventHandler<ScanPhase>? PhaseCompleted;

        public PhasedScanService(
            ILogger<PhasedScanService> logger,
            IServiceProvider serviceProvider,
            IScanStatusService scanStatusService)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _scanStatusService = scanStatusService;
        }

        public async Task StartPhasedScanAsync(CancellationToken cancellationToken = default)
        {
            if (IsScanning)
            {
                _logger.LogWarning("Phased scan already in progress");
                return;
            }

            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _scanTask = Task.Run(() => PerformPhasedScanAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
            
            await Task.CompletedTask;
        }

        public void CancelScan()
        {
            _cancellationTokenSource?.Cancel();
        }

        private async Task PerformPhasedScanAsync(CancellationToken cancellationToken)
        {
            _currentProgress = new PhasedScanProgress
            {
                StartTime = DateTime.UtcNow,
                IsRunning = true
            };

            try
            {
                _logger.LogInformation("Starting phased scan");

                // Log if no directories configured but continue anyway
                using (var scope = _serviceProvider.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<MyPhotoHelperDbContext>();
                    var hasDirectories = await dbContext.tbl_scan_directory.AnyAsync();
                    if (!hasDirectories)
                    {
                        _logger.LogWarning("No scan directories configured - scan will complete with no results");
                    }
                }

                // Phase 1: Discovery
                _logger.LogInformation("=== PHASE 1: DISCOVERY - STARTING ===");
                var phase1Success = await ExecutePhaseWithErrorHandling(
                    ScanPhase.Phase1_Discovery,
                    ExecutePhase1DiscoveryAsync,
                    cancellationToken);
                    
                if (!phase1Success || cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Scan stopped after Phase 1");
                    return;
                }

                // Phase 2: Metadata - only if Phase 1 found images
                if (await HasImagesToProcess())
                {
                    _logger.LogInformation("=== PHASE 2: METADATA - STARTING ===");
                    var phase2Success = await ExecutePhaseWithErrorHandling(
                        ScanPhase.Phase2_Metadata,
                        ExecutePhase2MetadataAsync,
                        cancellationToken);
                        
                    if (!phase2Success || cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogWarning("Scan stopped after Phase 2");
                        return;
                    }

                    // Phase 3: Screenshot Detection - only if Phase 2 succeeded
                    _logger.LogInformation("=== PHASE 3: SCREENSHOT DETECTION - STARTING ===");
                    var phase3Success = await ExecutePhaseWithErrorHandling(
                        ScanPhase.Phase3_ScreenshotDetection,
                        ExecutePhase3ScreenshotDetectionAsync,
                        cancellationToken);
                        
                    if (!phase3Success || cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogWarning("Scan stopped after Phase 3");
                        return;
                    }

                    // Phase 4: Hashing - only if Phase 3 succeeded
                    _logger.LogInformation("=== PHASE 4: HASHING - STARTING ===");
                    var phase4Success = await ExecutePhaseWithErrorHandling(
                        ScanPhase.Phase4_Hashing,
                        ExecutePhase4HashingAsync,
                        cancellationToken);
                        
                    if (!phase4Success || cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogWarning("Scan stopped after Phase 4");
                        return;
                    }
                }
                else
                {
                    _logger.LogInformation("No images to process - skipping Phases 2, 3, and 4");
                }

                // Phase 5: AI Analysis (future)
                // await ExecutePhase5AnalysisAsync(cancellationToken);

                _currentProgress.CurrentPhase = ScanPhase.Completed;
                _currentProgress.EndTime = DateTime.UtcNow;
                _currentProgress.IsRunning = false;

                _logger.LogInformation("Phased scan completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Phased scan failed");
                _currentProgress.IsRunning = false;
                _currentProgress.CurrentPhase = ScanPhase.Failed;
                _currentProgress.EndTime = DateTime.UtcNow;
            }
            finally
            {
                ProgressChanged?.Invoke(this, _currentProgress);
                _scanStatusService.UpdatePhasedStatus(_currentProgress);
            }
        }
        
        // Removed ValidatePrerequisites - we always run scans now
        
        private async Task<bool> HasImagesToProcess()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MyPhotoHelperDbContext>();
            
            return await dbContext.tbl_images.AnyAsync();
        }
        
        private async Task<bool> ExecutePhaseWithErrorHandling(
            ScanPhase phase,
            Func<CancellationToken, Task> phaseExecution,
            CancellationToken cancellationToken)
        {
            try
            {
                await phaseExecution(cancellationToken);
                _logger.LogInformation($"=== {phase} - COMPLETED ===");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Phase {phase} failed");
                _currentProgress!.PhaseProgress[phase].ErrorCount++;
                _currentProgress.PhaseProgress[phase].EndTime = DateTime.UtcNow;
                return false;
            }
        }

        private async Task ExecutePhase1DiscoveryAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Phase 1: Discovery");
            _currentProgress!.CurrentPhase = ScanPhase.Phase1_Discovery;
            
            using var scope = _serviceProvider.CreateScope();
            var photoScanService = scope.ServiceProvider.GetRequiredService<IPhotoScanService>();
            
            var phaseProgress = _currentProgress.PhaseProgress[ScanPhase.Phase1_Discovery];
            phaseProgress.StartTime = DateTime.UtcNow;

            // Set up progress tracking
            void OnScanProgress(object? sender, ScanProgressEventArgs e)
            {
                phaseProgress.TotalItems = e.TotalFiles;
                phaseProgress.ProcessedItems = e.ProcessedFiles;
                phaseProgress.CurrentItem = Path.GetFileName(e.CurrentFile);
                phaseProgress.ErrorCount = e.ErrorCount;
                
                ProgressChanged?.Invoke(this, _currentProgress!);
            }

            void OnScanCompleted(object? sender, ScanCompletedEventArgs e)
            {
                phaseProgress.SuccessCount = e.NewFilesAdded;
                phaseProgress.EndTime = DateTime.UtcNow;
                
                PhaseCompleted?.Invoke(this, ScanPhase.Phase1_Discovery);
            }

            photoScanService.ScanProgressChanged += OnScanProgress;
            photoScanService.ScanCompleted += OnScanCompleted;

            try
            {
                await photoScanService.StartScanAsync(cancellationToken);
                
                // Wait for completion
                while (photoScanService.IsScanning && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(500, cancellationToken);
                }
            }
            finally
            {
                photoScanService.ScanProgressChanged -= OnScanProgress;
                photoScanService.ScanCompleted -= OnScanCompleted;
            }

            _logger.LogInformation($"Phase 1 completed. Found {phaseProgress.SuccessCount} new images");
            _logger.LogInformation($"Phase 1 EndTime: {phaseProgress.EndTime}");
        }

        private async Task ExecutePhase2MetadataAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Phase 2: Image Details Extraction");
            _logger.LogInformation($"Cancellation requested: {cancellationToken.IsCancellationRequested}");
            _currentProgress!.CurrentPhase = ScanPhase.Phase2_Metadata;
            
            using var scope = _serviceProvider.CreateScope();
            var metadataService = scope.ServiceProvider.GetRequiredService<IMetadataExtractionService>();
            
            // Throttle progress updates to reduce UI overhead
            var lastProgressUpdate = DateTime.UtcNow;
            var updateInterval = TimeSpan.FromSeconds(1);
            
            var progressReporter = new Progress<PhaseProgress>(progress =>
            {
                _currentProgress.PhaseProgress[ScanPhase.Phase2_Metadata] = progress;
                
                // Only send updates every second, unless it's the final update
                var now = DateTime.UtcNow;
                if (progress.EndTime.HasValue || now - lastProgressUpdate >= updateInterval)
                {
                    lastProgressUpdate = now;
                    ProgressChanged?.Invoke(this, _currentProgress);
                    _scanStatusService.UpdatePhasedStatus(_currentProgress);
                }
            });

            await metadataService.ExtractMetadataForNewImagesAsync(progressReporter, cancellationToken);
            
            PhaseCompleted?.Invoke(this, ScanPhase.Phase2_Metadata);
            _logger.LogInformation("Phase 2 completed");
        }

        private async Task ExecutePhase3ScreenshotDetectionAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Phase 3: Screenshot Detection");
            _logger.LogInformation($"Cancellation requested: {cancellationToken.IsCancellationRequested}");
            _currentProgress!.CurrentPhase = ScanPhase.Phase3_ScreenshotDetection;
            
            using var scope = _serviceProvider.CreateScope();
            var fastScreenshotService = scope.ServiceProvider.GetRequiredService<IFastScreenshotDetectionService>();
            
            // Throttle progress updates to reduce UI overhead
            var lastProgressUpdate = DateTime.UtcNow;
            var updateInterval = TimeSpan.FromSeconds(1);
            
            var progressReporter = new Progress<PhaseProgress>(progress =>
            {
                _currentProgress.PhaseProgress[ScanPhase.Phase3_ScreenshotDetection] = progress;
                
                // Only send updates every second, unless it's the final update
                var now = DateTime.UtcNow;
                if (progress.EndTime.HasValue || now - lastProgressUpdate >= updateInterval)
                {
                    lastProgressUpdate = now;
                    ProgressChanged?.Invoke(this, _currentProgress);
                    _scanStatusService.UpdatePhasedStatus(_currentProgress);
                }
            });

            await fastScreenshotService.DetectScreenshotsWithQueriesAsync(progressReporter, cancellationToken);
            
            PhaseCompleted?.Invoke(this, ScanPhase.Phase3_ScreenshotDetection);
            _logger.LogInformation("Phase 3 completed");
        }

        private async Task ExecutePhase4HashingAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Phase 4: Hashing for Duplicate Detection");
            _currentProgress!.CurrentPhase = ScanPhase.Phase4_Hashing;
            
            using var scope = _serviceProvider.CreateScope();
            var hashService = scope.ServiceProvider.GetRequiredService<IHashCalculationService>();
            
            // Throttle progress updates to reduce UI overhead
            var lastProgressUpdate = DateTime.UtcNow;
            var updateInterval = TimeSpan.FromSeconds(1);
            
            var progressReporter = new Progress<PhaseProgress>(progress =>
            {
                _currentProgress.PhaseProgress[ScanPhase.Phase4_Hashing] = progress;
                
                // Only send updates every second, unless it's the final update
                var now = DateTime.UtcNow;
                if (progress.EndTime.HasValue || now - lastProgressUpdate >= updateInterval)
                {
                    lastProgressUpdate = now;
                    ProgressChanged?.Invoke(this, _currentProgress);
                    _scanStatusService.UpdatePhasedStatus(_currentProgress);
                }
            });

            await hashService.CalculateHashesForImagesAsync(progressReporter, cancellationToken);
            
            PhaseCompleted?.Invoke(this, ScanPhase.Phase4_Hashing);
            _logger.LogInformation("Phase 4 completed");
        }
    }
}