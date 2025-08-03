using System.Collections.Concurrent;
using MyPhotoHelper.Models;

namespace MyPhotoHelper.Services;

public interface IGalleryStateService
{
    event EventHandler<PhotosBatchLoadedEventArgs>? PhotosBatchLoaded;
    void QueuePhotosForDisplay(int year, int month, List<tbl_images> photos);
    void StartProcessing();
    void StopProcessing();
    void Clear();
}

public class PhotosBatchLoadedEventArgs : EventArgs
{
    public int Year { get; set; }
    public int Month { get; set; }
    public List<tbl_images> Photos { get; set; } = new();
}

public class GalleryStateService : IGalleryStateService, IDisposable
{
    public event EventHandler<PhotosBatchLoadedEventArgs>? PhotosBatchLoaded;
    
    private readonly ConcurrentQueue<(int year, int month, List<tbl_images> photos)> _photoQueue = new();
    private readonly ILogger<GalleryStateService> _logger;
    private CancellationTokenSource? _processingCts;
    private readonly System.Threading.Timer _batchTimer;
    private readonly object _timerLock = new();

    public GalleryStateService(ILogger<GalleryStateService> logger)
    {
        _logger = logger;
        _batchTimer = new System.Threading.Timer(ProcessBatch, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void QueuePhotosForDisplay(int year, int month, List<tbl_images> photos)
    {
        _photoQueue.Enqueue((year, month, photos));
        
        // Start batch timer if not already running
        lock (_timerLock)
        {
            _batchTimer.Change(100, Timeout.Infinite); // Process batch after 100ms
        }
    }

    private void ProcessBatch(object? state)
    {
        try
        {
            var batch = new List<(int year, int month, List<tbl_images> photos)>();
            
            // Dequeue up to 3 months at a time
            for (int i = 0; i < 3 && _photoQueue.TryDequeue(out var item); i++)
            {
                batch.Add(item);
            }

            if (batch.Any())
            {
                foreach (var (year, month, photos) in batch)
                {
                    PhotosBatchLoaded?.Invoke(this, new PhotosBatchLoadedEventArgs 
                    { 
                        Year = year, 
                        Month = month, 
                        Photos = photos 
                    });
                }
            }

            // If more items in queue, schedule next batch
            if (!_photoQueue.IsEmpty)
            {
                lock (_timerLock)
                {
                    _batchTimer.Change(100, Timeout.Infinite);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing photo batch");
        }
    }

    public void StartProcessing()
    {
        _processingCts = new CancellationTokenSource();
    }

    public void StopProcessing()
    {
        lock (_timerLock)
        {
            _batchTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }
        
        _processingCts?.Cancel();
        
        while (_photoQueue.TryDequeue(out _)) { }
    }

    public void Clear()
    {
        StopProcessing();
    }

    public void Dispose()
    {
        StopProcessing();
        _batchTimer?.Dispose();
        _processingCts?.Dispose();
    }
}