using MyPhotoHelper.Data;
using MyPhotoHelper.Models;
using Microsoft.EntityFrameworkCore;

namespace MyPhotoHelper.Services;

public interface IPhotoPathService
{
    Task<string?> GetPhotoDirectoryAsync();
    Task<string?> GetFullPathAsync(string relativePath);
    Task<string?> GetFullPathForImageAsync(tbl_images image);
    Task<string?> GetFullPathForImageAsync(int imageId);
}

public class PhotoPathService : IPhotoPathService
{
    private readonly MyPhotoHelperDbContext _context;

    public PhotoPathService(MyPhotoHelperDbContext context)
    {
        _context = context;
    }

    public async Task<string?> GetPhotoDirectoryAsync()
    {
        // This method is deprecated - use scan directories instead
        // Return the first scan directory as a fallback
        var firstScanDir = await _context.tbl_scan_directory
            .OrderBy(sd => sd.DateCreated)
            .FirstOrDefaultAsync();
        
        return firstScanDir?.DirectoryPath;
    }

    public async Task<string?> GetFullPathAsync(string relativePath)
    {
        var photoDir = await GetPhotoDirectoryAsync();
        if (string.IsNullOrEmpty(photoDir) || string.IsNullOrEmpty(relativePath))
            return null;

        return Path.Combine(photoDir, relativePath);
    }

    public async Task<string?> GetFullPathForImageAsync(tbl_images image)
    {
        if (image == null || string.IsNullOrEmpty(image.RelativePath))
            return null;

        // If scan directory is already loaded, use it
        if (image.ScanDirectory != null)
        {
            return Path.Combine(image.ScanDirectory.DirectoryPath, image.RelativePath);
        }

        // Load scan directory from database
        var scanDir = await _context.tbl_scan_directory
            .FirstOrDefaultAsync(sd => sd.ScanDirectoryId == image.ScanDirectoryId);
        
        if (scanDir == null)
            return null;

        return Path.Combine(scanDir.DirectoryPath, image.RelativePath);
    }

    public async Task<string?> GetFullPathForImageAsync(int imageId)
    {
        var image = await _context.tbl_images
            .Include(img => img.ScanDirectory)
            .FirstOrDefaultAsync(img => img.ImageId == imageId);
        
        if (image == null)
            return null;

        return await GetFullPathForImageAsync(image);
    }
}