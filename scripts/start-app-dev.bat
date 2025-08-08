@echo off
echo ========================================
echo   Starting MyPhotoHelper (Development)
echo ========================================
echo.

cd /d "%~dp0\.."

REM Check if dotnet is installed
dotnet --version
if errorlevel 1 (
    echo ERROR: .NET SDK is not installed or not in PATH
    echo Please install .NET 9 SDK from https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

echo.
echo Building application (Debug mode)...
dotnet build src\MyPhotoHelper.sln --configuration Debug
if errorlevel 1 (
    echo ERROR: Build failed
    pause
    exit /b 1
)

echo.
echo ========================================
echo Starting MyPhotoHelper with console output...
echo.
echo The console will show detailed logs.
echo Press Ctrl+C to stop the application.
echo ========================================
echo.

REM Set environment variables for verbose logging
set ASPNETCORE_ENVIRONMENT=Development
set ASPNETCORE_URLS=http://localhost:5113
set Logging__LogLevel__Default=Debug
set Logging__LogLevel__Microsoft=Information

REM Run the application with console output visible
cd src\MyPhotoHelper
dotnet run --no-build --configuration Debug

pause