using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Squirrel;
using Squirrel.Sources;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyPhotoHelper.Services
{
    public class AppUpdateService : BackgroundService
    {
        private readonly ILogger<AppUpdateService> _logger;
        private readonly IConfiguration _configuration;
        private UpdateManager? _updateManager;

        public static event EventHandler<UpdateStatus>? UpdateStatusChanged;

        public AppUpdateService(ILogger<AppUpdateService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Wait 30 seconds after startup before first check
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckForUpdates();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking for updates");
                }

                // Check every 6 hours
                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }
        }

        private async Task CheckForUpdates()
        {
            try
            {
                var updateUrl = _configuration["Updates:GitHubUrl"] ?? "https://github.com/thefrederiksen/MyPhotoHelper";
                
                using (var mgr = new UpdateManager(new GithubSource(updateUrl, "", false)))
                {
                    _updateManager = mgr;
                    
                    var updateInfo = await mgr.CheckForUpdate();
                    
                    if (updateInfo.ReleasesToApply.Count > 0)
                    {
                        _logger.LogInformation($"Found {updateInfo.ReleasesToApply.Count} updates");
                        
                        UpdateStatusChanged?.Invoke(this, new UpdateStatus 
                        { 
                            Status = "Downloading", 
                            Version = updateInfo.FutureReleaseEntry.Version.ToString() 
                        });

                        // Download updates
                        await mgr.DownloadReleases(updateInfo.ReleasesToApply);
                        
                        // Apply updates
                        await mgr.ApplyReleases(updateInfo);
                        
                        UpdateStatusChanged?.Invoke(this, new UpdateStatus 
                        { 
                            Status = "Ready", 
                            Version = updateInfo.FutureReleaseEntry.Version.ToString() 
                        });
                        
                        _logger.LogInformation($"Update ready: v{updateInfo.FutureReleaseEntry.Version}");
                    }
                    else
                    {
                        _logger.LogInformation("No updates available");
                    }
                }
            }
            finally
            {
                _updateManager = null;
            }
        }

        public override void Dispose()
        {
            _updateManager?.Dispose();
            base.Dispose();
        }
    }

    public class UpdateStatus
    {
        public string Status { get; set; } = "";
        public string Version { get; set; } = "";
    }
}