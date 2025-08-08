# MyPhotoHelper Release Preparation Script
# This script helps prepare a new release by updating version numbers

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    
    [switch]$SkipBuild,
    [switch]$SkipTests
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  MyPhotoHelper Release Preparation" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Validate version format
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Write-Host "ERROR: Version must be in format X.Y.Z (e.g., 1.3.1)" -ForegroundColor Red
    exit 1
}

$VersionWithBuild = "$Version.0"
$TagName = "v$Version"

Write-Host "Preparing release for version: $Version" -ForegroundColor Green
Write-Host "  Project version: $VersionWithBuild" -ForegroundColor Gray
Write-Host "  Git tag: $TagName" -ForegroundColor Gray
Write-Host ""

# Check if we're in the right directory
if (-not (Test-Path "src\MyPhotoHelper.sln")) {
    Write-Host "ERROR: Must run from repository root directory" -ForegroundColor Red
    exit 1
}

# Check for uncommitted changes
$gitStatus = git status --porcelain
if ($gitStatus) {
    Write-Host "WARNING: You have uncommitted changes:" -ForegroundColor Yellow
    git status --short
    Write-Host ""
    $response = Read-Host "Do you want to continue? (y/n)"
    if ($response -ne 'y') {
        Write-Host "Aborted by user" -ForegroundColor Yellow
        exit 0
    }
}

# Run tests if not skipped
if (-not $SkipTests) {
    Write-Host "Running tests..." -ForegroundColor Cyan
    dotnet test src\MyPhotoHelper.Tests --configuration Release --no-build
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Tests failed. Fix tests before releasing." -ForegroundColor Red
        exit 1
    }
    Write-Host "✓ Tests passed" -ForegroundColor Green
    Write-Host ""
}

# Update version in project file
Write-Host "Updating version in MyPhotoHelper.csproj..." -ForegroundColor Cyan
$csprojPath = "src\MyPhotoHelper\MyPhotoHelper.csproj"
$csproj = Get-Content $csprojPath -Raw

# Update all version fields
$csproj = $csproj -replace '<AssemblyVersion>[\d\.]+</AssemblyVersion>', "<AssemblyVersion>$VersionWithBuild</AssemblyVersion>"
$csproj = $csproj -replace '<FileVersion>[\d\.]+</FileVersion>', "<FileVersion>$VersionWithBuild</FileVersion>"
$csproj = $csproj -replace '<ProductVersion>[\d\.]+</ProductVersion>', "<ProductVersion>$VersionWithBuild</ProductVersion>"
$csproj = $csproj -replace '<Version>[\d\.]+</Version>', "<Version>$VersionWithBuild</Version>"
$csproj = $csproj -replace '<AssemblyInformationalVersion>[\d\.]+</AssemblyInformationalVersion>', "<AssemblyInformationalVersion>$Version</AssemblyInformationalVersion>"
$csproj = $csproj -replace '<InformationalVersion>[\d\.]+</InformationalVersion>', "<InformationalVersion>$Version</InformationalVersion>"

$csproj | Set-Content $csprojPath -NoNewline
Write-Host "✓ Project file updated" -ForegroundColor Green
Write-Host ""

# Build to verify
if (-not $SkipBuild) {
    Write-Host "Building release configuration..." -ForegroundColor Cyan
    dotnet build src\MyPhotoHelper.sln --configuration Release --no-incremental
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Build failed" -ForegroundColor Red
        exit 1
    }
    
    # Verify version in built exe
    $exePath = "src\MyPhotoHelper\bin\Release\net9.0-windows\MyPhotoHelper.exe"
    if (Test-Path $exePath) {
        $fileVersion = (Get-Item $exePath).VersionInfo.FileVersion
        if ($fileVersion -eq $VersionWithBuild) {
            Write-Host "✓ Build successful - Version verified: $fileVersion" -ForegroundColor Green
        } else {
            Write-Host "WARNING: Built exe has version $fileVersion, expected $VersionWithBuild" -ForegroundColor Yellow
        }
    }
    Write-Host ""
}

# Commit changes
Write-Host "Committing version update..." -ForegroundColor Cyan
git add $csprojPath
git commit -m "Bump version to $Version"
if ($LASTEXITCODE -ne 0) {
    Write-Host "WARNING: Nothing to commit or commit failed" -ForegroundColor Yellow
} else {
    Write-Host "✓ Changes committed" -ForegroundColor Green
}
Write-Host ""

# Show next steps
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Next Steps" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. Push changes to main branch:" -ForegroundColor Yellow
Write-Host "   git push origin main" -ForegroundColor White
Write-Host ""
Write-Host "2. Create and push release tag:" -ForegroundColor Yellow
Write-Host "   git tag $TagName" -ForegroundColor White
Write-Host "   git push origin $TagName" -ForegroundColor White
Write-Host ""
Write-Host "3. GitHub Actions will automatically:" -ForegroundColor Yellow
Write-Host "   - Build the release" -ForegroundColor Gray
Write-Host "   - Create the installer" -ForegroundColor Gray
Write-Host "   - Create GitHub release" -ForegroundColor Gray
Write-Host "   - Update update.xml" -ForegroundColor Gray
Write-Host ""
Write-Host "4. Verify the release at:" -ForegroundColor Yellow
Write-Host "   https://github.com/thefrederiksen/MyPhotoHelper/releases" -ForegroundColor White
Write-Host ""

# Ask if user wants to push now
$response = Read-Host "Do you want to push changes and create tag now? (y/n)"
if ($response -eq 'y') {
    Write-Host ""
    Write-Host "Pushing to main branch..." -ForegroundColor Cyan
    git push origin main
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Pushed to main" -ForegroundColor Green
        Write-Host ""
        
        Write-Host "Creating and pushing tag $TagName..." -ForegroundColor Cyan
        git tag $TagName
        git push origin $TagName
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Tag pushed successfully" -ForegroundColor Green
            Write-Host ""
            Write-Host "🎉 Release process initiated!" -ForegroundColor Green
            Write-Host "Monitor progress at: https://github.com/thefrederiksen/MyPhotoHelper/actions" -ForegroundColor Cyan
        } else {
            Write-Host "ERROR: Failed to push tag" -ForegroundColor Red
        }
    } else {
        Write-Host "ERROR: Failed to push to main" -ForegroundColor Red
    }
} else {
    Write-Host "Release preparation complete. Push manually when ready." -ForegroundColor Yellow
}