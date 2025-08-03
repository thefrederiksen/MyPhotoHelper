using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using MyPhotoHelper.Data;
using MyPhotoHelper.Models;

namespace MyPhotoHelper.Services;

public interface IBackgroundPhotoLoader
{
    Task<List<tbl_images>> LoadPhotosForMonthAsync(int year, int month, HashSet<string> selectedCategories, CancellationToken cancellationToken = default);
    void StartBackgroundLoading(List<(int year, int month)> monthsToLoad, HashSet<string> selectedCategories, Action<int, int, List<tbl_images>> onPhotosLoaded);
    void CancelBackgroundLoading();
}

public class BackgroundPhotoLoader : IBackgroundPhotoLoader, IDisposable
{
    private readonly IDbContextFactory<MyPhotoHelperDbContext> _contextFactory;
    private readonly ILogger<BackgroundPhotoLoader> _logger;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly ConcurrentQueue<(int year, int month, HashSet<string> categories, Action<int, int, List<tbl_images>> callback)> _loadQueue = new();
    private Task? _backgroundTask;

    public BackgroundPhotoLoader(IDbContextFactory<MyPhotoHelperDbContext> contextFactory, ILogger<BackgroundPhotoLoader> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<List<tbl_images>> LoadPhotosForMonthAsync(int year, int month, HashSet<string> selectedCategories, CancellationToken cancellationToken = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1);

        // Build base query with category filter
        var monthQuery = context.tbl_images
            .Where(img => img.FileExists == 1 && img.IsDeleted == 0);
        
        // Apply category filter if not all categories are selected
        if (selectedCategories.Count > 0 && selectedCategories.Count < 3)
        {
            monthQuery = monthQuery.Where(img => 
                // Check if image has analysis record
                (context.tbl_image_analysis.Any(a => 
                    a.ImageId == img.ImageId && 
                    ((selectedCategories.Contains("photo") && a.ImageCategory == "photo") ||
                     (selectedCategories.Contains("screenshot") && a.ImageCategory == "screenshot") ||
                     (selectedCategories.Contains("unknown") && (a.ImageCategory == null || a.ImageCategory == "unknown")))
                )) ||
                // Include images without analysis only if "unknown" is selected
                (selectedCategories.Contains("unknown") && !context.tbl_image_analysis.Any(a => a.ImageId == img.ImageId))
            );
        }

        var photos = await monthQuery
            .Join(context.tbl_image_metadata,
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
            .ToListAsync(cancellationToken);

        return photos;
    }

    public void StartBackgroundLoading(List<(int year, int month)> monthsToLoad, HashSet<string> selectedCategories, Action<int, int, List<tbl_images>> onPhotosLoaded)
    {
        CancelBackgroundLoading();
        
        _cancellationTokenSource = new CancellationTokenSource();
        
        // Queue all months
        foreach (var (year, month) in monthsToLoad)
        {
            _loadQueue.Enqueue((year, month, new HashSet<string>(selectedCategories), onPhotosLoaded));
        }
        
        // Start background processing
        _backgroundTask = Task.Run(async () => await ProcessLoadQueueAsync(_cancellationTokenSource.Token));
    }

    private async Task ProcessLoadQueueAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting background photo loading");
        
        while (!cancellationToken.IsCancellationRequested && _loadQueue.TryDequeue(out var item))
        {
            try
            {
                _logger.LogDebug($"Loading photos for {item.year}-{item.month}");
                
                var photos = await LoadPhotosForMonthAsync(item.year, item.month, item.categories, cancellationToken);
                
                if (!cancellationToken.IsCancellationRequested)
                {
                    // Invoke callback with cancellation check
                    await Task.Run(() => 
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            item.callback(item.year, item.month, photos);
                        }
                    }, cancellationToken);
                    
                    // Small delay to keep UI responsive
                    await Task.Delay(50, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Background photo loading cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading photos for {item.year}-{item.month}");
            }
        }
        
        // Clear any remaining items in the queue
        while (_loadQueue.TryDequeue(out _)) { }
        
        _logger.LogInformation("Background photo loading completed");
    }

    public void CancelBackgroundLoading()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        
        // Clear the queue
        while (_loadQueue.TryDequeue(out _)) { }
        
        if (_backgroundTask != null)
        {
            try
            {
                _backgroundTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch { }
        }
    }

    public void Dispose()
    {
        CancelBackgroundLoading();
    }
}