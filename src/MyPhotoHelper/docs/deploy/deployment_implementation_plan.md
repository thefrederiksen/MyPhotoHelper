# MyPhotoHelper - Deployment Implementation Plan

## Executive Summary

This document provides a detailed, step-by-step implementation plan for adding Squirrel.Windows deployment capabilities to MyPhotoHelper. The implementation will enable automatic updates through GitHub Releases, providing users with a seamless update experience.

**Timeline**: 3-4 days  
**Outcome**: Professional auto-updating Windows application

## Phase 1: Project Setup (Day 1)

### 1.1 Add Squirrel NuGet Package

**File**: `src/MyPhotoHelper/MyPhotoHelper.csproj`

Add the following PackageReference:
```xml
<ItemGroup>
  <!-- Existing packages... -->
  <PackageReference Include="Clowd.Squirrel" Version="2.11.1" />
</ItemGroup>
```

**Command**:
```powershell
cd src\MyPhotoHelper
dotnet add package Clowd.Squirrel --version 2.11.1
```

### 1.2 Create Application Icon

**Steps**:
1. Create a 256x256 PNG icon for your application
2. Use an online converter or tool to create multi-resolution ICO file
3. Save as `src/MyPhotoHelper/icon.ico`

**Icon should contain these sizes**:
- 256x256
- 128x128
- 64x64
- 48x48
- 32x32
- 16x16

### 1.3 Update Project Properties

**File**: `src/MyPhotoHelper/MyPhotoHelper.csproj`

Add after the existing PropertyGroup:
```xml
<PropertyGroup>
  <!-- Version Information -->
  <AssemblyVersion>1.0.0.0</AssemblyVersion>
  <FileVersion>1.0.0.0</FileVersion>
  <ProductVersion>1.0.0</ProductVersion>
  
  <!-- Application Information -->
  <Product>MyPhotoHelper</Product>
  <Company>Your Company</Company>
  <Copyright>Copyright © 2025</Copyright>
  <Description>AI-powered photo organization and management</Description>
  
  <!-- Application Icon -->
  <ApplicationIcon>icon.ico</ApplicationIcon>
  
  <!-- Squirrel Settings -->
  <SquirrelBuildDirectory>$(MSBuildProjectDirectory)\..\..\releases</SquirrelBuildDirectory>
</PropertyGroup>
```

### 1.4 Configure Build Output

**File**: `src/MyPhotoHelper/MyPhotoHelper.csproj`

Add publish profile settings:
```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <PublishSingleFile>false</PublishSingleFile>
  <SelfContained>false</SelfContained>
  <PublishReadyToRun>true</PublishReadyToRun>
  <PublishTrimmed>false</PublishTrimmed>
</PropertyGroup>
```

## Phase 2: Code Integration (Day 1-2)

### 2.1 Modify Program.cs for Squirrel

**File**: `src/MyPhotoHelper/Program.cs`

Add at the top:
```csharp
using Squirrel;
using System.Diagnostics;
```

