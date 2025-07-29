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
            // Get photos from this date across all years
            var query = _context.tbl_images
                .AsNoTracking()
                .Include(img => img.tbl_image_metadata)
                .Include(img => img.tbl_image_analysis)
                .Include(img => img.ScanDirectory)
                .Where(img => img.IsDeleted == 0 && 
                             img.FileExists == 1 &&
                             img.tbl_image_metadata != null &&
                             img.tbl_image_metadata.DateTaken.HasValue && 
                             img.tbl_image_metadata.DateTaken.Value.Month == date.Month && 
                             img.tbl_image_metadata.DateTaken.Value.Day == date.Day);
            
            // Add screenshot filter if requested
            if (excludeScreenshots)
            {
                // Filter out screenshots based on AI analysis
                query = query.Where(img => img.tbl_image_analysis == null || 
                                         img.tbl_image_analysis.ImageCategory != "screenshot");
            }
            
            var photos = await query
                .OrderByDescending(img => img.tbl_image_metadata!.DateTaken)
                .Take(200) // Limit to prevent performance issues
                .ToListAsync();

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

            Logger.Info($"Found {photos.Count} photos for {date:MMMM d} across {yearGroups.Count} years");
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