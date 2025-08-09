#!/usr/bin/env python3
"""
NightlyErrorParsy - Main Orchestrator Agent
Coordinates all sub-agents to extract, analyze, and report on system errors
"""

import os
import sys
import json
import logging
import argparse
from datetime import datetime
from pathlib import Path
from typing import Dict, Any, Optional

# Add agent directories to path
sys.path.append(str(Path(__file__).parent / 'ErrorMonitor'))
sys.path.append(str(Path(__file__).parent / 'DevOpsSync'))
sys.path.append(str(Path(__file__).parent / 'LLMAnalyzer'))
sys.path.append(str(Path(__file__).parent / 'ReportGenerator'))

from error_monitor_agent import ErrorMonitorAgent
from devops_sync_agent import DevOpsSyncAgent
from llm_analyzer_agent import LLMAnalyzerAgent
from report_generator_agent import ReportGeneratorAgent

class NightlyErrorParsy:
    """Main orchestrator for the error monitoring system"""
    
    def __init__(self, config_path: str = None):
        """Initialize the orchestrator with configuration"""
        self.config = self.load_config(config_path)
        self.setup_logging()
        self.initialize_agents()
        self.start_time = datetime.now()
        
    def load_config(self, config_path: str = None) -> Dict:
        """Load orchestrator configuration"""
        if config_path and Path(config_path).exists():
            with open(config_path, 'r') as f:
                return json.load(f)
        
        # Default configuration
        return {
            'mode': os.getenv('PARSY_MODE', 'full'),  # 'full', 'analyze', 'report'
            'dry_run': os.getenv('PARSY_DRY_RUN', 'false').lower() == 'true',
            'data_dir': 'shared/data',
            'log_dir': 'logs',
            'enable_llm': os.getenv('ENABLE_LLM', 'true').lower() == 'true',
            'enable_email': os.getenv('ENABLE_EMAIL', 'true').lower() == 'true',
            'create_work_items': os.getenv('CREATE_WORK_ITEMS', 'true').lower() == 'true',
            'max_errors_to_process': int(os.getenv('MAX_ERRORS', '100')),
            'parallel_processing': False
        }
    
    def setup_logging(self):
        """Configure logging for the orchestrator"""
        log_dir = Path(self.config['log_dir'])
        log_dir.mkdir(parents=True, exist_ok=True)
        
        log_file = log_dir / f'nightly_error_parsy_{datetime.now():%Y%m%d_%H%M%S}.log'
        
        logging.basicConfig(
            level=logging.INFO,
            format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
            handlers=[
                logging.FileHandler(log_file),
                logging.StreamHandler()
            ]
        )
        self.logger = logging.getLogger(__name__)
        self.logger.info("="*60)
        self.logger.info("NightlyErrorParsy Started")
        self.logger.info(f"Mode: {self.config['mode']}")
        self.logger.info(f"Dry Run: {self.config['dry_run']}")
        self.logger.info("="*60)
    
    def initialize_agents(self):
        """Initialize all sub-agents"""
        self.logger.info("Initializing agents...")
        
        try:
            # Initialize ErrorMonitor
            self.error_monitor = ErrorMonitorAgent()
            self.logger.info("✓ ErrorMonitor agent initialized")
            
            # Initialize DevOpsSync
            self.devops_sync = DevOpsSyncAgent()
            self.logger.info("✓ DevOpsSync agent initialized")
            
            # Initialize LLMAnalyzer if enabled
            if self.config['enable_llm']:
                self.llm_analyzer = LLMAnalyzerAgent()
                self.logger.info("✓ LLMAnalyzer agent initialized")
            else:
                self.llm_analyzer = None
                self.logger.info("⚠ LLMAnalyzer disabled")
            
            # Initialize ReportGenerator
            self.report_generator = ReportGeneratorAgent()
            self.logger.info("✓ ReportGenerator agent initialized")
            
        except Exception as e:
            self.logger.error(f"Failed to initialize agents: {str(e)}")
            raise
    
    def run(self) -> Dict[str, Any]:
        """Execute the main processing pipeline"""
        results = {
            'start_time': self.start_time.isoformat(),
            'mode': self.config['mode'],
            'success': False,
            'errors': [],
            'work_items': {'created': [], 'updated': [], 'failed': []},
            'summary': {},
            'reports': {}
        }
        
        try:
            # Step 1: Extract errors from database
            if self.config['mode'] in ['full', 'analyze']:
                self.logger.info("\n" + "="*40)
                self.logger.info("STEP 1: Extracting Errors from Database")
                self.logger.info("="*40)
                
                extraction_result = self.error_monitor.process_errors()
                
                if extraction_result['success']:
                    results['errors'] = extraction_result.get('errors', [])
                    self.logger.info(f"✓ Extracted {extraction_result['errors_found']} total errors")
                    self.logger.info(f"✓ Deduplicated to {extraction_result['unique_errors']} unique errors")
                else:
                    self.logger.error("✗ Error extraction failed")
                    results['error'] = "Failed to extract errors from database"
                    return results
                
                # Limit errors if configured
                if self.config['max_errors_to_process'] > 0:
                    results['errors'] = results['errors'][:self.config['max_errors_to_process']]
                    self.logger.info(f"ℹ Limited to {len(results['errors'])} errors for processing")
            
            # Step 2: Analyze errors with LLM
            if self.config['mode'] == 'full' and self.config['enable_llm'] and self.llm_analyzer:
                self.logger.info("\n" + "="*40)
                self.logger.info("STEP 2: Analyzing Errors with AI")
                self.logger.info("="*40)
                
                analyses = []
                for i, error in enumerate(results['errors'], 1):
                    self.logger.info(f"Analyzing error {i}/{len(results['errors'])}...")
                    analysis = self.llm_analyzer.analyze_error(error)
                    analyses.append(analysis)
                    
                    # Enrich error with analysis
                    error['ai_analysis'] = analysis
                
                # Find patterns across errors
                if len(results['errors']) > 5:
                    self.logger.info("Identifying patterns across errors...")
                    patterns = self.llm_analyzer.find_error_patterns(results['errors'])
                    results['patterns'] = patterns
                    self.logger.info(f"✓ Identified {len(patterns.get('patterns', []))} patterns")
                
                # Generate executive summary
                self.logger.info("Generating executive summary...")
                summary = self.llm_analyzer.generate_summary(results['errors'], analyses)
                results['executive_summary'] = summary
                
                self.logger.info("✓ AI analysis complete")
            
            # Step 3: Search for existing work items
            if self.config['mode'] == 'full' and not self.config['dry_run']:
                self.logger.info("\n" + "="*40)
                self.logger.info("STEP 3: Searching Existing Work Items")
                self.logger.info("="*40)
                
                existing_items = self.devops_sync.search_existing_work_items('', limit=50)
                self.logger.info(f"✓ Found {len(existing_items)} existing work items")
                
                # Match errors with work items
                if self.llm_analyzer and existing_items:
                    self.logger.info("Matching errors with existing work items...")
                    
                    for error in results['errors']:
                        matches = self.llm_analyzer.match_with_work_items(error, existing_items)
                        error['work_item_matches'] = matches
                        
                        if matches and matches[0]['similarity_score'] >= 0.85:
                            self.logger.info(f"  ✓ Found match for error: Work Item #{matches[0]['work_item_id']}")
            
            # Step 4: Create/Update work items
            if self.config['mode'] == 'full' and self.config['create_work_items'] and not self.config['dry_run']:
                self.logger.info("\n" + "="*40)
                self.logger.info("STEP 4: Creating/Updating Work Items")
                self.logger.info("="*40)
                
                # Process errors with DevOps integration
                work_items_result = self.devops_sync.process_error_batch(
                    results['errors'],
                    {'matching_work_items': []}  # Simplified for now
                )
                
                results['work_items'] = work_items_result
                
                self.logger.info(f"✓ Created {len(work_items_result['created'])} new work items")
                self.logger.info(f"✓ Updated {len(work_items_result['updated'])} existing work items")
                
                if work_items_result['failed']:
                    self.logger.warning(f"⚠ Failed to process {len(work_items_result['failed'])} errors")
            
            elif self.config['dry_run']:
                self.logger.info("\n" + "="*40)
                self.logger.info("STEP 4: Dry Run - Skipping Work Item Creation")
                self.logger.info("="*40)
                
                for error in results['errors'][:5]:
                    self.logger.info(f"Would create work item for: {error.get('log_message', '')[:100]}")
            
            # Step 5: Generate reports
            self.logger.info("\n" + "="*40)
            self.logger.info("STEP 5: Generating Reports")
            self.logger.info("="*40)
            
            # Add summary statistics
            results['summary'] = self.report_generator.generate_summary_stats(results)
            
            # Generate report files
            report_files = self.report_generator.generate_report(results)
            results['reports'] = report_files
            
            self.logger.info(f"✓ Generated {len(report_files)} report files")
            for report_type, path in report_files.items():
                self.logger.info(f"  - {report_type}: {path}")
            
            # Step 6: Send email notification
            if self.config['enable_email'] and not self.config['dry_run']:
                self.logger.info("\n" + "="*40)
                self.logger.info("STEP 6: Sending Email Notification")
                self.logger.info("="*40)
                
                if self.report_generator.send_email_report(report_files, results):
                    self.logger.info("✓ Email report sent successfully")
                else:
                    self.logger.warning("⚠ Failed to send email report")
            
            # Calculate total processing time
            processing_time = (datetime.now() - self.start_time).total_seconds()
            results['processing_time'] = processing_time
            results['end_time'] = datetime.now().isoformat()
            results['success'] = True
            
            self.logger.info("\n" + "="*60)
            self.logger.info("NightlyErrorParsy Completed Successfully")
            self.logger.info(f"Total Processing Time: {processing_time:.2f} seconds")
            self.logger.info("="*60)
            
        except Exception as e:
            self.logger.error(f"Pipeline failed: {str(e)}", exc_info=True)
            results['error'] = str(e)
            results['success'] = False
        
        finally:
            # Cleanup
            self.cleanup()
        
        return results
    
    def cleanup(self):
        """Clean up resources"""
        try:
            if hasattr(self, 'error_monitor'):
                self.error_monitor.cleanup()
            
            self.logger.info("Resources cleaned up")
        except Exception as e:
            self.logger.warning(f"Cleanup warning: {str(e)}")
    
    def save_results(self, results: Dict[str, Any]):
        """Save processing results to file"""
        data_dir = Path(self.config['data_dir'])
        data_dir.mkdir(parents=True, exist_ok=True)
        
        results_file = data_dir / f'processing_results_{datetime.now():%Y%m%d_%H%M%S}.json'
        
        with open(results_file, 'w') as f:
            json.dump(results, f, indent=2, default=str)
        
        self.logger.info(f"Results saved to: {results_file}")
        return str(results_file)


