using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyPhotoHelper.Forms;
using MyPhotoHelper.Services;
using MyPhotoHelper.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using CSnakes.Runtime;
using CSnakes.Runtime.PackageManagement;

namespace MyPhotoHelper
{
    public class BlazorServerStarter
    {
        private StartupForm? startupForm;
        private WebApplication? app;
        
        public void Start(string[] args)
        {
            // Check if app should start minimized
            var startMinimized = args.Contains("--minimized");
            
            // Show startup form
            startupForm = new StartupForm();
            if (!startMinimized)
            {
                startupForm.Show();
                Application.DoEvents(); // Process Windows messages
            }
            
            // Start initialization on background thread
            var initTask = Task.Run(async () =>
            {
                try
                {
                    await InitializeAndStartBlazorServer(args, startMinimized);
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex, "Failed to start Blazor server");
                    startupForm?.Invoke(new Action(() =>
                    {
                        ShowError("Startup Failed", ex.Message);
                        Application.Exit();
                    }));
                }
            });
            
            // Run Windows Forms message loop - this keeps the app running
            Application.Run();
        }
        
        private async Task InitializeAndStartBlazorServer(string[] args, bool startMinimized)
        {
            try
            {
                UpdateStatus("Initializing directories...", 10);
                StartupErrorLogger.LogError("Initializing directories", null);
                
                // All the original initialization code from Program.cs goes here
                // Initialize PathService
                var pathService = new PathService();
                Logger.Initialize(pathService.GetLogsDirectory(), MyPhotoHelper.Services.LogLevel.Info);
                Logger.Info("MyPhotoHelper starting (WinForms launcher)");
                
                pathService.EnsureDirectoriesExist();
                pathService.MigrateDatabaseIfNeeded();
                
                UpdateStatus("Creating web application...", 20);
                StartupErrorLogger.LogError("Creating web application", null);
                
                // Create the Blazor web application
                var builder = WebApplication.CreateBuilder(args);
                
                // Configure Kestrel
                builder.WebHost.ConfigureKestrel(serverOptions =>
                {
                    serverOptions.ListenLocalhost(5113, listenOptions =>
                    {
                        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
                    });
                });
                
                UpdateStatus("Configuring services...", 30);
                StartupErrorLogger.LogError("Configuring services", null);
                
                // Configure all the services (logging, EF, Python, etc.)
                ConfigureServices(builder, pathService);
                
                UpdateStatus("Building application...", 40);
                StartupErrorLogger.LogError("Building application", null);
                
                // Build the app
                app = builder.Build();
                
                // Configure the HTTP pipeline
                ConfigureApp(app);
                
                UpdateStatus("Initializing Python...", 50);
                StartupErrorLogger.LogError("Initializing Python", null);
                
                // Initialize Python
                await InitializePython(app);
                
                UpdateStatus("Initializing database...", 70);
                StartupErrorLogger.LogError("Initializing database", null);
            
            // Initialize database
            await InitializeDatabase(app, pathService);
            
            UpdateStatus("Starting system tray...", 85);
            
            // Initialize system tray on UI thread
            if (startupForm?.InvokeRequired == true)
            {
                startupForm.Invoke(new Action(() =>
                {
                    var systemTrayService = app.Services.GetRequiredService<SystemTrayService>();
                    systemTrayService.Initialize();
                }));
            }
            else
            {
                var systemTrayService = app.Services.GetRequiredService<SystemTrayService>();
                systemTrayService.Initialize();
            }
            
            UpdateStatus("Starting web server...", 90);
            
            // Start the Blazor server in background
            _ = Task.Run(async () =>
            {
                try
                {
                    await app.RunAsync();
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex, "Blazor server stopped");
                }
            });
            
            // Wait for server to be ready
            await Task.Delay(2000);
            
            UpdateStatus("Ready!", 100);
            await Task.Delay(500);
            
            // Close startup form and open browser if needed
            if (startupForm?.InvokeRequired == true)
            {
                startupForm.Invoke(new Action(() =>
                {
                    startupForm.Close();
                    startupForm = null;
                    
                    if (!startMinimized)
                    {
                        OpenBrowser();
                    }
                }));
            }
            else
            {
                startupForm?.Close();
                startupForm = null;
                
                if (!startMinimized)
                {
                    OpenBrowser();
                }
            }
            }
            catch (Exception ex)
            {
                StartupErrorLogger.LogError("Fatal error during Blazor server initialization", ex);
                
                // Hide startup form
                startupForm?.Invoke(new Action(() => startupForm.Hide()));
                
                // Show error dialog
                if (startupForm?.InvokeRequired == true)
                {
                    startupForm.Invoke(new Action(() =>
                    {
                        using (var errorForm = new StartupErrorForm("Failed to start MyPhotoHelper server", ex))
                        {
                            errorForm.ShowDialog();
                        }
                        Application.Exit();
                    }));
                }
                else
                {
                    using (var errorForm = new StartupErrorForm("Failed to start MyPhotoHelper server", ex))
                    {
                        errorForm.ShowDialog();
                    }
                    Application.Exit();
                }
            }
        }
        
        private void ConfigureServices(WebApplicationBuilder builder, IPathService pathService)
        {
            // Configure logging
            builder.Logging.ClearProviders();
            builder.Logging.AddDebug();
            builder.Logging.AddEventLog();
            builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
            
            // Configure Python
            var exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var pythonHome = Path.Join(exeDir, "Python");
            var virtualDir = Path.Join(pythonHome, ".venv");
            
            builder.Services
                .WithPython()
                .WithHome(pythonHome)
                .FromRedistributable("3.12")
                .WithVirtualEnvironment(virtualDir)
                .WithUvInstaller();
            
            // Configure Entity Framework
            var dbPath = pathService.GetDatabasePath();
            var connectionString = $"Data Source={dbPath};Cache=Shared;";
            
            // Add services
            builder.Services.AddMemoryCache();
            
            // Use DbContextFactory for both singleton and scoped access
            builder.Services.AddDbContextFactory<MyPhotoHelperDbContext>(options =>
                options.UseSqlite(connectionString, sqliteOptions =>
                {
                    sqliteOptions.CommandTimeout(30);
                })
                .EnableSensitiveDataLogging(builder.Environment.IsDevelopment())
                .EnableDetailedErrors(builder.Environment.IsDevelopment()));
            
            // Register a scoped DbContext that uses the factory (for components that inject DbContext directly)
            builder.Services.AddScoped<MyPhotoHelperDbContext>(provider =>
            {
                var factory = provider.GetRequiredService<IDbContextFactory<MyPhotoHelperDbContext>>();
                return factory.CreateDbContext();
            });
            
            builder.Services.AddSingleton<IPathService>(pathService);
            builder.Services.AddScoped<IDatabaseInitializationService, DatabaseInitializationService>();
            builder.Services.AddSingleton<IDatabaseChangeNotificationService, DatabaseChangeNotificationService>();
            builder.Services.AddSingleton<SystemTrayService>();
            builder.Services.AddHostedService<BackgroundTaskService>();
            builder.Services.AddScoped<ISettingsService, SettingsService>();
            builder.Services.AddScoped<IMemoryService, MemoryService>();
            builder.Services.AddScoped<IPhotoPathService, PhotoPathService>();
            builder.Services.AddScoped<IFolderDialogService, FolderDialogService>();
            builder.Services.AddSingleton<IThumbnailService, ThumbnailService>();
            builder.Services.AddScoped<IPhotoScanService, PhotoScanService>();
            builder.Services.AddSingleton<IScanStatusService, ScanStatusService>();
            builder.Services.AddScoped<IMetadataExtractionService, MetadataExtractionService>();
            builder.Services.AddScoped<IFastImageCategorizationService, FastImageCategorizationService>();
            builder.Services.AddScoped<IScreenshotAnalysisService, ScreenshotAnalysisService>();
            builder.Services.AddScoped<IHashCalculationService, HashCalculationService>();
            builder.Services.AddScoped<IPhasedScanService, PhasedScanService>();
            builder.Services.AddScoped<IDuplicateDetectionService, DuplicateDetectionService>();
            builder.Services.AddScoped<IImageDetailsService, ImageDetailsService>();
            builder.Services.AddSingleton<IThumbnailCacheService, ThumbnailCacheService>();
            builder.Services.AddSingleton<IHeicCacheService>(provider => provider.GetRequiredService<IThumbnailCacheService>());
            builder.Services.AddScoped<IMetadataClassificationService, MetadataClassificationService>();
            builder.Services.AddScoped<IMetadataClassificationTestService, MetadataClassificationTestService>();
            builder.Services.AddScoped<IImageDisplayService, ImageDisplayService>();
            builder.Services.AddScoped<IBackgroundPhotoLoader, BackgroundPhotoLoader>();
            builder.Services.AddScoped<IGalleryStateService, GalleryStateService>();
            
            // Gallery update notification service
            builder.Services.AddSingleton<IGalleryUpdateService, GalleryUpdateService>();
            
            // Directory monitoring service
            builder.Services.AddSingleton<DirectoryMonitoringService>();
            builder.Services.AddSingleton<IDirectoryMonitoringService>(provider => provider.GetRequiredService<DirectoryMonitoringService>());
            builder.Services.AddHostedService(provider => provider.GetRequiredService<DirectoryMonitoringService>());
            
            // Add Blazor services
            builder.Services.AddRazorPages();
            builder.Services.AddServerSideBlazor();
            builder.Services.AddControllers();
        }
        
        private void ConfigureApp(WebApplication app)
        {
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
            }
            
            app.UseStaticFiles();
            app.UseRouting();
            app.MapControllers();
            app.MapBlazorHub();
            app.MapFallbackToPage("/_Host");
            
            // Path-based endpoints removed for security - use ID-based endpoints instead
        }

        private static string GetContentTypeForPath(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".heic" => "image/heic",
                ".heif" => "image/heif",
                _ => "application/octet-stream"
            };
        }
        
        private async Task InitializePython(WebApplication app)
        {
            var exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var pythonHome = Path.Join(exeDir, "Python");
            var requirements = Path.Combine(pythonHome, "requirements.txt");
            
            var pythonEnv = app.Services.GetRequiredService<IPythonEnvironment>();
            Logger.Info("Python environment created");
            
            if (File.Exists(requirements))
            {
                UpdateStatus("Installing Python packages...", 60);
                var installer = app.Services.GetRequiredService<IPythonPackageInstaller>();
                await installer.InstallPackagesFromRequirements(pythonHome);
                Logger.Info("Python packages installed");
            }
        }
        
        private async Task InitializeDatabase(WebApplication app, IPathService pathService)
        {
            try
            {
                var dbPath = pathService.GetDatabasePath();
                var connectionString = $"Data Source={dbPath};Cache=Shared;";
                
                StartupErrorLogger.LogError($"Database path: {dbPath}", null);
                
                using var scope = app.Services.CreateScope();
                var dbInitService = scope.ServiceProvider.GetRequiredService<IDatabaseInitializationService>();
                var dbContext = scope.ServiceProvider.GetRequiredService<MyPhotoHelperDbContext>();
                
                var success = await dbInitService.InitializeDatabaseAsync(connectionString);
                if (!success)
                {
                    throw new Exception("Database initialization failed");
                }
                
                await dbContext.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
                await dbContext.Database.ExecuteSqlRawAsync("PRAGMA synchronous=NORMAL;");
                await dbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;");
                
                Logger.Info("Database initialized successfully");
                StartupErrorLogger.LogError("Database initialized successfully", null);
            }
            catch (Exception ex)
            {
                StartupErrorLogger.LogError("Database initialization error", ex);
                throw; // Re-throw to be caught by the main error handler
            }
        }
        
        private void UpdateStatus(string message, int progress)
        {
            startupForm?.UpdateStatus(message, progress);
            Logger.Info($"Startup: {message} ({progress}%)");
        }
        
        private void ShowError(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        
        private void OpenBrowser()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "http://localhost:5113",
                    UseShellExecute = true
                });
                Logger.Info("Opened browser to Blazor application");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to open browser: {ex.Message}");
            }
        }
    }
}