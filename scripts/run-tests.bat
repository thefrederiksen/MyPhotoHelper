@echo off
echo ========================================
echo MyPhotoHelper - Run Tests
echo ========================================
echo.

cd /d "%~dp0\.."

echo Running C# Unit Tests...
echo ------------------------
dotnet test src\MyPhotoHelper.Tests --logger "console;verbosity=normal"
if %errorlevel% neq 0 (
    echo.
    echo WARNING: Some C# tests failed
    set TEST_FAILED=1
) else (
    echo [OK] All C# tests passed
)

echo.
echo Running Python Tests...
echo ----------------------
cd src\MyPhotoHelper\Python
python -m pytest test\ -v
if %errorlevel% neq 0 (
    echo.
    echo WARNING: Some Python tests failed
    set TEST_FAILED=1
) else (
    echo [OK] All Python tests passed
)

cd ..\..\..

echo.
echo ========================================
if defined TEST_FAILED (
    echo TEST RESULTS: Some tests failed
    echo Please review the output above for details
) else (
    echo TEST RESULTS: All tests passed successfully!
)
echo ========================================
echo.
pause