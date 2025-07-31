@echo off
echo ========================================
echo   Starting MyPhotoHelper
echo ========================================
echo.

REM Check if dotnet is installed
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: .NET SDK is not installed or not in PATH
    echo Please install .NET 9 SDK from https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

echo Building and starting the application...
echo This may take a moment on first run.
echo.

REM Run the application
dotnet run --project src\MyPhotoHelper

REM If the app exits, pause so user can see any error messages
if errorlevel 1 (
    echo.
    echo ========================================
    echo   Application exited with errors
    echo ========================================
    pause
)