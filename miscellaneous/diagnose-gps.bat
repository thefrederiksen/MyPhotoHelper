@echo off
echo Building GPS diagnostic tool...
csc /out:DiagnoseGPS.exe DiagnoseGPS.cs /r:System.Drawing.Common.dll

if errorlevel 1 (
    echo Failed to build diagnostic tool.
    pause
    exit /b 1
)

echo.
echo GPS Diagnostic Tool built successfully!
echo.
echo Usage: DiagnoseGPS.exe [image-path]
echo.
echo Example: DiagnoseGPS.exe "C:\Photos\IMG_1234.jpg"
echo.
echo This tool will analyze whether your images contain GPS data and help
echo diagnose any extraction issues.
echo.