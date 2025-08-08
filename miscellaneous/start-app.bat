@echo off
echo ========================================
echo   Starting MyPhotoHelper
echo ========================================
echo.

cd /d "%~dp0\.."

REM Check if dotnet is installed
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: .NET SDK is not installed or not in PATH
    echo Please install .NET 9 SDK from https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

echo Building application (Release mode)...
dotnet build src\MyPhotoHelper.sln --configuration Release >nul 2>&1
if errorlevel 1 (
    echo ERROR: Build failed. Running with detailed output...
    echo.
    dotnet build src\MyPhotoHelper.sln --configuration Release
    pause
    exit /b 1
)

echo.
echo MyPhotoHelper is starting...
echo The application will run in the system tray.
echo Look for the photo icon near the clock.
echo.
echo To access the web interface: http://localhost:5113
echo To exit: Right-click the tray icon and select Exit
echo.
echo You can close this window once the app starts.
echo.

cd src\MyPhotoHelper
start "" dotnet run --no-build --configuration Release

timeout /t 5 >nul
exit