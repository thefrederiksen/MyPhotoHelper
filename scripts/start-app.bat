@echo off
echo ========================================
echo Starting MyPhotoHelper
echo ========================================
echo.

cd /d "%~dp0\.."

echo Checking .NET SDK...
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: .NET SDK is not installed or not in PATH
    echo Please install .NET 9.0 SDK from https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

echo Checking Python...
python --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: Python is not installed or not in PATH
    echo Please install Python 3.8+ from https://www.python.org/downloads/
    pause
    exit /b 1
)

echo Building application...
dotnet build src\MyPhotoHelper.sln --configuration Debug >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: Build failed. Running with detailed output...
    echo.
    dotnet build src\MyPhotoHelper.sln --configuration Debug
    pause
    exit /b 1
)

echo Starting MyPhotoHelper...
echo.
echo The application will start in the system tray.
echo Look for the photo icon in your system tray (near the clock).
echo.
echo To access the web interface, open: http://localhost:5113
echo.
echo Press Ctrl+C to stop the application.
echo ========================================
echo.

cd src\MyPhotoHelper
dotnet run --no-build --configuration Debug

pause