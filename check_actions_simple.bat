@echo off
REM Simple GitHub Actions Status Checker for MyPhotoHelper
REM Uses curl to check GitHub API

echo Checking GitHub Actions status...
echo.

REM Check if curl is available
curl --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: curl not found. Using PowerShell fallback...
    goto :powershell_version
)

REM Use curl to get GitHub Actions data
echo GitHub Actions Status for MyPhotoHelper
echo ================================================================================
echo.

curl -s "https://api.github.com/repos/thefrederiksen/MyPhotoHelper/actions/runs?per_page=5" | findstr /R "\"name\":\|\"status\":\|\"conclusion\":\|\"head_branch\":\|\"created_at\":" | more

echo.
echo View details: https://github.com/thefrederiksen/MyPhotoHelper/actions
echo.
pause
exit /b

:powershell_version
REM Fallback to PowerShell for systems without curl
powershell -NoProfile -ExecutionPolicy Bypass -Command "& {
    try {
        $response = Invoke-RestMethod -Uri 'https://api.github.com/repos/thefrederiksen/MyPhotoHelper/actions/runs?per_page=5' -TimeoutSec 10
        
        Write-Host 'GitHub Actions Status for MyPhotoHelper'
        Write-Host ('=' * 60)
        Write-Host
        
        foreach ($run in $response.workflow_runs) {
            $status = if ($run.status -eq 'completed') { $run.conclusion } else { $run.status }
            $timeAgo = [math]::Floor(([DateTime]::UtcNow - [DateTime]::Parse($run.created_at)).TotalMinutes).ToString() + 'm ago'
            
            Write-Host ('Status: {0,-12} Workflow: {1,-20} Branch: {2,-10} Time: {3}' -f $status, $run.name, $run.head_branch, $timeAgo)
        }
        
        Write-Host
        Write-Host 'View details: https://github.com/thefrederiksen/MyPhotoHelper/actions'
        
    } catch {
        Write-Host 'ERROR: Failed to fetch workflow data' -ForegroundColor Red
    }
}"

echo.
pause