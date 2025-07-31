#r "System.Drawing.Common"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

// EXIF GPS tags
const int PropertyTagGpsLatitudeRef = 0x0001;
const int PropertyTagGpsLatitude = 0x0002;
const int PropertyTagGpsLongitudeRef = 0x0003;
const int PropertyTagGpsLongitude = 0x0004;

var testImagePath = @"C:\Repos\MyPhotoHelper\src\MyPhotoHelper.Tests\Images\test-image-with-gps.jpg";

if (!File.Exists(testImagePath))
{
    Console.WriteLine($"Test image not found at: {testImagePath}");
    Console.WriteLine("Please provide a path to an image with GPS data:");
    
    // Try to find any jpg files in the test directory
    var testDir = @"C:\Repos\MyPhotoHelper\src\MyPhotoHelper.Tests\Images";
    if (Directory.Exists(testDir))
    {
        var jpgFiles = Directory.GetFiles(testDir, "*.jpg");
        Console.WriteLine($"\nFound {jpgFiles.Length} JPG files in test directory:");
        foreach (var file in jpgFiles.Take(5))
        {
            Console.WriteLine($"  - {Path.GetFileName(file)}");
        }
    }
    return;
}

try
{
    using var img = Image.FromFile(testImagePath);
    
    Console.WriteLine($"Image: {Path.GetFileName(testImagePath)}");
    Console.WriteLine($"Dimensions: {img.Width}x{img.Height}");
    Console.WriteLine($"\nTotal EXIF properties: {img.PropertyItems.Length}");
    
    // List all property IDs
    var propIds = img.PropertyIdList.ToList();
    Console.WriteLine($"\nProperty IDs found: {string.Join(", ", propIds.Select(id => $"0x{id:X4}"))}");
    
    // Check for GPS properties
    Console.WriteLine($"\nGPS Latitude (0x0002): {propIds.Contains(PropertyTagGpsLatitude)}");
    Console.WriteLine($"GPS Longitude (0x0004): {propIds.Contains(PropertyTagGpsLongitude)}");
    Console.WriteLine($"GPS Latitude Ref (0x0001): {propIds.Contains(PropertyTagGpsLatitudeRef)}");
    Console.WriteLine($"GPS Longitude Ref (0x0003): {propIds.Contains(PropertyTagGpsLongitudeRef)}");
    
    if (propIds.Contains(PropertyTagGpsLatitude))
    {
        var latProp = img.GetPropertyItem(PropertyTagGpsLatitude);
        Console.WriteLine($"\nLatitude property:");
        Console.WriteLine($"  Type: {latProp.Type}");
        Console.WriteLine($"  Length: {latProp.Len}");
        Console.WriteLine($"  Value bytes: {BitConverter.ToString(latProp.Value)}");
        
        if (latProp.Len >= 24)
        {
            var deg = BitConverter.ToUInt32(latProp.Value, 0) / (double)BitConverter.ToUInt32(latProp.Value, 4);
            var min = BitConverter.ToUInt32(latProp.Value, 8) / (double)BitConverter.ToUInt32(latProp.Value, 12);
            var sec = BitConverter.ToUInt32(latProp.Value, 16) / (double)BitConverter.ToUInt32(latProp.Value, 20);
            
            Console.WriteLine($"  Degrees: {deg}");
            Console.WriteLine($"  Minutes: {min}");
            Console.WriteLine($"  Seconds: {sec}");
            Console.WriteLine($"  Decimal: {deg + min/60 + sec/3600}");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine($"Stack: {ex.StackTrace}");
}