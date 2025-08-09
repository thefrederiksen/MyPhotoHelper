@echo off
REM NightlyErrorParsy - Windows Task Scheduler Script
REM Schedule this script to run nightly using Windows Task Scheduler

echo ========================================
echo NightlyErrorParsy - Starting Execution
echo ========================================
echo.
echo Date: %date%
echo Time: %time%
echo.

REM Set the working directory to the script location
cd /d "%~dp0"

REM Activate virtual environment if it exists
if exist "venv\Scripts\activate.bat" (
    echo Activating virtual environment...
    call venv\Scripts\activate.bat
)

REM Load environment variables from .env file if it exists
if exist ".env" (
    echo Loading environment variables...
    for /f "delims=" %%i in (.env) do set %%i
)

REM Check Python availability
python --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: Python is not installed or not in PATH
    exit /b 1
)

REM Install/update requirements if needed
echo Checking dependencies...
pip install -q -r requirements.txt

REM Run the main orchestrator
echo.
echo Starting NightlyErrorParsy...
echo ----------------------------------------
python nightly_error_parsy.py --mode full

REM Capture the exit code
set EXIT_CODE=%errorlevel%

echo ----------------------------------------
echo.

if %EXIT_CODE% equ 0 (
    echo SUCCESS: NightlyErrorParsy completed successfully
) else (
    echo ERROR: NightlyErrorParsy failed with exit code %EXIT_CODE%
)

echo.
echo End Time: %time%
echo ========================================

REM Exit with the same code as the Python script
exit /b %EXIT_CODE%