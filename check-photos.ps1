# PowerShell script to check photo directories and GPS data
$dbPath = "$env:APPDATA\MyPhotoHelper\Database\myphotohelper.db"

if (!(Test-Path $dbPath)) {
    Write-Host "Database not found at: $dbPath"
    exit
}

# Load SQLite assembly
Add-Type -Path "C:\Repos\MyPhotoHelper\src\MyPhotoHelper\bin\Debug\net9.0\System.Data.SQLite.dll" -ErrorAction SilentlyContinue

# If that fails, try using .NET's built-in SQLite
if (!$?) {
    Write-Host "Using alternative SQLite method..."
    $connection = New-Object System.Data.SQLite.SQLiteConnection
    $connection.ConnectionString = "Data Source=$dbPath"
} else {
    $connection = New-Object System.Data.SQLite.SQLiteConnection("Data Source=$dbPath")
}

try {
    $connection.Open()
    
    # Get scan directories
    Write-Host "`n=== Scan Directories ==="
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = "SELECT DirectoryPath FROM tbl_scan_directory"
    $reader = $cmd.ExecuteReader()
    
    $scanDirs = @()
    while ($reader.Read()) {
        $dir = $reader["DirectoryPath"]
        $scanDirs += $dir
        Write-Host "  $dir"
    }
    $reader.Close()
    
    # Get some sample images
    Write-Host "`n=== Checking Sample Images ==="
    $cmd.CommandText = "SELECT s.DirectoryPath, i.RelativePath, i.FileName FROM tbl_images i 
                        JOIN tbl_scan_directory s ON i.ScanDirectoryId = s.ScanDirectoryId 
                        WHERE i.FileExists = 1 AND i.IsDeleted = 0 
                        LIMIT 10"
    $reader = $cmd.ExecuteReader()
    
    $sampleImages = @()
    while ($reader.Read()) {
        $fullPath = Join-Path $reader["DirectoryPath"] $reader["RelativePath"]
        if (Test-Path $fullPath) {
            $sampleImages += $fullPath
        }
    }
    $reader.Close()
    
    $connection.Close()
    
    Write-Host "`nFound $($sampleImages.Count) sample images to check"
    
    # Build the diagnostic tool
    Write-Host "`n=== Building GPS Diagnostic Tool ==="
    & csc /nologo /out:DiagnoseGPS.exe DiagnoseGPS.cs /r:System.Drawing.Common.dll
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Build successful!"
        
        # Run diagnostic on sample images
        $withGPS = 0
        $withoutGPS = 0
        
        foreach ($imagePath in $sampleImages) {
            Write-Host "`n--- Checking: $(Split-Path $imagePath -Leaf) ---"
            $output = & .\DiagnoseGPS.exe $imagePath 2>&1 | Out-String
            
            if ($output -match "This image does not contain GPS") {
                Write-Host "  No GPS data"
                $withoutGPS++
            } elseif ($output -match "Final Coordinates") {
                Write-Host "  HAS GPS DATA!"
                if ($output -match "Big-endian: ([-\d.]+), ([-\d.]+)") {
                    Write-Host "  Coordinates: $($matches[1]), $($matches[2])"
                }
                $withGPS++
            }
        }
        
        Write-Host "`n=== Summary ==="
        Write-Host "Images with GPS: $withGPS"
        Write-Host "Images without GPS: $withoutGPS"
        Write-Host "Total checked: $($sampleImages.Count)"
        
        if ($withGPS -gt 0) {
            Write-Host "`nGPS data IS present in some images!"
            Write-Host "This suggests an extraction issue in the application."
        } else {
            Write-Host "`nNo GPS data found in sample images."
            Write-Host "Your images may not contain location information."
        }
    } else {
        Write-Host "Failed to build diagnostic tool"
    }
} catch {
    Write-Host "Error: $_"
} finally {
    if ($connection.State -eq 'Open') {
        $connection.Close()
    }
}