Replace the Main method with:
```csharp
[STAThread]
static void Main(string[] args)
{
    // Handle Squirrel events first
    try
    {
        SquirrelAwareApp.HandleEvents(
            onInitialInstall: OnAppInstall,
            onAppUninstall: OnAppUninstall,
            onEveryRun: OnAppRun
        );
    }
    catch (Exception ex)
    {
        // Log but don't crash if Squirrel fails
        StartupErrorLogger.LogError("Squirrel initialization failed", ex);
    }

    // Rest of existing Main method code...
    AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
    // ... continue with existing code
}

private static void OnAppInstall(SemanticVersion version, IAppTools tools)
{
    tools.CreateShortcutForThisExe(ShortcutLocation.Desktop | ShortcutLocation.StartMenu);
    tools.CreateUninstallerRegistryEntry();
}

private static void OnAppUninstall(SemanticVersion version, IAppTools tools)
{
    tools.RemoveShortcutForThisExe(ShortcutLocation.Desktop | ShortcutLocation.StartMenu);
    tools.RemoveUninstallerRegistryEntry();
}

private static void OnAppRun(SemanticVersion version, IAppTools tools, bool firstRun)
{
    tools.SetProcessAppUserModelId();
    
    if (firstRun)
    {
        // First run after install
        MessageBox.Show(
            "Welcome to MyPhotoHelper!\n\nThe application will help you organize and manage your photos with AI-powered features.", 
            "Welcome", 
            MessageBoxButtons.OK, 
            MessageBoxIcon.Information);
    }

    // Start update check in background
    Task.Run(async () => await CheckForUpdates());
}

private static async Task CheckForUpdates()
{
    try
    {
        using (var mgr = await UpdateManager.GitHubUpdateManager("https://github.com/thefrederiksen/MyPhotoHelper"))
        {
            var updateInfo = await mgr.CheckForUpdate();
            
            if (updateInfo.ReleasesToApply.Count > 0)
            {
                // Download updates in background
                await mgr.DownloadReleases(updateInfo.ReleasesToApply);
                
                // Apply updates (will be installed on restart)
                await mgr.ApplyReleases(updateInfo);
                
                // Notify user - you might want to do this through your UI instead
                Debug.WriteLine($"Update downloaded: v{updateInfo.FutureReleaseEntry.Version}");
            }
        }
    }
    catch (Exception ex)
    {
        // Don't crash the app if update check fails
        Debug.WriteLine($"Update check failed: {ex.Message}");
    }
}
```

### 2.2 Create Update Service

**File**: `src/MyPhotoHelper/Services/AppUpdateService.cs`

Create new file:
```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Squirrel;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyPhotoHelper.Services
{
    public class AppUpdateService : BackgroundService
    {
        private readonly ILogger<AppUpdateService> _logger;
        private readonly IConfiguration _configuration;
        private UpdateManager? _updateManager;

        public static event EventHandler<UpdateStatus>? UpdateStatusChanged;

        public AppUpdateService(ILogger<AppUpdateService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Wait 30 seconds after startup before first check
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckForUpdates();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking for updates");
                }

                // Check every 6 hours
                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }
        }

        private async Task CheckForUpdates()
        {
            try
            {
                var updateUrl = _configuration["Updates:GitHubUrl"] ?? "https://github.com/thefrederiksen/MyPhotoHelper";
                
                using (var mgr = await UpdateManager.GitHubUpdateManager(updateUrl))
                {
                    _updateManager = mgr;
                    
                    var updateInfo = await mgr.CheckForUpdate();
                    
                    if (updateInfo.ReleasesToApply.Count > 0)
                    {
                        _logger.LogInformation($"Found {updateInfo.ReleasesToApply.Count} updates");
                        
                        UpdateStatusChanged?.Invoke(this, new UpdateStatus 
                        { 
                            Status = "Downloading", 
                            Version = updateInfo.FutureReleaseEntry.Version.ToString() 
                        });

                        // Download updates
                        await mgr.DownloadReleases(updateInfo.ReleasesToApply);
                        
                        // Apply updates
                        await mgr.ApplyReleases(updateInfo);
                        
                        UpdateStatusChanged?.Invoke(this, new UpdateStatus 
                        { 
                            Status = "Ready", 
                            Version = updateInfo.FutureReleaseEntry.Version.ToString() 
                        });
                        
                        _logger.LogInformation($"Update ready: v{updateInfo.FutureReleaseEntry.Version}");
                    }
                    else
                    {
                        _logger.LogInformation("No updates available");
                    }
                }
            }
            finally
            {
                _updateManager = null;
            }
        }

        public override void Dispose()
        {
            _updateManager?.Dispose();
            base.Dispose();
        }
    }

    public class UpdateStatus
    {
        public string Status { get; set; } = "";
        public string Version { get; set; } = "";
    }
}
```

### 2.3 Register Update Service