def main():
    """Main entry point"""
    parser = argparse.ArgumentParser(description='NightlyErrorParsy - Automated Error Monitoring System')
    parser.add_argument('--config', '-c', help='Configuration file path')
    parser.add_argument('--mode', '-m', choices=['full', 'analyze', 'report'],
                       help='Processing mode')
    parser.add_argument('--dry-run', action='store_true',
                       help='Run without creating work items or sending emails')
    parser.add_argument('--no-email', action='store_true',
                       help='Skip email notification')
    parser.add_argument('--no-llm', action='store_true',
                       help='Skip AI analysis')
    parser.add_argument('--max-errors', type=int,
                       help='Maximum number of errors to process')
    
    args = parser.parse_args()
    
    # Initialize orchestrator
    parsy = NightlyErrorParsy(args.config)
    
    # Override config with command line arguments
    if args.mode:
        parsy.config['mode'] = args.mode
    if args.dry_run:
        parsy.config['dry_run'] = True
    if args.no_email:
        parsy.config['enable_email'] = False
    if args.no_llm:
        parsy.config['enable_llm'] = False
    if args.max_errors:
        parsy.config['max_errors_to_process'] = args.max_errors
    
    # Run the pipeline
    results = parsy.run()
    
    # Save results
    results_file = parsy.save_results(results)
    
    # Print summary
    print("\n" + "="*60)
    print("NIGHTLY ERROR PARSY - EXECUTION SUMMARY")
    print("="*60)
    print(f"Status: {'SUCCESS' if results['success'] else 'FAILED'}")
    print(f"Errors Processed: {len(results.get('errors', []))}")
    print(f"Work Items Created: {len(results.get('work_items', {}).get('created', []))}")
    print(f"Work Items Updated: {len(results.get('work_items', {}).get('updated', []))}")
    print(f"Processing Time: {results.get('processing_time', 0):.2f} seconds")
    print(f"Results File: {results_file}")
    
    if results.get('reports'):
        print("\nGenerated Reports:")
        for report_type, path in results['reports'].items():
            print(f"  - {report_type}: {path}")
    
    print("="*60)
    
    # Exit with appropriate code
    sys.exit(0 if results['success'] else 1)


if __name__ == '__main__':
    main()