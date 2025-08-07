@echo off
echo ========================================
echo Starting MyPhotoHelper (Release Mode)
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

echo Building application (Release)...
dotnet build src\MyPhotoHelper.sln --configuration Release >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: Build failed. Running with detailed output...
    echo.
    dotnet build src\MyPhotoHelper.sln --configuration Release
    pause
    exit /b 1
)

echo Starting MyPhotoHelper...
echo.
echo The application is running in the system tray.
echo To exit, right-click the tray icon and select Exit.
echo.

start "" src\MyPhotoHelper\bin\Release\net9.0-windows\MyPhotoHelper.exe

echo Application started successfully!
echo You can close this window.
timeout /t 3 >nul
exit