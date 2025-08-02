#!/usr/bin/env python3
"""
GitHub Actions Status Checker for MyPhotoHelper
Displays recent workflow runs and their status without opening the browser.
"""

import requests
import json
from datetime import datetime, timezone
import sys
import os

# Set UTF-8 encoding for Windows console
if sys.platform == "win32":
    import codecs
    sys.stdout = codecs.getwriter("utf-8")(sys.stdout.detach())

# Configuration
REPO_OWNER = "thefrederiksen"
REPO_NAME = "MyPhotoHelper"
GITHUB_API_BASE = "https://api.github.com"

def get_github_token():
    """Get GitHub token from environment or return None for public access."""
    return os.environ.get('GITHUB_TOKEN')

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

def get_status_emoji(status, conclusion):
    """Get emoji for workflow status."""
    if status == "in_progress":
        return "🔄"
    elif status == "queued":
        return "⏳"
    elif conclusion == "success":
        return "✅"
    elif conclusion == "failure":
        return "❌"
    elif conclusion == "cancelled":
        return "🚫"
    elif conclusion == "skipped":
        return "⏭️"
    else:
        return "❓"

def fetch_workflow_runs():
    """Fetch recent workflow runs from GitHub API."""
    url = f"{GITHUB_API_BASE}/repos/{REPO_OWNER}/{REPO_NAME}/actions/runs"
    
    headers = {
        "Accept": "application/vnd.github.v3+json",
        "User-Agent": "MyPhotoHelper-Actions-Checker"
    }
    
    # Add authorization if token is available
    token = get_github_token()
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
        print(f"❌ Error fetching workflow runs: {e}")
        return None

def display_workflow_runs(data):
    """Display workflow runs in a formatted table."""
    if not data or 'workflow_runs' not in data:
        print("❌ No workflow data available")
        return
    
    runs = data['workflow_runs']
    
    if not runs:
        print("📝 No recent workflow runs found")
        return
    
    print("🚀 Recent GitHub Actions for MyPhotoHelper")
    print("=" * 80)
    print()
    
    # Header
    print(f"{'Status':<8} {'Workflow':<25} {'Branch':<12} {'Started':<12} {'Duration':<10}")
    print("-" * 80)
    
    for run in runs:
        status = run.get('status', 'unknown')
        conclusion = run.get('conclusion', '')
        name = run.get('name', 'Unknown')[:24]
        branch = run.get('head_branch', 'unknown')[:11]
        created_at = run.get('created_at', '')
        updated_at = run.get('updated_at', '')
        
        # Get emoji and status text
        emoji = get_status_emoji(status, conclusion)
        if status == "in_progress":
            status_text = "Running"
        elif status == "queued":
            status_text = "Queued"
        elif conclusion:
            status_text = conclusion.title()
        else:
            status_text = status.title()
        
        # Format times
        time_ago = format_time_ago(created_at)
        duration = format_duration(created_at, updated_at) if status == "completed" else "..."
        
        print(f"{emoji} {status_text:<6} {name:<25} {branch:<12} {time_ago:<12} {duration:<10}")
    
    print()
    print("💡 Tips:")
    print("   • Set GITHUB_TOKEN environment variable for higher API rate limits")
    print("   • Re-run this script to refresh status")
    print(f"   • View details: https://github.com/{REPO_OWNER}/{REPO_NAME}/actions")

def main():
    """Main function."""
    print("Checking GitHub Actions status...")
    print()
    
    data = fetch_workflow_runs()
    if data:
        display_workflow_runs(data)
    else:
        print("❌ Failed to fetch workflow data")
        sys.exit(1)

if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n\n👋 Cancelled by user")
        sys.exit(0)
    except Exception as e:
        print(f"\n❌ Unexpected error: {e}")
        sys.exit(1)