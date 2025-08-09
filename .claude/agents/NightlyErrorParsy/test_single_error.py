#!/usr/bin/env python3
"""
Test NightlyErrorParsy with a single error
This script allows testing without connecting to SQL Server
"""

import json
import sys
import os
from pathlib import Path

# Add agent directories to path
sys.path.append(str(Path(__file__).parent / 'DevOpsSync'))
sys.path.append(str(Path(__file__).parent / 'LLMAnalyzer'))
sys.path.append(str(Path(__file__).parent / 'ReportGenerator'))

def test_single_error_full_pipeline():
    """Test the full pipeline with one error"""
    
    print("="*60)
    print("TESTING WITH SINGLE ERROR - FULL PIPELINE")
    print("="*60)
    
    # Load test error
    with open('test_data/single_error.json', 'r') as f:
        test_data = json.load(f)
    
    error = test_data['errors'][0]
    print(f"\nTest Error: {error['log_message'][:100]}...")
    
    # Test 1: AI Analysis (if configured)
    if os.getenv('OPENAI_API_KEY'):
        print("\n" + "-"*40)
        print("TEST 1: AI Analysis")
        print("-"*40)
        
        from llm_analyzer_agent import LLMAnalyzerAgent
        analyzer = LLMAnalyzerAgent()
        
        analysis = analyzer.analyze_error(error)
        print(f"Root Cause: {analysis.get('root_cause', 'N/A')}")
        print(f"Impact: {analysis.get('impact', 'N/A')}")
        print(f"Urgency: {analysis.get('urgency', 'N/A')}/10")
        print(f"Resolution: {analysis.get('resolution', 'N/A')[:200]}...")
    else:
        print("\n⚠️  Skipping AI Analysis - No OPENAI_API_KEY set")
    
    # Test 2: DevOps Search (Dry Run)
    if os.getenv('AZURE_DEVOPS_PAT'):
        print("\n" + "-"*40)
        print("TEST 2: Search Existing Work Items")
        print("-"*40)
        
        from devops_sync_agent import DevOpsSyncAgent
        devops = DevOpsSyncAgent()
        
        # Search for similar items
        existing_items = devops.search_existing_work_items('', limit=5)
        print(f"Found {len(existing_items)} existing work items")
        
        # Check if this error would match any
        for item in existing_items:
            if devops.is_similar_error(error, item):
                print(f"✓ Would match with: #{item.get('id')} - {item.get('fields', {}).get('System.Title', '')[:50]}")
                break
        else:
            print("✗ No matches found - would create new work item")
        
        # Show what would be created (dry run)
        print("\n" + "-"*40)
        print("TEST 3: Work Item Creation (DRY RUN)")
        print("-"*40)
        
        title = devops.generate_work_item_title(error)
        tags = devops.generate_tags(error)
        
        print(f"Would create work item:")
        print(f"  Title: {title}")
        print(f"  Type: Bug")
        print(f"  Priority: {error.get('suggested_priority', 2)}")
        print(f"  Tags: {tags}")
        print(f"  Component: {error.get('affected_component', 'Unknown')}")
    else:
        print("\n⚠️  Skipping DevOps Integration - No AZURE_DEVOPS_PAT set")
    
    # Test 4: Report Generation
    print("\n" + "-"*40)
    print("TEST 4: Report Generation")
    print("-"*40)
    
    from report_generator_agent import ReportGeneratorAgent
    reporter = ReportGeneratorAgent()
    
    # Generate test report
    test_results = {
        'errors': [error],
        'work_items': {'created': [], 'updated': []},
        'summary': reporter.generate_summary_stats({'errors': [error]})
    }
    
    reports = reporter.generate_report(test_results)
    print("Generated reports:")
    for report_type, path in reports.items():
        print(f"  - {report_type}: {path}")
    
    print("\n" + "="*60)
    print("TEST COMPLETE")
    print("="*60)

def test_llm_only():
    """Test only the LLM analysis component"""
    
    print("\n" + "="*60)
    print("TESTING LLM ANALYSIS ONLY")
    print("="*60)
    
    # Create a simple test error
    test_error = {
        'log_message': 'Database connection timeout after 30 seconds',
        'log_source': 'DataAccess.Repository',
        'affected_component': 'Database',
        'severity_score': 8,
        'occurrence_count': 5
    }
    
    from llm_analyzer_agent import LLMAnalyzerAgent
    
    # Initialize with test configuration
    analyzer = LLMAnalyzerAgent()
    
    print("\nAnalyzing error with AI...")
    analysis = analyzer.analyze_error(test_error)
    
    print("\nAI Analysis Results:")
    print("-" * 40)
    for key, value in analysis.items():
        if key not in ['error_hash', 'analyzed_at']:
            print(f"{key}: {value}")

def test_devops_search_only():
    """Test only DevOps work item search"""
    
    print("\n" + "="*60)
    print("TESTING DEVOPS SEARCH ONLY")
    print("="*60)
    
    from devops_sync_agent import DevOpsSyncAgent
    
    devops = DevOpsSyncAgent()
    
    print("\nSearching for existing work items...")
    items = devops.search_existing_work_items('', limit=3)
    
    print(f"\nFound {len(items)} work items:")
    for item in items:
        fields = item.get('fields', {})
        print(f"  #{item.get('id')} - {fields.get('System.Title', 'No title')[:60]}")
        print(f"         State: {fields.get('System.State', 'Unknown')}")

def main():
    """Main test entry point"""
    import argparse
    
    parser = argparse.ArgumentParser(description='Test NightlyErrorParsy with single error')
    parser.add_argument('--mode', choices=['full', 'llm', 'devops', 'report'],
                       default='full', help='Test mode')
    parser.add_argument('--dry-run', action='store_true',
                       help='Run without creating actual work items')
    
    args = parser.parse_args()
    
    # Set dry run mode
    if args.dry_run:
        os.environ['PARSY_DRY_RUN'] = 'true'
    
    # Load environment variables from .env if exists
    if Path('.env').exists():
        from dotenv import load_dotenv
        load_dotenv()
    
    # Run selected test
    if args.mode == 'full':
        test_single_error_full_pipeline()
    elif args.mode == 'llm':
        test_llm_only()
    elif args.mode == 'devops':
        test_devops_search_only()
    else:
        print("Report test not yet implemented")

if __name__ == '__main__':
    main()