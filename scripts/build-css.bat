@echo off
echo ========================================
echo MyPhotoHelper - Build Tailwind CSS
echo ========================================
echo.

cd /d "%~dp0\.."

echo Checking Node.js installation...
node --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: Node.js is not installed or not in PATH
    echo Please install Node.js from https://nodejs.org/
    pause
    exit /b 1
) else (
    echo [OK] Node.js found: 
    node --version
)

echo.
echo Checking npm packages...
if not exist "node_modules" (
    echo Installing npm packages...
    npm install
    if %errorlevel% neq 0 (
        echo ERROR: Failed to install npm packages
        pause
        exit /b 1
    )
) else (
    echo [OK] Node modules found
)

echo.
echo Building Tailwind CSS...
npx tailwindcss -i src/MyPhotoHelper/wwwroot/css/tailwind-input.css -o src/MyPhotoHelper/wwwroot/css/tailwind.css --minify
if %errorlevel% neq 0 (
    echo ERROR: Failed to build Tailwind CSS
    pause
    exit /b 1
)

echo.
echo [OK] Tailwind CSS built successfully!
echo Output: src/MyPhotoHelper/wwwroot/css/tailwind.css
echo.
pause