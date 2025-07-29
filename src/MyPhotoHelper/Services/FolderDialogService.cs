using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace MyPhotoHelper.Services
{
    public class FolderDialogService : IFolderDialogService
    {
        private readonly ILogger<FolderDialogService> _logger;
        
        public FolderDialogService(ILogger<FolderDialogService> logger)
        {
            _logger = logger;
        }
        
        public Task<string?> OpenFolderDialogAsync(string? initialDirectory = null)
        {
            _logger.LogInformation("OpenFolderDialogAsync called with initial directory: {InitialDirectory}", initialDirectory);
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    // Try to use Control.Invoke if we have a form available
                    var mainForm = System.Windows.Forms.Application.OpenForms.Cast<System.Windows.Forms.Form>().FirstOrDefault();
                    
                    if (mainForm != null)
                    {
                        _logger.LogInformation($"Found main form, InvokeRequired: {mainForm.InvokeRequired}");
                        var tcs = new TaskCompletionSource<string?>();
                        
                        if (mainForm.InvokeRequired)
                        {
                            mainForm.BeginInvoke(new Action(() =>
                            {
                                try
                                {
                                    ShowDialogCore(initialDirectory, tcs);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error in BeginInvoke");
                                    tcs.SetException(ex);
                                }
                            }));
                        }
                        else
                        {
                            // Already on UI thread
                            ShowDialogCore(initialDirectory, tcs);
                        }
                        
                        return tcs.Task;
                    }
                    else
                    {
                        // Fallback to STA thread
                        _logger.LogInformation("No main form available, using STA thread");
                        var tcs = new TaskCompletionSource<string?>();
                        
                        var thread = new Thread(() =>
                        {
                            try
                            {
                                ShowDialogCore(initialDirectory, tcs);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error showing folder dialog on STA thread");
                                tcs.SetException(ex);
                            }
                        });
                        
                        thread.SetApartmentState(ApartmentState.STA);
                        thread.Start();
                        
                        return tcs.Task;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in OpenFolderDialogAsync");
                    return Task.FromResult<string?>(null);
                }
            }
            
            _logger.LogWarning("Not running on Windows platform");
            // For non-Windows platforms, return null
            return Task.FromResult<string?>(null);
        }
        
        private void ShowDialogCore(string? initialDirectory, TaskCompletionSource<string?> tcs)
        {
            try
            {
                _logger.LogInformation("Creating FolderBrowserDialog");
                
                using var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Select a folder containing your photos",
                    UseDescriptionForTitle = true,
                    ShowNewFolderButton = false
                };

                if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
                {
                    dialog.SelectedPath = initialDirectory;
                }
                else
                {
                    // Default to My Pictures
                    dialog.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                }

                _logger.LogInformation("Showing dialog...");
                var result = dialog.ShowDialog();
                
                var selectedPath = result == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
                _logger.LogInformation("Dialog result: {Result}, Selected path: {Path}", result, selectedPath);
                
                tcs.SetResult(selectedPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ShowDialogCore");
                tcs.SetException(ex);
            }
        }
    }
}