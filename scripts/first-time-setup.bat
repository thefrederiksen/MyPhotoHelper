@echo off
echo ========================================
echo MyPhotoHelper - First Time Setup
echo ========================================
echo.

cd /d "%~dp0\.."

echo Step 1: Checking .NET SDK...
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: .NET SDK is not installed
    echo.
    echo Please install .NET 9.0 SDK from:
    echo https://dotnet.microsoft.com/download/dotnet/9.0
    echo.
    pause
    exit /b 1
) else (
    echo [OK] .NET SDK found: 
    dotnet --version
)

echo.
echo Step 2: Checking Python installation...
python --version >nul 2>&1
if %errorlevel% neq 0 (
    echo WARNING: Python is not installed or not in PATH
    echo.
    echo Please install Python 3.8+ from:
    echo https://www.python.org/downloads/
    echo.
    echo The application requires Python for image processing.
    echo.
    pause
    exit /b 1
) else (
    echo [OK] Python found: 
    python --version
)

echo.
echo Step 3: Installing Python dependencies...
cd src\MyPhotoHelper\Python
pip install -r requirements.txt >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: Failed to install Python dependencies
    echo Running with detailed output...
    echo.
    pip install -r requirements.txt
    pause
    exit /b 1
) else (
    echo [OK] Python dependencies installed
)
cd ..\..\..

echo.
echo Step 4: Restoring NuGet packages...
dotnet restore src\MyPhotoHelper.sln
if %errorlevel% neq 0 (
    echo ERROR: Failed to restore packages
    pause
    exit /b 1
) else (
    echo [OK] Packages restored successfully
)

echo.
echo Step 5: Building application...
dotnet build src\MyPhotoHelper.sln --configuration Debug >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: Build failed. Running with detailed output...
    echo.
    dotnet build src\MyPhotoHelper.sln --configuration Debug
    pause
    exit /b 1
) else (
    echo [OK] Build successful
)

echo.
echo Step 6: Creating application data directory...
set APPDATA_DIR=%APPDATA%\MyPhotoHelper
if not exist "%APPDATA_DIR%" (
    mkdir "%APPDATA_DIR%"
    echo [OK] Created: %APPDATA_DIR%
) else (
    echo [OK] Directory exists: %APPDATA_DIR%
)

echo.
echo Step 7: Checking for database...
if not exist "src\MyPhotoHelper\Database\dev_myphotohelper.db" (
    echo [INFO] Database will be created on first run
) else (
    echo [OK] Database found
)

echo.
echo ========================================
echo Setup Complete!
echo ========================================
echo.
echo You can now run MyPhotoHelper using:
echo   - scripts\start-app.bat (normal mode)
echo   - scripts\start-app-dev.bat (development mode with console output)
echo   - scripts\start-app-release.bat (optimized release mode)
echo.
echo The application will:
echo   1. Run in your system tray (look for the photo icon)
echo   2. Provide a web interface at http://localhost:5113
echo   3. Store photos database in src\MyPhotoHelper\Database\
echo.
echo Press any key to start the application now, or close this window.
pause >nul

echo.
echo Starting MyPhotoHelper...
call "%~dp0\start-app.bat"