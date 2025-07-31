using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using MyPhotoHelper.Data;
using MyPhotoHelper.Models;

namespace MyPhotoHelper
{
    public class TestGPS
    {
        public static void RunTest()
        {
            Console.WriteLine("\n=== GPS Data Analysis ===\n");
            
            var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                     "MyPhotoHelper", "Database", "myphotohelper.db");
            
            var optionsBuilder = new DbContextOptionsBuilder<MyPhotoHelperDbContext>();
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
            
            using var context = new MyPhotoHelperDbContext(optionsBuilder.Options);
            
            // Get statistics
            var totalImages = context.tbl_images.Count(i => i.FileExists == 1 && i.IsDeleted == 0);
            var imagesWithMetadata = context.tbl_image_metadata.Count();
            var imagesWithGPS = context.tbl_image_metadata.Count(m => m.Latitude.HasValue && m.Longitude.HasValue);
            
            Console.WriteLine($"Database Statistics:");
            Console.WriteLine($"  Total active images: {totalImages}");
            Console.WriteLine($"  Images with metadata: {imagesWithMetadata}");
            Console.WriteLine($"  Images with GPS data: {imagesWithGPS}");
            Console.WriteLine($"  Percentage with GPS: {(imagesWithGPS * 100.0 / Math.Max(1, totalImages)):F1}%\n");
            
            // Sample some images to check
            var sampleImages = context.tbl_images
                .Where(i => i.FileExists == 1 && i.IsDeleted == 0)
                .Include(i => i.tbl_image_metadata)
                .Include(i => i.ScanDirectory)
                .Take(10)
                .ToList();
            
            Console.WriteLine($"Checking {sampleImages.Count} sample images for GPS data...\n");
            
            int checkedCount = 0;
            int fileHasGPS = 0;
            int dbHasGPS = 0;
            
            foreach (var image in sampleImages)
            {
                var fullPath = Path.Combine(image.ScanDirectory.DirectoryPath, image.RelativePath);
                
                if (!File.Exists(fullPath))
                    continue;
                    
                checkedCount++;
                Console.WriteLine($"{checkedCount}. {image.FileName}");
                
                // Check database
                if (image.tbl_image_metadata?.Latitude.HasValue == true)
                {
                    dbHasGPS++;
                    Console.WriteLine($"   DB GPS: {image.tbl_image_metadata.Latitude:F6}, {image.tbl_image_metadata.Longitude:F6}");
                }
                else
                {
                    Console.WriteLine($"   DB GPS: None");
                }
                
                // Check file
                try
                {
                    using var img = Image.FromFile(fullPath);
                    var hasLat = img.PropertyIdList.Contains(0x0002); // GPS Latitude
                    var hasLon = img.PropertyIdList.Contains(0x0004); // GPS Longitude
                    
                    if (hasLat && hasLon)
                    {
                        fileHasGPS++;
                        Console.WriteLine($"   File GPS: YES (found GPS EXIF tags)");
                        
                        // Show byte order test
                        var latProp = img.GetPropertyItem(0x0002);
                        if (latProp != null && latProp.Value != null && latProp.Len >= 8)
                        {
                            var b1 = BitConverter.ToUInt32(latProp.Value, 0);
                            var b2 = BitConverter.ToUInt32(latProp.Value, 4);
                            Console.WriteLine($"   Raw bytes: {b1}/{b2} (first rational)");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"   File GPS: No");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   File check error: {ex.Message}");
                }
                
                Console.WriteLine();
            }
            
            Console.WriteLine($"\nSummary of {checkedCount} checked files:");
            Console.WriteLine($"  Files with GPS data: {fileHasGPS}");
            Console.WriteLine($"  Database entries with GPS: {dbHasGPS}");
            
            if (fileHasGPS > dbHasGPS)
            {
                Console.WriteLine($"\n⚠️ ISSUE DETECTED: {fileHasGPS - dbHasGPS} files have GPS data that wasn't extracted!");
                Console.WriteLine("This indicates a problem with the GPS extraction code.");
            }
            else if (fileHasGPS == 0)
            {
                Console.WriteLine("\n✓ No GPS data found in the sample files.");
                Console.WriteLine("Your images may not contain location information.");
            }
            else
            {
                Console.WriteLine("\n✓ GPS extraction appears to be working correctly.");
            }
        }
    }
}