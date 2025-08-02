#!/usr/bin/env python3
"""
GitHub Actions Status Checker for MyPhotoHelper (Simple Version)
Displays recent workflow runs without emojis for better Windows compatibility.
"""

import requests
import json
from datetime import datetime, timezone
import sys
import os

# Configuration
REPO_OWNER = "thefrederiksen"
REPO_NAME = "MyPhotoHelper"
GITHUB_API_BASE = "https://api.github.com"

def format_duration(start_time, end_time):
    """Format duration between two ISO timestamps."""
    if not start_time or not end_time:
        return "N/A"
    
    start = datetime.fromisoformat(start_time.replace('Z', '+00:00'))
    end = datetime.fromisoformat(end_time.replace('Z', '+00:00'))
    duration = end - start
    
    total_seconds = int(duration.total_seconds())
    minutes = total_seconds // 60
    seconds = total_seconds % 60
    
    if minutes > 0:
        return f"{minutes}m {seconds}s"
    else:
        return f"{seconds}s"

def format_time_ago(timestamp):
    """Format how long ago a timestamp was."""
    if not timestamp:
        return "N/A"
    
    time = datetime.fromisoformat(timestamp.replace('Z', '+00:00'))
    now = datetime.now(timezone.utc)
    diff = now - time
    
    total_seconds = int(diff.total_seconds())
    
    if total_seconds < 60:
        return f"{total_seconds}s ago"
    elif total_seconds < 3600:
        minutes = total_seconds // 60
        return f"{minutes}m ago"
    elif total_seconds < 86400:
        hours = total_seconds // 3600
        return f"{hours}h ago"
    else:
        days = total_seconds // 86400
        return f"{days}d ago"

def get_status_symbol(status, conclusion):
    """Get symbol for workflow status."""
    if status == "in_progress":
        return "[RUNNING]"
    elif status == "queued":
        return "[QUEUED]"
    elif conclusion == "success":
        return "[SUCCESS]"
    elif conclusion == "failure":
        return "[FAILED] "
    elif conclusion == "cancelled":
        return "[CANCEL] "
    elif conclusion == "skipped":
        return "[SKIPPED]"
    else:
        return "[UNKNOWN]"

def fetch_workflow_runs():
    """Fetch recent workflow runs from GitHub API."""
    url = f"{GITHUB_API_BASE}/repos/{REPO_OWNER}/{REPO_NAME}/actions/runs"
    
    headers = {
        "Accept": "application/vnd.github.v3+json",
        "User-Agent": "MyPhotoHelper-Actions-Checker"
    }
    
    # Add authorization if token is available
    token = os.environ.get('GITHUB_TOKEN')
    if token:
        headers["Authorization"] = f"token {token}"
    
    params = {
        "per_page": 10,  # Get last 10 runs
        "page": 1
    }
    
    try:
        response = requests.get(url, headers=headers, params=params, timeout=10)
        response.raise_for_status()
        return response.json()
    except requests.exceptions.RequestException as e:
        print(f"ERROR: Failed to fetch workflow runs: {e}")
        return None

def display_workflow_runs(data):
    """Display workflow runs in a formatted table."""
    if not data or 'workflow_runs' not in data:
        print("ERROR: No workflow data available")
        return
    
    runs = data['workflow_runs']
    
    if not runs:
        print("INFO: No recent workflow runs found")
        return
    
    print("GitHub Actions Status for MyPhotoHelper")
    print("=" * 80)
    print()
    
    # Header
    print(f"{'Status':<12} {'Workflow':<30} {'Branch':<12} {'Started':<12} {'Duration':<10}")
    print("-" * 80)
    
    for run in runs:
        status = run.get('status', 'unknown')
        conclusion = run.get('conclusion', '')
        name = run.get('name', 'Unknown')[:29]
        branch = run.get('head_branch', 'unknown')[:11]
        created_at = run.get('created_at', '')
        updated_at = run.get('updated_at', '')
        
        # Get status symbol
        status_symbol = get_status_symbol(status, conclusion)
        
        # Format times
        time_ago = format_time_ago(created_at)
        duration = format_duration(created_at, updated_at) if status == "completed" else "..."
        
        print(f"{status_symbol:<12} {name:<30} {branch:<12} {time_ago:<12} {duration:<10}")
    
    print()
    print("Tips:")
    print("  - Set GITHUB_TOKEN environment variable for higher API rate limits")
    print("  - Re-run this script to refresh status")
    print(f"  - View details: https://github.com/{REPO_OWNER}/{REPO_NAME}/actions")

def main():
    """Main function."""
    print("Checking GitHub Actions status...")
    print()
    
    data = fetch_workflow_runs()
    if data:
        display_workflow_runs(data)
    else:
        print("ERROR: Failed to fetch workflow data")
        sys.exit(1)

if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\nCancelled by user")
        sys.exit(0)
    except Exception as e:
        print(f"\nERROR: Unexpected error: {e}")
        sys.exit(1)