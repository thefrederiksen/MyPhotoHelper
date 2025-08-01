using Microsoft.EntityFrameworkCore;
using MyPhotoHelper.Data;
using MyPhotoHelper.Models;

namespace MyPhotoHelper.Services;

public class MemoryService : IMemoryService
{
    private readonly MyPhotoHelperDbContext _context;

    public MemoryService(MyPhotoHelperDbContext context)
    {
        _context = context;
    }

    public async Task<MemoryCollection> GetTodaysMemoriesAsync(DateTime date, bool excludeScreenshots = true)
    {
        var yearGroups = await GetPhotosByDateAsync(date, excludeScreenshots);
        var totalPhotos = yearGroups.Sum(g => g.PhotoCount);

        return new MemoryCollection
        {
            Date = date,
            YearGroups = yearGroups,
            TotalPhotos = totalPhotos
        };
    }

    public async Task<List<YearGroup>> GetPhotosByDateAsync(DateTime date, bool excludeScreenshots = true)
    {
        try
        {
            // Get photos from this date across all years - optimize query
            var query = _context.tbl_images
                .AsNoTracking()
                .Where(img => img.IsDeleted == 0 && 
                             img.FileExists == 1)
                .Join(_context.tbl_image_metadata
                    .Where(m => m.DateTaken.HasValue && 
                               m.DateTaken.Value.Month == date.Month && 
                               m.DateTaken.Value.Day == date.Day),
                    img => img.ImageId,
                    meta => meta.ImageId,
                    (img, meta) => new { Image = img, Metadata = meta })
                .Select(x => x.Image);
            
            // Get the photos with basic data first
            var photoIds = await query
                .Select(img => img.ImageId)
                .Take(200) // Limit to prevent performance issues
                .ToListAsync();

            if (!photoIds.Any())
            {
                return new List<YearGroup>();
            }

            // Now load the full photo data with necessary includes
            var photos = await _context.tbl_images
                .AsNoTracking()
                .Where(img => photoIds.Contains(img.ImageId))
                .Include(img => img.tbl_image_metadata)
                .Include(img => img.ScanDirectory)
                .ToListAsync();
            
            // Add screenshot filter if requested
            if (excludeScreenshots)
            {
                // Load analysis data separately to avoid complex joins
                var analysisData = await _context.tbl_image_analysis
                    .Where(a => photoIds.Contains(a.ImageId))
                    .ToListAsync();
                
                var screenshotIds = analysisData
                    .Where(a => a.ImageCategory == "screenshot")
                    .Select(a => a.ImageId)
                    .ToHashSet();
                
                photos = photos.Where(p => !screenshotIds.Contains(p.ImageId)).ToList();
            }

            // Group by year and create year groups
            var yearGroups = photos
                .Where(img => img.tbl_image_metadata?.DateTaken != null)
                .GroupBy(img => img.tbl_image_metadata!.DateTaken!.Value.Year)
                .OrderByDescending(g => g.Key)
                .Select(g => new YearGroup
                {
                    Year = g.Key,
                    Photos = g.OrderByDescending(img => img.tbl_image_metadata!.DateTaken).ToList()
                })
                .ToList();

            Logger.Info($"Found {photos.Count} photos for {date:MMMM d, yyyy} (Month: {date.Month}, Day: {date.Day}) across {yearGroups.Count} years");
            return yearGroups;
        }
        catch (Exception ex)
        {
            Logger.Error($"Error fetching photos for date {date:MMMM d}: {ex.Message}");
            return new List<YearGroup>();
        }
    }

    public async Task<int> GetTotalPhotosForDateAsync(DateTime date)
    {
        try
        {
            return await _context.tbl_images
                .AsNoTracking()
                .Include(img => img.tbl_image_metadata)
                .CountAsync(img => img.IsDeleted == 0 && 
                           img.FileExists == 1 &&
                           img.tbl_image_metadata != null &&
                           img.tbl_image_metadata.DateTaken.HasValue && 
                           img.tbl_image_metadata.DateTaken.Value.Month == date.Month && 
                           img.tbl_image_metadata.DateTaken.Value.Day == date.Day);
        }
        catch (Exception ex)
        {
            Logger.Error($"Error counting photos for date {date:MMMM d}: {ex.Message}");
            return 0;
        }
    }
}