using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

class TestGPSExtraction
{
    static void Main()
    {
        var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                                  "MyPhotoHelper", "Database", "myphotohelper.db");
        
        if (!File.Exists(dbPath))
        {
            Console.WriteLine($"Database not found at: {dbPath}");
            return;
        }

        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        // Get scan directories and sample images
        Console.WriteLine("=== Checking Photo Directories ===");
        
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT s.DirectoryPath, i.RelativePath, i.ImageId
            FROM tbl_images i
            JOIN tbl_scan_directory s ON i.ScanDirectoryId = s.ScanDirectoryId
            WHERE i.FileExists = 1 AND i.IsDeleted = 0
            ORDER BY RANDOM()
            LIMIT 20";

        var imagesToCheck = new List<(string path, int id)>();
        
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                var fullPath = Path.Combine(reader.GetString(0), reader.GetString(1));
                if (File.Exists(fullPath))
                {
                    imagesToCheck.Add((fullPath, reader.GetInt32(2)));
                }
            }
        }

        Console.WriteLine($"Found {imagesToCheck.Count} images to check\n");

        // Check current GPS data in database
        cmd.CommandText = @"
            SELECT COUNT(*) as total,
                   COUNT(m.Latitude) as with_gps
            FROM tbl_images i
            LEFT JOIN tbl_image_metadata m ON i.ImageId = m.ImageId
            WHERE i.FileExists = 1 AND i.IsDeleted = 0";

        using (var reader = cmd.ExecuteReader())
        {
            if (reader.Read())
            {
                var total = reader.GetInt32(0);
                var withGps = reader.GetInt32(1);
                Console.WriteLine($"Database Status:");
                Console.WriteLine($"  Total images: {total}");
                Console.WriteLine($"  Images with GPS in DB: {withGps}");
                Console.WriteLine($"  Percentage with GPS: {(withGps * 100.0 / total):F1}%\n");
            }
        }

        // Show first few image paths
        Console.WriteLine("Sample image paths:");
        foreach (var (path, id) in imagesToCheck.Take(5))
        {
            Console.WriteLine($"  {Path.GetFileName(path)}");
            
            // Check if this specific image has GPS in database
            cmd.CommandText = "SELECT Latitude, Longitude FROM tbl_image_metadata WHERE ImageId = @id";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@id", id);
            
            using var reader = cmd.ExecuteReader();
            if (reader.Read() && !reader.IsDBNull(0))
            {
                Console.WriteLine($"    DB GPS: {reader.GetDouble(0)}, {reader.GetDouble(1)}");
            }
            else
            {
                Console.WriteLine($"    DB GPS: None");
            }
        }
        
        Console.WriteLine("\nTo diagnose GPS extraction issues:");
        Console.WriteLine($"1. Run: DiagnoseGPS.exe \"{imagesToCheck.First().path}\"");
        Console.WriteLine("2. This will show if the image file actually contains GPS data");
    }
}