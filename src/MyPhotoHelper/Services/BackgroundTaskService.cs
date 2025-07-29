using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

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
                var photoScanService = scope.ServiceProvider.GetRequiredService<IPhotoScanService>();
                
                // Subscribe to progress events
                void OnProgressChanged(object? sender, ScanProgressEventArgs e)
                {
                    _scanStatusService.UpdateStatus(true, new ScanProgress
                    {
                        TotalDirectories = e.TotalDirectories,
                        ProcessedDirectories = e.ProcessedDirectories,
                        TotalFiles = e.TotalFiles,
                        ProcessedFiles = e.ProcessedFiles,
                        CurrentDirectory = e.CurrentDirectory,
                        CurrentFile = e.CurrentFile,
                        ErrorCount = e.ErrorCount,
                        StartTime = DateTime.UtcNow
                    });
                }
                
                void OnScanCompleted(object? sender, ScanCompletedEventArgs e)
                {
                    _scanStatusService.UpdateStatus(false);
                    _scanStatusService.UpdateLastScan(DateTime.UtcNow, e);
                    _logger.LogInformation($"Background scan completed. New files: {e.NewFilesAdded}, Errors: {e.ErrorCount}");
                }
                
                photoScanService.ScanProgressChanged += OnProgressChanged;
                photoScanService.ScanCompleted += OnScanCompleted;
                
                try
                {
                    await photoScanService.StartScanAsync();
                    
                    // Wait for scan to complete
                    while (photoScanService.IsScanning)
                    {
                        await Task.Delay(1000);
                    }
                }
                finally
                {
                    photoScanService.ScanProgressChanged -= OnProgressChanged;
                    photoScanService.ScanCompleted -= OnScanCompleted;
                }
                
                _logger.LogInformation("Background photo scan completed");
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