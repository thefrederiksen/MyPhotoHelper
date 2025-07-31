using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyPhotoHelper.Models;

namespace MyPhotoHelper.Services
{
    public class BackgroundTaskService : BackgroundService
    {
        private readonly ILogger<BackgroundTaskService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IScanStatusService _scanStatusService;
        private System.Threading.Timer? _scanTimer;

        public BackgroundTaskService(
            ILogger<BackgroundTaskService> logger,
            IServiceProvider serviceProvider,
            IScanStatusService scanStatusService)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _scanStatusService = scanStatusService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Background task service started");

            // Wait a bit for the application to fully start
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            // Perform initial scan on startup
            await PerformBackgroundScan();

            // Start periodic scanning (every hour)
            _scanTimer = new System.Threading.Timer(
                callback: async _ => await PerformBackgroundScan(),
                state: null,
                dueTime: TimeSpan.FromHours(1), // First periodic scan after 1 hour
                period: TimeSpan.FromHours(1)); // Then every hour

            // Keep the service running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }

        private async Task PerformBackgroundScan()
        {
            try
            {
                _logger.LogInformation("Starting background photo scan");
                
                using var scope = _serviceProvider.CreateScope();
                var phasedScanService = scope.ServiceProvider.GetRequiredService<IPhasedScanService>();
                
                // Subscribe to progress events
                void OnProgressChanged(object? sender, PhasedScanProgress progress)
                {
                    _scanStatusService.UpdatePhasedStatus(progress);
                    _logger.LogInformation($"Phased scan progress - Phase: {progress.CurrentPhase}");
                }
                
                void OnPhaseCompleted(object? sender, ScanPhase phase)
                {
                    _logger.LogInformation($"Phase {phase} completed");
                    
                    if (phase == ScanPhase.Phase1_Discovery)
                    {
                        var scanResult = new ScanCompletedEventArgs
                        {
                            Success = true,
                            TotalFilesProcessed = 0, // Will be updated from progress
                            NewFilesAdded = 0,
                            ErrorCount = 0,
                            Duration = TimeSpan.Zero
                        };
                        _scanStatusService.UpdateLastScan(DateTime.UtcNow, scanResult);
                    }
                }
                
                phasedScanService.ProgressChanged += OnProgressChanged;
                phasedScanService.PhaseCompleted += OnPhaseCompleted;
                
                try
                {
                    await phasedScanService.StartPhasedScanAsync();
                    
                    // Wait for scan to complete
                    while (phasedScanService.IsScanning)
                    {
                        await Task.Delay(1000);
                    }
                }
                finally
                {
                    phasedScanService.ProgressChanged -= OnProgressChanged;
                    phasedScanService.PhaseCompleted -= OnPhaseCompleted;
                }
                
                _logger.LogInformation("Background phased scan completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during background scan");
                _scanStatusService.UpdateStatus(false);
            }
        }
        
        public async Task TriggerScanAsync()
        {
            _logger.LogInformation("Manual scan triggered");
            await PerformBackgroundScan();
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Background task service stopping");
            
            _scanTimer?.Change(Timeout.Infinite, 0);
            _scanTimer?.Dispose();
            
            await base.StopAsync(cancellationToken);
        }
    }
}