**File**: `src/MyPhotoHelper/Program.cs` or startup configuration

Add to service registration:
```csharp
// In ConfigureServices or wherever services are registered
services.AddHostedService<AppUpdateService>();
```

### 2.4 Create Update Notification Component

**File**: `src/MyPhotoHelper/Components/UpdateNotification.razor`

Create new component:
```razor
@using MyPhotoHelper.Services
@implements IDisposable

@if (showUpdateNotification)
{
    <div class="update-notification">
        <div class="update-content">
            <span class="update-icon">🎉</span>
            <span class="update-text">Update available: Version @updateVersion</span>
            <button class="btn btn-sm btn-primary" @onclick="RestartApplication">Restart Now</button>
            <button class="btn btn-sm btn-secondary" @onclick="DismissNotification">Later</button>
        </div>
    </div>
}

@code {
    private bool showUpdateNotification = false;
    private string updateVersion = "";

    protected override void OnInitialized()
    {
        AppUpdateService.UpdateStatusChanged += OnUpdateStatusChanged;
    }

    private void OnUpdateStatusChanged(object? sender, UpdateStatus status)
    {
        if (status.Status == "Ready")
        {
            updateVersion = status.Version;
            showUpdateNotification = true;
            InvokeAsync(StateHasChanged);
        }
    }

    private void RestartApplication()
    {
        UpdateManager.RestartApp();
    }

    private void DismissNotification()
    {
        showUpdateNotification = false;
    }

    public void Dispose()
    {
        AppUpdateService.UpdateStatusChanged -= OnUpdateStatusChanged;
    }
}

<style>
    .update-notification {
        position: fixed;
        bottom: 20px;
        right: 20px;
        background-color: var(--primary);
        color: white;
        padding: 1rem 1.5rem;
        border-radius: 8px;
        box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
        z-index: 1050;
        animation: slideIn 0.3s ease-out;
    }

    .update-content {
        display: flex;
        align-items: center;
        gap: 1rem;
    }

    .update-icon {
        font-size: 1.5rem;
    }

    .update-text {
        margin-right: 1rem;
    }

    @keyframes slideIn {
        from {
            transform: translateX(100%);
            opacity: 0;
        }
        to {
            transform: translateX(0);
            opacity: 1;
        }
    }
</style>
```

## Phase 3: Build Infrastructure (Day 2)

### 3.1 Create NuSpec File

**File**: `MyPhotoHelper.nuspec` (in root directory)

```xml
<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
  <metadata>
    <id>MyPhotoHelper</id>
    <version>$version$</version>
    <title>MyPhotoHelper</title>
    <authors>Your Name</authors>
    <owners>Your Name</owners>
    <description>AI-powered photo organization and management application</description>
    <releaseNotes>See https://github.com/thefrederiksen/MyPhotoHelper/releases</releaseNotes>
    <projectUrl>https://github.com/thefrederiksen/MyPhotoHelper</projectUrl>
    <iconUrl>https://raw.githubusercontent.com/thefrederiksen/MyPhotoHelper/main/icon.png</iconUrl>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <copyright>Copyright 2025</copyright>
  </metadata>
  <files>
    <file src="publish\**\*.*" target="lib\net45\" />
  </files>
</package>
```

### 3.2 Create Build Script

**File**: `build/build-release.ps1`

