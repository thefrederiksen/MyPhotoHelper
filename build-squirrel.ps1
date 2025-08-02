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

# Find Squirrel.exe in NuGet packages
$possiblePaths = @(
    "$env:USERPROFILE\.nuget\packages\clowd.squirrel\2.11.1\tools\Squirrel.exe",
    "$env:NUGET_PACKAGES\clowd.squirrel\2.11.1\tools\Squirrel.exe",
    "src\MyPhotoHelper\packages\Clowd.Squirrel.2.11.1\tools\Squirrel.exe"
)

$squirrelExe = $null
foreach ($path in $possiblePaths) {
    if (Test-Path $path) {
        $squirrelExe = $path
        Write-Host "Found Squirrel.exe at: $squirrelExe" -ForegroundColor Yellow
        break
    }
}

if (-not $squirrelExe) {
    Write-Error "Squirrel.exe not found! Tried multiple locations."
    Write-Host "Searched paths:" -ForegroundColor Yellow
    foreach ($path in $possiblePaths) {
        Write-Host "  - $path" -ForegroundColor Gray
    }
    
    # Try installing as global tool as last resort
    Write-Host "Attempting to install Squirrel as global tool..." -ForegroundColor Yellow
    dotnet tool install --global Clowd.Squirrel --version 2.11.1 --verbosity quiet
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Creating Squirrel package using global tool..." -ForegroundColor Yellow
        squirrel pack --packId "MyPhotoHelper" `
            --packVersion $Version `
            --packDir "src\MyPhotoHelper\bin\Release\net9.0-windows\win-x64\publish" `
            --releaseDir ".\Releases" `
            --mainExe "MyPhotoHelper.exe" `
            --packTitle "MyPhotoHelper" `
            --packAuthors "MyPhotoHelper Team"
    } else {
        Write-Error "Failed to install Squirrel tool!"
        exit 1
    }
} else {
    # Use the found Squirrel.exe with releasify command
    Write-Host "Creating Squirrel package using releasify..." -ForegroundColor Yellow
    & $squirrelExe releasify `
        --package "MyPhotoHelper.$Version.nupkg" `
        --releaseDir ".\Releases" `
        --allowUnaware
}

if ($LASTEXITCODE -ne 0) {
    Write-Error "Squirrel releasify failed!"
    exit 1
}

Write-Host "Build complete! Check the Releases folder for output." -ForegroundColor Green
Write-Host "Files created:"
Get-ChildItem -Path ".\Releases" | ForEach-Object { Write-Host "  - $($_.Name)" }