@echo off
REM GitHub Actions Status Checker for MyPhotoHelper
REM Shows recent workflow runs without opening browser

setlocal enabledelayedexpansion

echo Checking GitHub Actions status...
echo.

REM Use PowerShell to call GitHub API and format output
powershell -NoProfile -ExecutionPolicy Bypass -Command "& {
    $repo = 'thefrederiksen/MyPhotoHelper'
    $url = 'https://api.github.com/repos/' + $repo + '/actions/runs?per_page=8'
    
    try {
        # Make API request
        $response = Invoke-RestMethod -Uri $url -Headers @{
            'User-Agent' = 'MyPhotoHelper-Actions-Checker'
            'Accept' = 'application/vnd.github.v3+json'
        } -TimeoutSec 10
        
        Write-Host '🚀 Recent GitHub Actions for MyPhotoHelper' -ForegroundColor Cyan
        Write-Host ('=' * 75) -ForegroundColor Gray
        Write-Host
        
        # Header
        Write-Host ('{0,-8} {1,-25} {2,-12} {3,-15}' -f 'Status', 'Workflow', 'Branch', 'Started') -ForegroundColor Yellow
        Write-Host ('-' * 75) -ForegroundColor Gray
        
        foreach ($run in $response.workflow_runs) {
            # Get status emoji and text
            $emoji = '❓'
            $statusText = $run.status
            
            if ($run.status -eq 'in_progress') {
                $emoji = '🔄'
                $statusText = 'Running'
            } elseif ($run.status -eq 'queued') {
                $emoji = '⏳'
                $statusText = 'Queued'
            } elseif ($run.conclusion -eq 'success') {
                $emoji = '✅'
                $statusText = 'Success'
            } elseif ($run.conclusion -eq 'failure') {
                $emoji = '❌'
                $statusText = 'Failed'
            } elseif ($run.conclusion -eq 'cancelled') {
                $emoji = '🚫'
                $statusText = 'Cancelled'
            }
            
            # Format workflow name and branch
            $workflowName = $run.name
            if ($workflowName.Length -gt 24) { $workflowName = $workflowName.Substring(0, 21) + '...' }
            
            $branch = $run.head_branch
            if ($branch.Length -gt 11) { $branch = $branch.Substring(0, 8) + '...' }
            
            # Calculate time ago
            $created = [DateTime]::Parse($run.created_at)
            $now = [DateTime]::UtcNow
            $diff = $now - $created
            
            if ($diff.TotalMinutes -lt 60) {
                $timeAgo = [math]::Floor($diff.TotalMinutes).ToString() + 'm ago'
            } elseif ($diff.TotalHours -lt 24) {
                $timeAgo = [math]::Floor($diff.TotalHours).ToString() + 'h ago'
            } else {
                $timeAgo = [math]::Floor($diff.TotalDays).ToString() + 'd ago'
            }
            
            # Color based on status
            $color = 'White'
            if ($run.conclusion -eq 'success') { $color = 'Green' }
            elseif ($run.conclusion -eq 'failure') { $color = 'Red' }
            elseif ($run.status -eq 'in_progress') { $color = 'Yellow' }
            
            # Output formatted line
            $line = '{0} {1,-6} {2,-25} {3,-12} {4,-15}' -f $emoji, $statusText, $workflowName, $branch, $timeAgo
            Write-Host $line -ForegroundColor $color
        }
        
        Write-Host
        Write-Host '💡 Tips:' -ForegroundColor Cyan
        Write-Host '   • Re-run this script to refresh status'
        Write-Host ('   • View details: https://github.com/' + $repo + '/actions')
        Write-Host '   • For more info, use: check_actions.py (Python version)'
        
    } catch {
        Write-Host '❌ Error fetching workflow data:' $_.Exception.Message -ForegroundColor Red
        Write-Host 'Make sure you have internet access and try again.' -ForegroundColor Yellow
    }
}"

echo.
pause