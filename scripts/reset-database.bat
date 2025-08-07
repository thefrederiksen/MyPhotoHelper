@echo off
echo ========================================
echo MyPhotoHelper - Reset Database
echo ========================================
echo.
echo WARNING: This will delete all photo data and analysis!
echo.
echo Press Ctrl+C to cancel, or any other key to continue...
pause >nul

set DB_PATH=src\MyPhotoHelper\Database

if exist "%DB_PATH%\dev_myphotohelper.db" (
    echo.
    echo Deleting database files...
    del "%DB_PATH%\dev_myphotohelper.db" /Q 2>nul
    del "%DB_PATH%\dev_myphotohelper.db-shm" /Q 2>nul
    del "%DB_PATH%\dev_myphotohelper.db-wal" /Q 2>nul
    echo Database deleted successfully.
) else (
    echo No existing database found.
)

echo.
echo Also clearing temporary Python cache...
if exist "src\MyPhotoHelper\Python\__pycache__" (
    rmdir /s /q "src\MyPhotoHelper\Python\__pycache__" 2>nul
    echo Python cache cleared.
)

echo.
echo Database has been reset. The application will create a fresh database on next startup.
echo All photo analysis and metadata will need to be regenerated.
echo.
pause