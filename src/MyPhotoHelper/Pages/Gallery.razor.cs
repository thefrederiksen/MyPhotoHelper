using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using MyPhotoHelper.Components.Shared;
using MyPhotoHelper.Data;
using MyPhotoHelper.Models;
using MyPhotoHelper.Services;

namespace MyPhotoHelper.Pages
{
    public partial class Gallery : ComponentBase, IDisposable
    {
        [Inject] private MyPhotoHelperDbContext DbContext { get; set; } = null!;
        [Inject] private IScanStatusService ScanStatusService { get; set; } = null!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = null!;

        private class YearGroup
        {
            public int Year { get; set; }
            public int TotalPhotos { get; set; }
            public List<MonthGroup> MonthGroups { get; set; } = new();
        }

        private class MonthGroup
        {
            public int Month { get; set; }
            public int PhotoCount { get; set; }
            public List<tbl_images>? Photos { get; set; }
        }

        private List<YearGroup> yearGroups = new();
        private bool isLoading = true;
        private bool isCompactView = false;
        private int totalPhotos = 0;
        private int totalImagesCount = 0;
        private DateTime? oldestPhoto;
        private DateTime? newestPhoto;
        private bool showScrollTop = false;
        private Dictionary<string, bool> expandedMonths = new();
        private bool showCategoryDropdown = false;
        private HashSet<string> selectedCategories = new() { "photo", "unknown" };
        private HashSet<string> loadingMonths = new();

