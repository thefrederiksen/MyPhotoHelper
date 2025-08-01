using MyPhotoHelper.Models;

namespace MyPhotoHelper.Services;

public interface IMemoryService
{
    Task<MemoryCollection> GetTodaysMemoriesAsync(DateTime date, bool excludeScreenshots = false);
    Task<List<YearGroup>> GetPhotosByDateAsync(DateTime date, bool excludeScreenshots = false);
    Task<int> GetTotalPhotosForDateAsync(DateTime date);
}

public class MemoryCollection
{
    public DateTime Date { get; set; }
    public List<YearGroup> YearGroups { get; set; } = new();
    public int TotalPhotos { get; set; }
    public string FormattedDate => Date.ToString("MMMM d");
    public bool HasMemories => YearGroups.Any(g => g.Photos.Any());
}

public class YearGroup
{
    public int Year { get; set; }
    public List<tbl_images> Photos { get; set; } = new();
    public int PhotoCount => Photos.Count;
    public string YearLabel => Year.ToString();
    public bool IsCurrentYear => Year == DateTime.Now.Year;
    
    // For memories page, we want to know if these are photos from "today" in this specific year
    public bool IsToday(DateTime selectedDate) => 
        Year == DateTime.Today.Year && 
        selectedDate.Month == DateTime.Today.Month && 
        selectedDate.Day == DateTime.Today.Day;
}