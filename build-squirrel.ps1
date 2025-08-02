# Build script for creating Squirrel packages
param(
    [string]$Version = "1.0.0",
    [string]$ReleaseNotes = "Bug fixes and improvements"
)

Write-Host "Building MyPhotoHelper Squirrel Package v$Version" -ForegroundColor Green

# Clean previous builds
Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
if (Test-Path ".\Releases") {
    Remove-Item -Path ".\Releases" -Recurse -Force
}

# Build the application
Write-Host "Building application..." -ForegroundColor Yellow
dotnet publish src\MyPhotoHelper\MyPhotoHelper.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=true `
    -o "src\MyPhotoHelper\bin\Release\net9.0-windows\win-x64\publish"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed!"
    exit 1
}

# Update version in project file
Write-Host "Updating version to $Version..." -ForegroundColor Yellow
$csprojPath = "src\MyPhotoHelper\MyPhotoHelper.csproj"
$csproj = [xml](Get-Content $csprojPath)

# Update all version properties
$csproj.Project.PropertyGroup[0].AssemblyVersion = "$Version.0"
$csproj.Project.PropertyGroup[0].FileVersion = "$Version.0"
$csproj.Project.PropertyGroup[0].ProductVersion = $Version

$csproj.Save($csprojPath)

# Update version in nuspec
Write-Host "Updating NuSpec version..." -ForegroundColor Yellow
$nuspecPath = "MyPhotoHelper.nuspec"
$nuspec = [xml](Get-Content $nuspecPath)
$nuspec.package.metadata.version = $Version
$nuspec.package.metadata.releaseNotes = $ReleaseNotes
$nuspec.Save($nuspecPath)

# Create NuGet package
Write-Host "Creating NuGet package..." -ForegroundColor Yellow
nuget pack MyPhotoHelper.nuspec -Version $Version -OutputDirectory .\

if ($LASTEXITCODE -ne 0) {
    Write-Error "NuGet pack failed!"
    exit 1
}

# Create Squirrel package
Write-Host "Creating Squirrel package..." -ForegroundColor Yellow
$squirrelPath = "src\MyPhotoHelper\packages\Clowd.Squirrel.2.11.1\tools"

if (Test-Path $squirrelPath) {
    & "$squirrelPath\Squirrel.exe" `
        --releasify "MyPhotoHelper.$Version.nupkg" `
        --releaseDir ".\Releases" `
        --no-msi `
        --icon "src\MyPhotoHelper\app.ico"
} else {
    Write-Error "Squirrel.exe not found! Please restore NuGet packages first."
    exit 1
}

if ($LASTEXITCODE -ne 0) {
    Write-Error "Squirrel releasify failed!"
    exit 1
}

Write-Host "Build complete! Check the Releases folder for output." -ForegroundColor Green
Write-Host "Files created:"
Get-ChildItem -Path ".\Releases" | ForEach-Object { Write-Host "  - $($_.Name)" }