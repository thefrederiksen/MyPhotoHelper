using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;

class DiagnoseGPS
{
    // EXIF GPS tags
    const int PropertyTagGpsLatitudeRef = 0x0001;
    const int PropertyTagGpsLatitude = 0x0002;
    const int PropertyTagGpsLongitudeRef = 0x0003;
    const int PropertyTagGpsLongitude = 0x0004;

    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: DiagnoseGPS.exe <image-path>");
            Console.WriteLine("\nThis tool will analyze EXIF GPS data in your images.");
            return;
        }

        var imagePath = args[0];
        
        if (!File.Exists(imagePath))
        {
            Console.WriteLine($"Error: File not found: {imagePath}");
            return;
        }

        try
        {
            using var img = Image.FromFile(imagePath);
            
            Console.WriteLine($"\n=== Image Analysis: {Path.GetFileName(imagePath)} ===");
            Console.WriteLine($"Dimensions: {img.Width}x{img.Height}");
            Console.WriteLine($"Total EXIF properties: {img.PropertyItems.Length}");
            
            // Check for GPS properties
            var hasLatitude = img.PropertyIdList.Contains(PropertyTagGpsLatitude);
            var hasLongitude = img.PropertyIdList.Contains(PropertyTagGpsLongitude);
            
            Console.WriteLine($"\nGPS Data Present:");
            Console.WriteLine($"  Latitude: {(hasLatitude ? "YES" : "NO")}");
            Console.WriteLine($"  Longitude: {(hasLongitude ? "YES" : "NO")}");
            
            if (!hasLatitude || !hasLongitude)
            {
                Console.WriteLine("\nThis image does not contain GPS location data.");
                Console.WriteLine("GPS data is typically added by:");
                Console.WriteLine("  - Smartphones with location services enabled");
                Console.WriteLine("  - GPS-enabled cameras");
                Console.WriteLine("  - Manual geotagging software");
                
                // Show what EXIF data IS present
                Console.WriteLine($"\nEXIF properties found ({img.PropertyIdList.Length} total):");
                foreach (var propId in img.PropertyIdList.Take(10))
                {
                    try
                    {
                        var prop = img.GetPropertyItem(propId);
                        Console.WriteLine($"  ID: 0x{propId:X4} - Length: {prop.Len} bytes");
                    }
                    catch { }
                }
                if (img.PropertyIdList.Length > 10)
                    Console.WriteLine($"  ... and {img.PropertyIdList.Length - 10} more");
                
                return;
            }
            
            // Extract GPS coordinates
            Console.WriteLine("\n=== GPS Coordinate Extraction ===");
            
            var latProp = img.GetPropertyItem(PropertyTagGpsLatitude);
            var lonProp = img.GetPropertyItem(PropertyTagGpsLongitude);
            
            Console.WriteLine($"\nLatitude raw data:");
            Console.WriteLine($"  Type: {latProp.Type}");
            Console.WriteLine($"  Length: {latProp.Len} bytes");
            Console.WriteLine($"  First 24 bytes: {BitConverter.ToString(latProp.Value.Take(Math.Min(24, latProp.Len)).ToArray())}");
            
            // Try both endianness
            Console.WriteLine("\nTrying different byte orders:");
            
            // Little-endian (Windows default)
            Console.WriteLine("\n1. Little-endian interpretation:");
            var latLE = ExtractCoordinate(latProp.Value, false);
            var lonLE = ExtractCoordinate(lonProp.Value, false);
            Console.WriteLine($"   Latitude: {latLE:F6}");
            Console.WriteLine($"   Longitude: {lonLE:F6}");
            
            // Big-endian (common in EXIF)
            Console.WriteLine("\n2. Big-endian interpretation:");
            var latBE = ExtractCoordinate(latProp.Value, true);
            var lonBE = ExtractCoordinate(lonProp.Value, true);
            Console.WriteLine($"   Latitude: {latBE:F6}");
            Console.WriteLine($"   Longitude: {lonBE:F6}");
            
            // Apply hemisphere
            if (img.PropertyIdList.Contains(PropertyTagGpsLatitudeRef))
            {
                var latRef = img.GetPropertyItem(PropertyTagGpsLatitudeRef);
                var latRefStr = Encoding.ASCII.GetString(latRef.Value).TrimEnd('\0');
                Console.WriteLine($"\nLatitude reference: {latRefStr}");
                if (latRefStr == "S")
                {
                    latLE = -latLE;
                    latBE = -latBE;
                }
            }
            
            if (img.PropertyIdList.Contains(PropertyTagGpsLongitudeRef))
            {
                var lonRef = img.GetPropertyItem(PropertyTagGpsLongitudeRef);
                var lonRefStr = Encoding.ASCII.GetString(lonRef.Value).TrimEnd('\0');
                Console.WriteLine($"Longitude reference: {lonRefStr}");
                if (lonRefStr == "W")
                {
                    lonLE = -lonLE;
                    lonBE = -lonBE;
                }
            }
            
            Console.WriteLine("\n=== Final Coordinates ===");
            Console.WriteLine($"Little-endian: {latLE:F6}, {lonLE:F6}");
            Console.WriteLine($"Big-endian: {latBE:F6}, {lonBE:F6}");
            
            Console.WriteLine("\nGoogle Maps URLs:");
            Console.WriteLine($"Little-endian: https://www.google.com/maps?q={latLE},{lonLE}");
            Console.WriteLine($"Big-endian: https://www.google.com/maps?q={latBE},{lonBE}");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
    
    static double ExtractCoordinate(byte[] data, bool bigEndian)
    {
        if (data.Length < 24) return 0;
        
        uint GetUInt32(byte[] bytes, int offset)
        {
            if (bigEndian)
                return (uint)((bytes[offset] << 24) | (bytes[offset + 1] << 16) | 
                             (bytes[offset + 2] << 8) | bytes[offset + 3]);
            else
                return BitConverter.ToUInt32(bytes, offset);
        }
        
        var degNum = GetUInt32(data, 0);
        var degDen = GetUInt32(data, 4);
        var minNum = GetUInt32(data, 8);
        var minDen = GetUInt32(data, 12);
        var secNum = GetUInt32(data, 16);
        var secDen = GetUInt32(data, 20);
        
        double degrees = degDen > 0 ? (double)degNum / degDen : 0;
        double minutes = minDen > 0 ? (double)minNum / minDen : 0;
        double seconds = secDen > 0 ? (double)secNum / secDen : 0;
        
        return degrees + minutes / 60.0 + seconds / 3600.0;
    }
}