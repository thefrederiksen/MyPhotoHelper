using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyPhotoHelper.Forms;
using MyPhotoHelper.Services;
using Squirrel;
using System.Diagnostics;

namespace MyPhotoHelper
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // Handle Squirrel events first
            try
            {
                SquirrelAwareApp.HandleEvents(
                    onInitialInstall: OnAppInstall,
                    onAppUninstall: OnAppUninstall,
                    onEveryRun: OnAppRun
                );
            }
            catch (Exception ex)
            {
                // Log but don't crash if Squirrel fails
                StartupErrorLogger.LogError("Squirrel initialization failed", ex);
            }

            // Set up global exception handlers FIRST
            AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
            Application.ThreadException += HandleThreadException;
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            try
            {
                StartupErrorLogger.LogError("Application starting", null);

                // Check for GPS test mode
                if (args.Length > 0 && args[0] == "--test-gps")
                {
                    TestGPS.RunTest();
                    return;
                }

                // Check for single instance
                const string appGuid = "MyPhotoHelper-{8F6F0AC4-B9A1-45fd-A8CF-72F04E6BDE8F}";
                using (Mutex mutex = new Mutex(true, appGuid, out bool createdNew))
                {
                    if (!createdNew)
                    {
                        // Application is already running - open browser and exit
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "http://localhost:5113",
                                UseShellExecute = true
                            });
                        }
                        catch
                        {
                            // If browser fails to open, at least show a message
                            MessageBox.Show("MyPhotoHelper is already running. Please check your system tray.", 
                                "MyPhotoHelper", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        return;
                    }
                    
                    // First instance - continue with normal startup
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    
                    StartupErrorLogger.LogError("Starting Blazor server", null);
                    
                    // Start the Blazor server with a progress window
                    var starter = new BlazorServerStarter();
                    starter.Start(args);
                    
                    // Keep mutex alive for the lifetime of the application
                    GC.KeepAlive(mutex);
                }
            }
            catch (Exception ex)
            {
                StartupErrorLogger.LogError("Fatal error during startup", ex);
                ShowStartupError("A fatal error occurred during application startup.", ex);
            }
        }

        private static void HandleUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            StartupErrorLogger.LogError("Unhandled exception in AppDomain", ex);
            
            if (e.IsTerminating)
            {
                ShowStartupError("An unhandled error occurred and the application must close.", ex);
            }
        }

        private static void HandleThreadException(object sender, ThreadExceptionEventArgs e)
        {
            StartupErrorLogger.LogError("Unhandled thread exception", e.Exception);
            ShowStartupError("An unhandled error occurred in the application.", e.Exception);
        }

        private static void ShowStartupError(string message, Exception? exception)
        {
            try
            {
                using (var errorForm = new StartupErrorForm(message, exception))
                {
                    errorForm.ShowDialog();
                }
            }
            catch
            {
                // If even the error form fails, show basic message box
                var basicMessage = $"{message}\n\nError: {exception?.Message ?? "Unknown error"}\n\nLog file: {StartupErrorLogger.GetLogPath()}";
                MessageBox.Show(basicMessage, "MyPhotoHelper - Startup Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            
            Environment.Exit(1);
        }

        // Squirrel event handlers
        private static void OnAppInstall(SemanticVersion version, IAppTools tools)
        {
            tools.CreateShortcutForThisExe(ShortcutLocation.Desktop | ShortcutLocation.StartMenu);
            tools.CreateUninstallerRegistryEntry();
        }

        private static void OnAppUninstall(SemanticVersion version, IAppTools tools)
        {
            tools.RemoveShortcutForThisExe(ShortcutLocation.Desktop | ShortcutLocation.StartMenu);
            tools.RemoveUninstallerRegistryEntry();
        }

        private static void OnAppRun(SemanticVersion version, IAppTools tools, bool firstRun)
        {
            tools.SetProcessAppUserModelId();
            
            if (firstRun)
            {
                // First run after install
                MessageBox.Show(
                    "Welcome to MyPhotoHelper!\n\nThe application will help you organize and manage your photos with AI-powered features.", 
                    "Welcome", 
                    MessageBoxButtons.OK, 
                    MessageBoxIcon.Information);
            }

            // Start update check in background
            Task.Run(async () => await CheckForUpdates());
        }

        private static async Task CheckForUpdates()
        {
            try
            {
                using (var mgr = await UpdateManager.GitHubUpdateManager("https://github.com/thefrederiksen/MyPhotoHelper"))
                {
                    var updateInfo = await mgr.CheckForUpdate();
                    
                    if (updateInfo.ReleasesToApply.Count > 0)
                    {
                        // Download updates in background
                        await mgr.DownloadReleases(updateInfo.ReleasesToApply);
                        
                        // Apply updates (will be installed on restart)
                        await mgr.ApplyReleases(updateInfo);
                        
                        // Notify user - you might want to do this through your UI instead
                        Debug.WriteLine($"Update downloaded: v{updateInfo.FutureReleaseEntry.Version}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Don't crash the app if update check fails
                Debug.WriteLine($"Update check failed: {ex.Message}");
            }
        }
    }
}