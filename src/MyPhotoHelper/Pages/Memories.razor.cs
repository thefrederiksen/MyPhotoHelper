using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using MyPhotoHelper.Components.Shared;
using MyPhotoHelper.Data;
using MyPhotoHelper.Models;
using MyPhotoHelper.Services;
using static MyPhotoHelper.Services.Logger;

namespace MyPhotoHelper.Pages
{
    public partial class Memories : ComponentBase, IDisposable
    {
        [Inject] private IMemoryService MemoryService { get; set; } = null!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = null!;
        [Inject] private MyPhotoHelperDbContext DbContext { get; set; } = null!;
        [Inject] private IServiceProvider ServiceProvider { get; set; } = null!;
        [Inject] private NavigationManager Navigation { get; set; } = null!;
        [Inject] private IScanStatusService ScanStatusService { get; set; } = null!;

        private DateTime selectedDate = DateTime.Today;
        private MemoryCollection? memories;
        private bool isLoading = true;
        private bool hasPhotos = false;
        private System.Threading.Timer? refreshTimer;
        private int lastPhotoCount = 0;

        protected override async Task OnInitializedAsync()
        {
            try
            {
                // Check if any scan directories are configured
                var hasScanDirectories = await DbContext.tbl_scan_directory.AnyAsync();
                
                if (!hasScanDirectories)
                {
                    // No directories configured, redirect to startup wizard
                    Navigation.NavigateTo("/startup-wizard", replace: true);
                    return;
                }
                
                // Subscribe to scan status changes first
                ScanStatusService.StatusChanged += OnScanStatusChanged;
                
                // Load memories in background to not block page render
                _ = Task.Run(async () =>
                {
                    await InvokeAsync(async () =>
                    {
                        await LoadMemories();
                    });
                });
                
                // Start auto-refresh timer if scanning
                if (ScanStatusService.IsScanning)
                {
                    StartAutoRefresh();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in Memories OnInitializedAsync: {ex.Message}");
                isLoading = false;
                StateHasChanged();
            }
        }

        private async Task LoadMemories()
        {
            isLoading = true;
            StateHasChanged();
            
            try
            {
                Logger.Info($"Loading memories for {selectedDate:yyyy-MM-dd}");
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                memories = await MemoryService.GetTodaysMemoriesAsync(selectedDate, excludeScreenshots: true);
                hasPhotos = memories?.TotalPhotos > 0;
                
                stopwatch.Stop();
                Logger.Info($"Loaded {memories?.TotalPhotos ?? 0} memories in {stopwatch.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading memories: {ex.Message}");
                Logger.Error($"Stack trace: {ex.StackTrace}");
                memories = new MemoryCollection { Date = selectedDate };
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        private async Task PreviousDay()
        {
            selectedDate = selectedDate.AddDays(-1);
            await LoadMemories();
            StateHasChanged(); // Force UI update
        }

        private async Task NextDay()
        {
            selectedDate = selectedDate.AddDays(1);
            await LoadMemories();
            StateHasChanged(); // Force UI update
        }

        private async Task GoToToday()
        {
            selectedDate = DateTime.Today;
            await LoadMemories();
            StateHasChanged(); // Force UI update
        }

        private void HandleImageClick(tbl_images photo)
        {
            // Image viewer handles the click internally, this is just for tracking
            Console.WriteLine($"Image clicked: {photo.FileName}");
        }

        private async Task ShowAllPhotosForYear(int year)
        {
            // TODO: Navigate to photo gallery filtered by year and date
            await JSRuntime.InvokeVoidAsync("alert", $"Show all photos from {year}");
        }
        
        private void OnScanStatusChanged(object? sender, EventArgs e)
        {
            InvokeAsync(async () =>
            {
                if (ScanStatusService.IsScanning)
                {
                    StartAutoRefresh();
                }
                else
                {
                    StopAutoRefresh();
                    // Do one final refresh when scan completes
                    await LoadMemories();
                }
            });
        }
        
        private void StartAutoRefresh()
        {
            // Refresh every 3 seconds while scanning
            refreshTimer = new System.Threading.Timer(async _ =>
            {
                await InvokeAsync(async () =>
                {
                    try
                    {
                        // Create a new scoped context for the background timer
                        using var scope = ServiceProvider.CreateScope();
                        var scopedDbContext = scope.ServiceProvider.GetRequiredService<MyPhotoHelperDbContext>();
                        
                        // Check if we have new photos
                        var currentPhotoCount = await scopedDbContext.tbl_images.CountAsync();
                        if (currentPhotoCount != lastPhotoCount)
                        {
                            lastPhotoCount = currentPhotoCount;
                            await LoadMemories();
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the error but don't crash the timer
                        Console.WriteLine($"Error in auto-refresh timer: {ex.Message}");
                        // Stop the timer to prevent repeated errors
                        StopAutoRefresh();
                    }
                });
            }, null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));
        }
        
        private void StopAutoRefresh()
        {
            refreshTimer?.Dispose();
            refreshTimer = null;
        }
        
        public void Dispose()
        {
            StopAutoRefresh();
            ScanStatusService.StatusChanged -= OnScanStatusChanged;
        }
    }
}