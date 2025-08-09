@echo off
echo Cleaning up QA UI Tester artifacts...

REM Clean up screenshots
if exist screenshots\before (
    echo Removing before screenshots...
    rmdir /s /q screenshots\before
)

if exist screenshots\after (
    echo Removing after screenshots...
    rmdir /s /q screenshots\after
)

if exist screenshots\main (
    echo Removing main screenshots...
    rmdir /s /q screenshots\main
)

if exist screenshots\pr (
    echo Removing PR screenshots...
    rmdir /s /q screenshots\pr
)

REM Create clean directories
mkdir screenshots\before 2>nul
mkdir screenshots\after 2>nul

REM Clean up node modules if present
if exist node_modules (
    echo Removing node_modules...
    rmdir /s /q node_modules
)

REM Clean up test reports
if exist *.md (
    echo Removing test reports...
    del /q *.md
)

if exist *.log (
    echo Removing log files...
    del /q *.log
)

echo Cleanup complete!
echo.
echo Directory structure ready for next test run.