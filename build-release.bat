@echo off
REM Batch wrapper for build-squirrel.ps1

echo Building MyPhotoHelper Release Package...
echo.

REM Check if PowerShell is available
where powershell >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: PowerShell is not installed or not in PATH
    exit /b 1
)

REM Execute the PowerShell script
if "%1"=="" (
    REM No version specified, use default
    powershell -ExecutionPolicy Bypass -File build-squirrel.ps1
) else if "%2"=="" (
    REM Version specified, no release notes
    powershell -ExecutionPolicy Bypass -File build-squirrel.ps1 -Version %1
) else (
    REM Version and release notes specified
    powershell -ExecutionPolicy Bypass -File build-squirrel.ps1 -Version %1 -ReleaseNotes "%2"
)

if %errorlevel% neq 0 (
    echo.
    echo ERROR: Build failed! See above for details.
    pause
    exit /b 1
)

echo.
echo Build completed successfully!
pause