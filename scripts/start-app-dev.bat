@echo off
echo ========================================
echo Starting MyPhotoHelper (Development Mode)
echo ========================================
echo.

cd /d "%~dp0\.."

echo Checking .NET SDK...
dotnet --version
if %errorlevel% neq 0 (
    echo ERROR: .NET SDK is not installed or not in PATH
    echo Please install .NET 9.0 SDK from https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

echo.
echo Checking Python...
python --version
if %errorlevel% neq 0 (
    echo ERROR: Python is not installed or not in PATH
    echo Please install Python 3.8+ from https://www.python.org/downloads/
    pause
    exit /b 1
)

echo.
echo Restoring packages...
dotnet restore src\MyPhotoHelper.sln
if %errorlevel% neq 0 (
    echo ERROR: Package restore failed
    pause
    exit /b 1
)

echo.
echo Building application (Debug)...
dotnet build src\MyPhotoHelper.sln --configuration Debug
if %errorlevel% neq 0 (
    echo ERROR: Build failed
    pause
    exit /b 1
)

echo.
echo ========================================
echo Starting MyPhotoHelper with verbose logging...
echo.
echo System Tray: Look for the photo icon near the clock
echo Web Interface: http://localhost:5113
echo.
echo This window will show detailed logs and console output.
echo Press Ctrl+C to stop the application
echo ========================================
echo.

set ASPNETCORE_ENVIRONMENT=Development
set ASPNETCORE_URLS=http://localhost:5113
set Logging__LogLevel__Default=Debug
set Logging__LogLevel__Microsoft=Information
set Logging__LogLevel__Microsoft.Hosting.Lifetime=Information

cd src\MyPhotoHelper
dotnet run --no-build --configuration Debug

pause