        protected override async Task OnInitializedAsync()
        {
            ScanStatusService.StatusChanged += OnScanStatusChanged;
            await LoadGalleryStructure();
            
            // Set up scroll detection for scroll-to-top button
            await JSRuntime.InvokeVoidAsync("eval", @"
                window.addEventListener('scroll', function() {
                    window.scrollY > 300 ? 
                        DotNet.invokeMethodAsync('MyPhotoHelper', 'SetScrollTopVisible', true) :
                        DotNet.invokeMethodAsync('MyPhotoHelper', 'SetScrollTopVisible', false);
                });
            ");
        }

        private async Task LoadGalleryStructure()
        {
            isLoading = true;
            StateHasChanged();

            try
            {
                // Build base query with category filter
                var baseQuery = DbContext.tbl_images
                    .Where(img => img.FileExists == 1 && img.IsDeleted == 0);
                
                // Apply category filter if not all categories are selected
                if (selectedCategories.Count > 0 && selectedCategories.Count < 3)
                {
                    baseQuery = baseQuery.Where(img => 
                        // Check if image has analysis record
                        (DbContext.tbl_image_analysis.Any(a => 
                            a.ImageId == img.ImageId && 
                            ((selectedCategories.Contains("photo") && a.ImageCategory == "photo") ||
                             (selectedCategories.Contains("screenshot") && a.ImageCategory == "screenshot") ||
                             (selectedCategories.Contains("unknown") && (a.ImageCategory == null || a.ImageCategory == "unknown")))
                        )) ||
                        // Include images without analysis only if "unknown" is selected
                        (selectedCategories.Contains("unknown") && !DbContext.tbl_image_analysis.Any(a => a.ImageId == img.ImageId))
                    );
                }

                // Get photo counts grouped by year and month
                var photoStats = await baseQuery
                    .Join(DbContext.tbl_image_metadata,
                          img => img.ImageId,
                          meta => meta.ImageId,
                          (img, meta) => new { img, meta })
                    .Select(x => new
                    {
                        x.img.ImageId,
                        // Always use DateTaken from metadata, which should never be null
                        // If it is null for some reason, fall back to DateCreated
                        Date = x.meta.DateTaken ?? x.img.DateCreated,
                        Year = (x.meta.DateTaken ?? x.img.DateCreated).Year,
                        Month = (x.meta.DateTaken ?? x.img.DateCreated).Month
                    })
                    .GroupBy(x => new { x.Year, x.Month })
                    .Select(g => new
                    {
                        g.Key.Year,
                        g.Key.Month,
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Year)
                    .ThenByDescending(x => x.Month)
                    .ToListAsync();

                // Calculate totals
                totalPhotos = photoStats.Sum(x => x.Count);
                totalImagesCount = totalPhotos;
                
                // Get date range
                if (photoStats.Any())
                {
                    var years = photoStats.Select(x => x.Year).Distinct().OrderBy(y => y).ToList();
                    oldestPhoto = new DateTime(years.First(), 1, 1);
                    newestPhoto = new DateTime(years.Last(), 12, 31);
                }

                // Build year/month structure (without loading photos yet)
                yearGroups = photoStats
                    .GroupBy(x => x.Year)
                    .Select(yearGroup => new YearGroup
                    {
                        Year = yearGroup.Key,
                        TotalPhotos = yearGroup.Sum(x => x.Count),
                        MonthGroups = yearGroup
                            .Select(x => new MonthGroup
                            {
                                Month = x.Month,
                                PhotoCount = x.Count,
                                Photos = null // Will load on demand
                            })
                            .OrderByDescending(x => x.Month)
                            .ToList()
                    })
                    .OrderByDescending(x => x.Year)
                    .ToList();

                // Auto-expand current month
                var currentYear = DateTime.Now.Year;
                var currentMonth = DateTime.Now.Month;
                var key = $"{currentYear}-{currentMonth}";
                expandedMonths[key] = true;
                
                // Load photos for current month immediately
                if (yearGroups.Any(y => y.Year == currentYear && y.MonthGroups.Any(m => m.Month == currentMonth)))
                {
                    await LoadMonthPhotos(currentYear, currentMonth);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading gallery: {ex.Message}");
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        private async Task LoadMonthPhotos(int year, int month)
        {
            var yearGroup = yearGroups.FirstOrDefault(y => y.Year == year);
            var monthGroup = yearGroup?.MonthGroups.FirstOrDefault(m => m.Month == month);
            
            if (monthGroup == null || monthGroup.Photos != null) return;

            try
            {
                // Load photos for this specific month
                var startDate = new DateTime(year, month, 1);
                var endDate = startDate.AddMonths(1);

                // Build base query with category filter
                var monthQuery = DbContext.tbl_images
                    .Where(img => img.FileExists == 1 && img.IsDeleted == 0);
                
                // Apply category filter if not all categories are selected
                if (selectedCategories.Count > 0 && selectedCategories.Count < 3)
                {
                    monthQuery = monthQuery.Where(img => 
                        // Check if image has analysis record
                        (DbContext.tbl_image_analysis.Any(a => 
                            a.ImageId == img.ImageId && 
                            ((selectedCategories.Contains("photo") && a.ImageCategory == "photo") ||
                             (selectedCategories.Contains("screenshot") && a.ImageCategory == "screenshot") ||
                             (selectedCategories.Contains("unknown") && (a.ImageCategory == null || a.ImageCategory == "unknown")))
                        )) ||
                        // Include images without analysis only if "unknown" is selected
                        (selectedCategories.Contains("unknown") && !DbContext.tbl_image_analysis.Any(a => a.ImageId == img.ImageId))
                    );
                }

                monthGroup.Photos = await monthQuery
                    .Join(DbContext.tbl_image_metadata,
                          img => img.ImageId,
                          meta => meta.ImageId,
                          (img, meta) => new { img, meta })
                    .Where(x => (x.meta.DateTaken ?? x.img.DateCreated) >= startDate &&
                               (x.meta.DateTaken ?? x.img.DateCreated) < endDate)
                    .OrderByDescending(x => x.meta.DateTaken ?? x.img.DateCreated)
                    .Select(x => x.img)
                    .Include(img => img.tbl_image_metadata)
                    .Include(img => img.tbl_image_analysis)
                    .Include(img => img.ScanDirectory)
                    .ToListAsync();

                StateHasChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading photos for {year}-{month}: {ex.Message}");
            }
        }

        private async Task ToggleMonth(int year, int month)
        {
            var key = $"{year}-{month}";
            
            if (expandedMonths.ContainsKey(key))
            {
                expandedMonths[key] = !expandedMonths[key];
            }
            else
            {
                expandedMonths[key] = true;
            }

            // Load photos if expanding and not already loaded
            if (expandedMonths[key])
            {
                await LoadMonthPhotos(year, month);
            }
            
            StateHasChanged();
        }

        private bool IsMonthExpanded(int year, int month)
        {
            var key = $"{year}-{month}";
            return expandedMonths.ContainsKey(key) && expandedMonths[key];
        }

        private string GetMonthName(int month)
        {
            return new DateTime(2000, month, 1).ToString("MMMM");
        }

        private void ToggleView()
        {
            isCompactView = !isCompactView;
        }

        private async Task RefreshGallery()
        {
            expandedMonths.Clear();
            await LoadGalleryStructure();
        }

        private void ExpandAll()
        {
            foreach (var yearGroup in yearGroups)
            {
                foreach (var monthGroup in yearGroup.MonthGroups)
                {
                    var key = $"{yearGroup.Year}-{monthGroup.Month}";
                    expandedMonths[key] = true;
                    
                    // Don't load photos immediately - let them load on-demand as user scrolls
                    // This prevents memory issues and crashes
                }
            }
            StateHasChanged();
        }

        private void CollapseAll()
        {
            expandedMonths.Clear();
            StateHasChanged();
        }


        private async Task ScrollToTop()
        {
            await JSRuntime.InvokeVoidAsync("window.scrollTo", 0, 0);
        }

        [JSInvokable]
        public static Task SetScrollTopVisible(bool visible)
        {
            // This would need to be implemented with a static event or similar
            // For now, we'll keep it simple
            return Task.CompletedTask;
        }

        private async void OnScanStatusChanged(object? sender, EventArgs e)
        {
            await InvokeAsync(async () =>
            {
                // Refresh if scan completed
                if (!ScanStatusService.IsScanning)
                {
                    await LoadGalleryStructure();
                }
            });
        }

        private void ToggleCategoryDropdown()
        {
            showCategoryDropdown = !showCategoryDropdown;
        }

        private void ToggleCategory(string category)
        {
            if (selectedCategories.Contains(category))
            {
                selectedCategories.Remove(category);
            }
            else
            {
                selectedCategories.Add(category);
            }
            
            // Don't close dropdown or refresh - wait for Apply button
            StateHasChanged();
        }
        
        private async Task ApplyCategoryFilter()
        {
            showCategoryDropdown = false;
            await RefreshGallery();
        }

        private void SelectAllCategories()
        {
            selectedCategories.Clear();
            selectedCategories.Add("photo");
            selectedCategories.Add("screenshot");
            selectedCategories.Add("unknown");
            
            StateHasChanged();
        }

        private void ClearAllCategories()
        {
            selectedCategories.Clear();
            
            StateHasChanged();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                // Add click away listener for dropdown
                await JSRuntime.InvokeVoidAsync("eval", @"
                    document.addEventListener('click', function(event) {
                        const dropdown = document.querySelector('.dropdown-menu.show');
                        const button = document.querySelector('.dropdown-toggle');
                        if (dropdown && !dropdown.contains(event.target) && !button.contains(event.target)) {
                            DotNet.invokeMethodAsync('MyPhotoHelper', 'CloseDropdowns');
                        }
                    });
                ");
            }
            
            // Check for expanded months that need photos loaded
            var monthsToLoad = new List<(int Year, int Month)>();
            
            foreach (var yearGroup in yearGroups)
            {
                foreach (var monthGroup in yearGroup.MonthGroups)
                {
                    var key = $"{yearGroup.Year}-{monthGroup.Month}";
                    if (IsMonthExpanded(yearGroup.Year, monthGroup.Month) && 
                        monthGroup.Photos == null && 
                        !loadingMonths.Contains(key))
                    {
                        monthsToLoad.Add((yearGroup.Year, monthGroup.Month));
                    }
                }
            }
            
            // Load photos for expanded months
            foreach (var (year, month) in monthsToLoad)
            {
                var key = $"{year}-{month}";
                loadingMonths.Add(key);
                _ = Task.Run(async () => 
                {
                    await LoadMonthPhotos(year, month);
                    loadingMonths.Remove(key);
                });
            }
        }

        [JSInvokable]
        public static Task CloseDropdowns()
        {
            // This would need to be implemented with a static event or similar
            // For now, we'll keep it simple
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            ScanStatusService.StatusChanged -= OnScanStatusChanged;
        }
    }
}