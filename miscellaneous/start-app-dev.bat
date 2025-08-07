@echo off
echo ========================================
echo   Starting MyPhotoHelper (Development)
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
echo The console will remain open for debugging.
echo Press Ctrl+C to stop the application.
echo.

REM Run the application with console window visible
cd ..\src\MyPhotoHelper
dotnet run --no-build --configuration Debug

pause