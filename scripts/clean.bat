@echo off
echo ========================================
echo MyPhotoHelper - Clean Build Artifacts
echo ========================================
echo.

cd /d "%~dp0\.."

echo Cleaning C# build artifacts...
dotnet clean src\MyPhotoHelper.sln >nul 2>&1
if exist "src\MyPhotoHelper\bin" rmdir /s /q "src\MyPhotoHelper\bin" 2>nul
if exist "src\MyPhotoHelper\obj" rmdir /s /q "src\MyPhotoHelper\obj" 2>nul
if exist "src\MyPhotoHelper.Tests\bin" rmdir /s /q "src\MyPhotoHelper.Tests\bin" 2>nul
if exist "src\MyPhotoHelper.Tests\obj" rmdir /s /q "src\MyPhotoHelper.Tests\obj" 2>nul
echo [OK] C# artifacts cleaned

echo.
echo Cleaning Python cache...
if exist "src\MyPhotoHelper\Python\__pycache__" rmdir /s /q "src\MyPhotoHelper\Python\__pycache__" 2>nul
if exist "src\MyPhotoHelper\Python\test\__pycache__" rmdir /s /q "src\MyPhotoHelper\Python\test\__pycache__" 2>nul
for /d /r "src\MyPhotoHelper\Python" %%d in (__pycache__) do @if exist "%%d" rmdir /s /q "%%d" 2>nul
echo [OK] Python cache cleaned

echo.
echo Cleaning temporary files...
if exist "src\MyPhotoHelper\temp" rmdir /s /q "src\MyPhotoHelper\temp" 2>nul
del /s /q *.tmp 2>nul
echo [OK] Temporary files cleaned

echo.
echo ========================================
echo Clean complete!
echo ========================================
echo.
pause