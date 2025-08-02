# Create fresh database from consolidated schema
$dbPath = Join-Path $PSScriptRoot "dev_facevault.db"
$sqlPath = Join-Path $PSScriptRoot "DatabaseVersion_001.sql"

# Delete existing database if it exists
if (Test-Path $dbPath) {
    Remove-Item $dbPath -Force
    Write-Host "Deleted existing database: $dbPath" -ForegroundColor Yellow
}

# Create new database
Write-Host "Creating new database: $dbPath" -ForegroundColor Green

# Read SQL script
$sql = Get-Content $sqlPath -Raw

# Execute SQL using sqlite3
$sqliteExe = "sqlite3.exe"

# Check if sqlite3 is available
$sqlite3Path = Get-Command $sqliteExe -ErrorAction SilentlyContinue
if (-not $sqlite3Path) {
    Write-Host "sqlite3.exe not found in PATH. Trying alternative methods..." -ForegroundColor Yellow
    
    # Try using .NET SQLite provider
    Add-Type -Path "C:\Repos\MyPhotoHelper\src\MyPhotoHelper\bin\Debug\net9.0-windows\Microsoft.Data.Sqlite.dll" -ErrorAction SilentlyContinue
    
    try {
        $connection = New-Object Microsoft.Data.Sqlite.SqliteConnection("Data Source=$dbPath")
        $connection.Open()
        
        $command = $connection.CreateCommand()
        $command.CommandText = $sql
        $command.ExecuteNonQuery()
        
        $connection.Close()
        Write-Host "Database created successfully using .NET SQLite!" -ForegroundColor Green
    }
    catch {
        Write-Host "Error creating database: $_" -ForegroundColor Red
        exit 1
    }
}
else {
    # Use sqlite3.exe if available
    $sql | & $sqliteExe $dbPath
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Database created successfully using sqlite3.exe!" -ForegroundColor Green
    }
    else {
        Write-Host "Error creating database with sqlite3.exe" -ForegroundColor Red
        exit 1
    }
}

# Verify database was created
if (Test-Path $dbPath) {
    $fileInfo = Get-Item $dbPath
    Write-Host "Database file created: $($fileInfo.FullName)" -ForegroundColor Green
    Write-Host "File size: $($fileInfo.Length) bytes" -ForegroundColor Cyan
}
else {
    Write-Host "Database file was not created!" -ForegroundColor Red
    exit 1
}