using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyPhotoHelper.Models;

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

                // Phase 1: Discovery
                await ExecutePhase1DiscoveryAsync(cancellationToken);
                if (cancellationToken.IsCancellationRequested) return;

                // Phase 2: Hashing
                await ExecutePhase2HashingAsync(cancellationToken);
                if (cancellationToken.IsCancellationRequested) return;

                // Phase 3: Metadata (already implemented)
                await ExecutePhase3MetadataAsync(cancellationToken);
                if (cancellationToken.IsCancellationRequested) return;

                // Phase 4: AI Analysis (future)
                // await ExecutePhase4AnalysisAsync(cancellationToken);

                _currentProgress.CurrentPhase = ScanPhase.Completed;
                _currentProgress.EndTime = DateTime.UtcNow;
                _currentProgress.IsRunning = false;

                _logger.LogInformation("Phased scan completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Phased scan failed");
                _currentProgress.IsRunning = false;
            }
            finally
            {
                ProgressChanged?.Invoke(this, _currentProgress);
                _scanStatusService.UpdatePhasedStatus(_currentProgress);
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
        }

        private async Task ExecutePhase2HashingAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Phase 2: Hashing");
            _currentProgress!.CurrentPhase = ScanPhase.Phase2_Hashing;
            
            using var scope = _serviceProvider.CreateScope();
            var hashService = scope.ServiceProvider.GetRequiredService<IHashCalculationService>();
            
            var progressReporter = new Progress<PhaseProgress>(progress =>
            {
                _currentProgress.PhaseProgress[ScanPhase.Phase2_Hashing] = progress;
                ProgressChanged?.Invoke(this, _currentProgress);
                _scanStatusService.UpdatePhasedStatus(_currentProgress);
            });

            await hashService.CalculateHashesForImagesAsync(progressReporter, cancellationToken);
            
            PhaseCompleted?.Invoke(this, ScanPhase.Phase2_Hashing);
            _logger.LogInformation("Phase 2 completed");
        }

        private async Task ExecutePhase3MetadataAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Phase 3: Image Details Extraction");
            _currentProgress!.CurrentPhase = ScanPhase.Phase3_Metadata;
            
            using var scope = _serviceProvider.CreateScope();
            var metadataService = scope.ServiceProvider.GetRequiredService<IMetadataExtractionService>();
            
            var progressReporter = new Progress<PhaseProgress>(progress =>
            {
                _currentProgress.PhaseProgress[ScanPhase.Phase3_Metadata] = progress;
                ProgressChanged?.Invoke(this, _currentProgress);
                _scanStatusService.UpdatePhasedStatus(_currentProgress);
            });

            await metadataService.ExtractMetadataForNewImagesAsync(progressReporter, cancellationToken);
            
            PhaseCompleted?.Invoke(this, ScanPhase.Phase3_Metadata);
            _logger.LogInformation("Phase 3 completed");
        }
    }
}