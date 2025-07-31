using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyPhotoHelper.Forms;
using MyPhotoHelper.Services;

namespace MyPhotoHelper
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
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
    }
}