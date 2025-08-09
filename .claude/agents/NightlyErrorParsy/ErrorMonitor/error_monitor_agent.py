#!/usr/bin/env python3
"""
ErrorMonitor Agent - SQL Server Error Log Extraction
Monitors and extracts error logs from SQL Server database for analysis
"""

import os
import json
import pyodbc
import hashlib
from datetime import datetime, timedelta
from typing import List, Dict, Optional, Any
from pathlib import Path
import logging

class ErrorMonitorAgent:
    """Agent for monitoring and extracting errors from SQL Server"""
    
    def __init__(self, config_path: str = None):
        """Initialize ErrorMonitor with database configuration"""
        self.config = self.load_config(config_path)
        self.connection = None
        self.last_run_file = Path(self.config['data_dir']) / 'last_run.json'
        self.setup_logging()
        
    def load_config(self, config_path: str = None) -> Dict:
        """Load configuration from file or environment"""
        if config_path and Path(config_path).exists():
            with open(config_path, 'r') as f:
                return json.load(f)
        
        # Default configuration from environment or defaults
        return {
            'server': os.getenv('SQL_SERVER', 'localhost'),
            'database': os.getenv('SQL_DATABASE', 'mindzie'),
            'username': os.getenv('SQL_USERNAME', 'svc_nightlyerrorparsy'),
            'password': os.getenv('SQL_PASSWORD', ''),
            'table': 'tbl_global_log',
            'data_dir': '../shared/data',
            'log_dir': '../logs',
            'batch_size': 100,
            'lookback_hours': 24,
            'dedup_threshold': 0.85
        }
    
    def setup_logging(self):
        """Configure logging for the agent"""
        log_dir = Path(self.config['log_dir'])
        log_dir.mkdir(parents=True, exist_ok=True)
        
        logging.basicConfig(
            level=logging.INFO,
            format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
            handlers=[
                logging.FileHandler(log_dir / f'error_monitor_{datetime.now():%Y%m%d}.log'),
                logging.StreamHandler()
            ]
        )
        self.logger = logging.getLogger(__name__)
    
    def connect_to_database(self) -> bool:
        """Establish connection to SQL Server"""
        try:
            connection_string = (
                f"DRIVER={{ODBC Driver 17 for SQL Server}};"
                f"SERVER={self.config['server']};"
                f"DATABASE={self.config['database']};"
                f"UID={self.config['username']};"
                f"PWD={self.config['password']}"
            )
            
            self.connection = pyodbc.connect(connection_string)
            self.logger.info(f"Connected to SQL Server: {self.config['server']}/{self.config['database']}")
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to connect to database: {str(e)}")
            return False
    
    def get_last_run_timestamp(self) -> Optional[datetime]:
        """Get the timestamp of the last successful run"""
        if self.last_run_file.exists():
            try:
                with open(self.last_run_file, 'r') as f:
                    data = json.load(f)
                    return datetime.fromisoformat(data['last_run'])
            except Exception as e:
                self.logger.warning(f"Could not read last run file: {e}")
        
        # Default to 24 hours ago
        return datetime.now() - timedelta(hours=self.config['lookback_hours'])
    
    def save_last_run_timestamp(self, timestamp: datetime):
        """Save the timestamp of the current run"""
        self.last_run_file.parent.mkdir(parents=True, exist_ok=True)
        
        with open(self.last_run_file, 'w') as f:
            json.dump({
                'last_run': timestamp.isoformat(),
                'errors_processed': self.errors_processed,
                'unique_errors': self.unique_errors_found
            }, f, indent=2)
    
    def extract_errors(self, since: datetime = None) -> List[Dict[str, Any]]:
        """Extract error logs from SQL Server since last run"""
        if not self.connection:
            if not self.connect_to_database():
                return []
        
        if not since:
            since = self.get_last_run_timestamp()
        
        query = f"""
        SELECT 
            log_id,
            log_timestamp,
            log_level,
            log_message,
            log_source,
            log_details,
            application_name,
            application_version,
            user_id,
            session_id,
            stack_trace,
            is_error_visible
        FROM {self.config['table']}
        WHERE log_timestamp > ?
            AND log_level IN ('ERROR', 'FATAL', 'CRITICAL')
            AND is_error_visible = 1
        ORDER BY log_timestamp DESC
        """
        
        try:
            cursor = self.connection.cursor()
            cursor.execute(query, since)
            
            errors = []
            columns = [column[0] for column in cursor.description]
            
            for row in cursor:
                error = dict(zip(columns, row))
                # Convert datetime objects to strings
                if error.get('log_timestamp'):
                    error['log_timestamp'] = error['log_timestamp'].isoformat()
                errors.append(error)
            
            self.logger.info(f"Extracted {len(errors)} errors since {since}")
            return errors
            
        except Exception as e:
            self.logger.error(f"Failed to extract errors: {str(e)}")
            return []
    
    def deduplicate_errors(self, errors: List[Dict]) -> List[Dict]:
        """Remove duplicate errors based on similarity"""
        if not errors:
            return []
        
        unique_errors = []
        seen_hashes = set()
        
        for error in errors:
            # Create a hash of the error message and source
            error_key = self.generate_error_hash(error)
            
            if error_key not in seen_hashes:
                seen_hashes.add(error_key)
                # Group similar errors
                error['occurrence_count'] = 1
                error['first_seen'] = error['log_timestamp']
                error['last_seen'] = error['log_timestamp']
                unique_errors.append(error)
            else:
                # Update occurrence count for existing error
                for unique_error in unique_errors:
                    if self.generate_error_hash(unique_error) == error_key:
                        unique_error['occurrence_count'] += 1
                        unique_error['last_seen'] = error['log_timestamp']
                        break
        
        self.logger.info(f"Deduplicated {len(errors)} errors to {len(unique_errors)} unique errors")
        return unique_errors
    
    def generate_error_hash(self, error: Dict) -> str:
        """Generate a hash for error deduplication"""
        # Normalize the error message by removing timestamps, IDs, etc.
        message = error.get('log_message', '')
        source = error.get('log_source', '')
        
        # Remove common variable parts
        import re
        message = re.sub(r'\d{4}-\d{2}-\d{2}', 'DATE', message)
        message = re.sub(r'\d{2}:\d{2}:\d{2}', 'TIME', message)
        message = re.sub(r'#\d+', '#ID', message)
        message = re.sub(r'0x[0-9a-fA-F]+', '0xHEX', message)
        
        # Create hash
        hash_input = f"{source}:{message[:200]}"  # Use first 200 chars
        return hashlib.md5(hash_input.encode()).hexdigest()
    
    def enrich_error_data(self, errors: List[Dict]) -> List[Dict]:
        """Add additional context and metadata to errors"""
        for error in errors:
            # Add severity score
            error['severity_score'] = self.calculate_severity(error)
            
            # Add category
            error['category'] = self.categorize_error(error)
            
            # Add affected component
            error['affected_component'] = self.identify_component(error)
            
            # Add suggested priority
            error['suggested_priority'] = self.suggest_priority(error)
        
        return errors
    
    def calculate_severity(self, error: Dict) -> int:
        """Calculate severity score (1-10) based on error characteristics"""
        score = 5  # Base score
        
        # Adjust based on log level
        if error.get('log_level') == 'FATAL':
            score += 3
        elif error.get('log_level') == 'CRITICAL':
            score += 2
        elif error.get('log_level') == 'ERROR':
            score += 1
        
        # Adjust based on occurrence count
        occurrences = error.get('occurrence_count', 1)
        if occurrences > 100:
            score += 2
        elif occurrences > 10:
            score += 1
        
        # Check for specific keywords
        message = error.get('log_message', '').lower()
        if any(word in message for word in ['database', 'connection', 'timeout']):
            score += 1
        if any(word in message for word in ['security', 'authentication', 'unauthorized']):
            score += 2
        
        return min(score, 10)  # Cap at 10
    
    def categorize_error(self, error: Dict) -> str:
        """Categorize error based on content"""
        message = error.get('log_message', '').lower()
        source = error.get('log_source', '').lower()
        
        if 'database' in message or 'sql' in message:
            return 'Database'
        elif 'auth' in message or 'security' in message:
            return 'Security'
        elif 'network' in message or 'timeout' in message:
            return 'Network'
        elif 'ui' in source or 'view' in source:
            return 'Frontend'
        elif 'api' in source or 'service' in source:
            return 'API'
        else:
            return 'General'
    
    def identify_component(self, error: Dict) -> str:
        """Identify the affected component from error details"""
        source = error.get('log_source', '')
        app_name = error.get('application_name', '')
        
        # Try to extract component from source
        if '.' in source:
            parts = source.split('.')
            if len(parts) >= 2:
                return '.'.join(parts[:2])
        
        # Fall back to application name
        if app_name:
            return app_name
        
        return 'Unknown'
    
    def suggest_priority(self, error: Dict) -> int:
        """Suggest priority for Azure DevOps (1=High, 2=Medium, 3=Low)"""
        severity = error.get('severity_score', 5)
        
        if severity >= 8:
            return 1  # High
        elif severity >= 5:
            return 2  # Medium
        else:
            return 3  # Low
    
    def process_errors(self) -> Dict[str, Any]:
        """Main processing pipeline for error extraction"""
        self.errors_processed = 0
        self.unique_errors_found = 0
        
        start_time = datetime.now()
        self.logger.info(f"Starting error extraction at {start_time}")
        
        # Extract errors
        errors = self.extract_errors()
        self.errors_processed = len(errors)
        
        if not errors:
            self.logger.info("No new errors found")
            return {
                'success': True,
                'errors_found': 0,
                'unique_errors': 0,
                'processing_time': (datetime.now() - start_time).total_seconds()
            }
        
        # Deduplicate
        unique_errors = self.deduplicate_errors(errors)
        self.unique_errors_found = len(unique_errors)
        
        # Enrich with metadata
        enriched_errors = self.enrich_error_data(unique_errors)
        
        # Save results
        output_file = Path(self.config['data_dir']) / f'errors_{datetime.now():%Y%m%d_%H%M%S}.json'
        output_file.parent.mkdir(parents=True, exist_ok=True)
        
        with open(output_file, 'w') as f:
            json.dump({
                'extraction_timestamp': start_time.isoformat(),
                'total_errors': self.errors_processed,
                'unique_errors': self.unique_errors_found,
                'errors': enriched_errors
            }, f, indent=2)
        
        # Update last run timestamp
        self.save_last_run_timestamp(start_time)
        
        processing_time = (datetime.now() - start_time).total_seconds()
        self.logger.info(f"Processing complete in {processing_time:.2f} seconds")
        
        return {
            'success': True,
            'errors_found': self.errors_processed,
            'unique_errors': self.unique_errors_found,
            'output_file': str(output_file),
            'processing_time': processing_time,
            'errors': enriched_errors
        }
    
    def get_error_summary(self, errors: List[Dict]) -> Dict[str, Any]:
        """Generate summary statistics for errors"""
        if not errors:
            return {}
        
        categories = {}
        components = {}
        severities = {'High': 0, 'Medium': 0, 'Low': 0}
        
        for error in errors:
            # Count by category
            cat = error.get('category', 'Unknown')
            categories[cat] = categories.get(cat, 0) + 1
            
            # Count by component
            comp = error.get('affected_component', 'Unknown')
            components[comp] = components.get(comp, 0) + 1
            
            # Count by priority
            priority = error.get('suggested_priority', 2)
            if priority == 1:
                severities['High'] += 1
            elif priority == 2:
                severities['Medium'] += 1
            else:
                severities['Low'] += 1
        
        return {
            'total_unique_errors': len(errors),
            'categories': categories,
            'components': components,
            'severities': severities,
            'top_occurring': sorted(errors, key=lambda x: x.get('occurrence_count', 1), reverse=True)[:5]
        }
    
    def cleanup(self):
        """Clean up resources"""
        if self.connection:
            self.connection.close()
            self.logger.info("Database connection closed")


def main():
    """Main entry point for ErrorMonitor agent"""
    import argparse
    
    parser = argparse.ArgumentParser(description='ErrorMonitor - SQL Server Error Extraction Agent')
    parser.add_argument('--config', '-c', help='Configuration file path')
    parser.add_argument('--since', '-s', help='Extract errors since (ISO format datetime)')
    parser.add_argument('--summary', action='store_true', help='Show summary only')
    
    args = parser.parse_args()
    
    # Initialize agent
    monitor = ErrorMonitorAgent(args.config)
    
    try:
        # Process errors
        if args.since:
            since = datetime.fromisoformat(args.since)
            errors = monitor.extract_errors(since)
        else:
            result = monitor.process_errors()
            
            if args.summary and result.get('errors'):
                summary = monitor.get_error_summary(result['errors'])
                print(json.dumps(summary, indent=2))
            else:
                print(json.dumps(result, indent=2))
    
    finally:
        monitor.cleanup()


if __name__ == '__main__':
    main()