```powershell
param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    
    [Parameter(Mandatory=$false)]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "Building MyPhotoHelper v$Version" -ForegroundColor Green

# Clean previous builds
Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
Remove-Item -Path ".\publish" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path ".\releases" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path ".\nupkg" -Recurse -Force -ErrorAction SilentlyContinue

# Update version in csproj
Write-Host "Updating version in project file..." -ForegroundColor Yellow
$csprojPath = ".\src\MyPhotoHelper\MyPhotoHelper.csproj"
$csproj = Get-Content $csprojPath -Raw
$csproj = $csproj -replace '<AssemblyVersion>[\d\.]+</AssemblyVersion>', "<AssemblyVersion>$Version.0</AssemblyVersion>"
$csproj = $csproj -replace '<FileVersion>[\d\.]+</FileVersion>', "<FileVersion>$Version.0</FileVersion>"
$csproj = $csproj -replace '<ProductVersion>[\d\.]+</ProductVersion>', "<ProductVersion>$Version</ProductVersion>"
Set-Content $csprojPath $csproj

# Build and publish
Write-Host "Building application..." -ForegroundColor Yellow
dotnet publish .\src\MyPhotoHelper\MyPhotoHelper.csproj `
    -c $Configuration `
    -r win-x64 `
    --self-contained false `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=true `
    -o .\publish

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed!"
    exit 1
}

Write-Host "Build completed successfully!" -ForegroundColor Green
```

### 3.3 Create Packaging Script

**File**: `build/pack-release.ps1`

```powershell
param(
    [Parameter(Mandatory=$true)]
    [string]$Version
)

$ErrorActionPreference = "Stop"

Write-Host "Packaging MyPhotoHelper v$Version" -ForegroundColor Green

# Ensure NuGet is available
if (-not (Get-Command nuget -ErrorAction SilentlyContinue)) {
    Write-Host "Installing NuGet CLI..." -ForegroundColor Yellow
    Invoke-WebRequest -Uri "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -OutFile ".\nuget.exe"
    $env:Path += ";$PWD"
}

# Create NuGet package
Write-Host "Creating NuGet package..." -ForegroundColor Yellow
nuget pack MyPhotoHelper.nuspec -Version $Version -OutputDirectory .\nupkg

if ($LASTEXITCODE -ne 0) {
    Write-Error "NuGet packaging failed!"
    exit 1
}

# Create Squirrel release
Write-Host "Creating Squirrel release..." -ForegroundColor Yellow

# Ensure Squirrel is available
$squirrelPath = "${env:USERPROFILE}\.dotnet\tools\squirrel.exe"
if (-not (Test-Path $squirrelPath)) {
    Write-Host "Installing Squirrel..." -ForegroundColor Yellow
    dotnet tool install -g Clowd.Squirrel
}

# Run Squirrel
& $squirrelPath --releasify ".\nupkg\MyPhotoHelper.$Version.nupkg" `
    --releaseDir ".\releases" `
    --setupIcon ".\src\MyPhotoHelper\icon.ico" `
    --no-msi `
    --no-delta  # For first release, then remove this flag

if ($LASTEXITCODE -ne 0) {
    Write-Error "Squirrel packaging failed!"
    exit 1
}

Write-Host "Release package created successfully in .\releases" -ForegroundColor Green
Write-Host "Files created:" -ForegroundColor Yellow
Get-ChildItem .\releases | ForEach-Object { Write-Host "  - $($_.Name)" }
```

### 3.4 Create Full Release Script

**File**: `build/create-release.ps1`

```powershell
param(
    [Parameter(Mandatory=$true)]
    [string]$Version
)

$ErrorActionPreference = "Stop"

Write-Host "Creating full release for MyPhotoHelper v$Version" -ForegroundColor Cyan

# Build
& .\build\build-release.ps1 -Version $Version
if ($LASTEXITCODE -ne 0) { exit 1 }

# Package
& .\build\pack-release.ps1 -Version $Version
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "`nRelease v$Version created successfully!" -ForegroundColor Green
Write-Host "`nNext steps:" -ForegroundColor Yellow
Write-Host "1. Test the installer: .\releases\Setup.exe"
Write-Host "2. Commit version changes: git commit -am 'Release v$Version'"
Write-Host "3. Create tag: git tag v$Version"
Write-Host "4. Push: git push origin main --tags"
Write-Host "5. GitHub Actions will create the release automatically"
```

## Phase 4: GitHub Integration (Day 3)

### 4.1 Create GitHub Actions Workflow

**File**: `.github/workflows/release.yml`

```yaml
name: Release

on:
  push:
    tags:
      - 'v*'

jobs:
  release:
    runs-on: windows-latest
    
    permissions:
      contents: write
    
    steps:
    - name: Checkout
      uses: actions/checkout@v4
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: 9.0.x
    
    - name: Install tools
      run: |
        dotnet tool install -g Clowd.Squirrel
        choco install nuget.commandline -y
    
    - name: Get version from tag
      id: version
      shell: pwsh
      run: |
        $version = $env:GITHUB_REF_NAME -replace '^v', ''
        echo "VERSION=$version" >> $env:GITHUB_OUTPUT
        echo "Version: $version"
    
    - name: Build release
      shell: pwsh
      run: |
        .\build\build-release.ps1 -Version ${{ steps.version.outputs.VERSION }}
    
    - name: Package release
      shell: pwsh
      run: |
        .\build\pack-release.ps1 -Version ${{ steps.version.outputs.VERSION }}
    
    - name: Create GitHub Release
      uses: softprops/action-gh-release@v1
      with:
        name: MyPhotoHelper v${{ steps.version.outputs.VERSION }}
        draft: false
        prerelease: false
        files: |
          releases/Setup.exe
          releases/RELEASES
          releases/*.nupkg
        body: |
          ## MyPhotoHelper v${{ steps.version.outputs.VERSION }}
          
          ### Installation
          - **New users**: Download and run `Setup.exe`
          - **Existing users**: The app will auto-update
          
          ### What's New
          - See [CHANGELOG](https://github.com/thefrederiksen/MyPhotoHelper/blob/main/CHANGELOG.md)
          
          ### Requirements
          - Windows 10 or later
          - .NET 9 Runtime (installer will download if needed)
      env:
        GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

### 4.2 Create GitHub Workflow for Testing

**File**: `.github/workflows/test-build.yml`

```yaml
name: Test Build

on:
  pull_request:
    branches: [ main ]
  workflow_dispatch:

jobs:
  test:
    runs-on: windows-latest
    
    steps:
    - uses: actions/checkout@v4
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: 9.0.x
    
    - name: Restore
      run: dotnet restore
    
    - name: Build
      run: dotnet build --configuration Release --no-restore
    
    - name: Test
      run: dotnet test --no-restore --verbosity normal
```

## Phase 5: Documentation & Testing (Day 3-4)

### 5.1 Create Installation Guide

**File**: `src/MyPhotoHelper/docs/deploy/INSTALL.md`

```markdown
# MyPhotoHelper Installation Guide

## System Requirements
- Windows 10 version 1809 or later
- 4GB RAM (8GB recommended)
- 500MB free disk space
- Internet connection for updates

## Installation

### First-Time Installation

1. Download `Setup.exe` from the [latest release](https://github.com/thefrederiksen/MyPhotoHelper/releases/latest)
2. Run `Setup.exe` (no administrator rights required)
3. MyPhotoHelper will install to your user folder
4. Desktop and Start Menu shortcuts will be created
5. The application will launch automatically

### Installation Location
- Installed to: `%LOCALAPPDATA%\MyPhotoHelper`
- No administrator rights required
- Per-user installation

## Updates

MyPhotoHelper automatically checks for updates every 6 hours. When an update is available:

1. The update downloads in the background
2. You'll see a notification in the app
3. Click "Restart Now" or the update will apply next time you start the app

### Manual Update Check
You can manually check for updates in Settings > About > Check for Updates

## Uninstallation

### Method 1: Windows Settings
1. Open Windows Settings
2. Go to Apps > Apps & features
3. Search for "MyPhotoHelper"
4. Click Uninstall

### Method 2: Control Panel
1. Open Control Panel
2. Go to Programs and Features
3. Find "MyPhotoHelper"
4. Click Uninstall

## Troubleshooting

### Application Won't Start
1. Check if already running in system tray
2. Restart your computer
3. Reinstall using latest Setup.exe

### Updates Not Working
1. Check internet connection
2. Check Windows Firewall settings
3. Try manual download from GitHub

### Error Messages
- **"VCRUNTIME140.dll missing"**: Install [Visual C++ Redistributable](https://aka.ms/vs/17/release/vc_redist.x64.exe)
- **".NET Runtime missing"**: The installer should download it automatically

## Support
- GitHub Issues: https://github.com/thefrederiksen/MyPhotoHelper/issues
- Documentation: https://github.com/thefrederiksen/MyPhotoHelper/wiki
```

### 5.2 Create Developer Testing Guide

**File**: `build/TEST_GUIDE.md`

```markdown
# Testing Squirrel Updates - Developer Guide

## Local Testing Setup

### 1. Create Test Releases
```powershell
# Build version 1.0.0
.\build\create-release.ps1 -Version 1.0.0

# Copy releases to test server
Copy-Item -Path .\releases\* -Destination C:\TestReleases\1.0.0\ -Recurse

# Build version 1.0.1
.\build\create-release.ps1 -Version 1.0.1
Copy-Item -Path .\releases\* -Destination C:\TestReleases\1.0.1\ -Recurse
```

### 2. Test Local Updates
Modify update URL in code temporarily:
```csharp
#if DEBUG
    var updateUrl = @"C:\TestReleases\1.0.1";
#else
    var updateUrl = "https://github.com/thefrederiksen/MyPhotoHelper";
#endif
```

### 3. Test Scenarios

#### Fresh Install
1. Run Setup.exe from 1.0.0
2. Verify shortcuts created
3. Verify app launches

#### Update Check
1. Install 1.0.0
2. Point update URL to 1.0.1 folder
3. Trigger update check
4. Verify download and notification

#### Update Application
1. Click restart when prompted
2. Verify new version running
3. Check old version cleaned up

## Production Testing

### Pre-Release Checklist
- [ ] Version number updated in csproj
- [ ] CHANGELOG.md updated
- [ ] All tests passing
- [ ] Manual smoke test completed
- [ ] Icon included and visible

### Release Process
1. Commit all changes
2. Tag with version: `git tag v1.0.0`
3. Push tag: `git push origin v1.0.0`
4. Monitor GitHub Actions
5. Verify release created
6. Test download from GitHub

### Post-Release Testing
1. Download Setup.exe from GitHub
2. Install on clean machine
3. Verify auto-update works
4. Check analytics/telemetry
```

## Quick Reference Commands

### Build Commands
```powershell
# Full release build
.\build\create-release.ps1 -Version 1.0.0

# Just build
.\build\build-release.ps1 -Version 1.0.0

# Just package
.\build\pack-release.ps1 -Version 1.0.0
```

### Git Commands
```bash
# Create release tag
git tag v1.0.0
git push origin v1.0.0

# Delete tag if needed
git tag -d v1.0.0
git push origin :refs/tags/v1.0.0
```

### Testing Commands
```powershell
# Run local installer
.\releases\Setup.exe

# Check Squirrel logs
Get-Content "$env:LOCALAPPDATA\MyPhotoHelper\SquirrelSetup.log"
```

## Troubleshooting

### Common Issues

1. **NuGet/Squirrel not found**
   ```powershell
   dotnet tool install -g Clowd.Squirrel
   choco install nuget.commandline
   ```

2. **Build fails with version error**
   - Ensure version format is X.Y.Z (e.g., 1.0.0)
   - Check csproj has version properties

3. **GitHub Release not created**
   - Check GitHub Actions permissions
   - Ensure tag starts with 'v'

4. **Updates not detected**
   - Check RELEASES file is uploaded
   - Verify GitHub Release is not draft
   - Check update URL in code

## Next Steps

1. Run through Phase 1-5 implementations
2. Test locally with test releases
3. Create first v1.0.0 release
4. Monitor user feedback
5. Iterate and improve

This implementation plan provides everything needed to add professional auto-updating capabilities to MyPhotoHelper using Squirrel.Windows.