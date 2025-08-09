#!/usr/bin/env python3
"""
DevOpsSync Agent - Azure DevOps Work Item Management
Manages work item creation, updates, and synchronization for error tracking
"""

import os
import json
import subprocess
from datetime import datetime
from typing import List, Dict, Optional, Any
from pathlib import Path
import logging
import hashlib

class DevOpsSyncAgent:
    """Agent for managing Azure DevOps work items"""
    
    def __init__(self, config_path: str = None):
        """Initialize DevOpsSync with Azure DevOps configuration"""
        self.config = self.load_config(config_path)
        self.work_items_cache = {}
        self.setup_logging()
        self.authenticate()
        
    def load_config(self, config_path: str = None) -> Dict:
        """Load configuration from file or environment"""
        if config_path and Path(config_path).exists():
            with open(config_path, 'r') as f:
                return json.load(f)
        
        return {
            'organization': os.getenv('AZURE_DEVOPS_ORG', 'https://dev.azure.com/mindzie'),
            'project': os.getenv('AZURE_DEVOPS_PROJECT', 'mindzieStudio1'),
            'pat': os.getenv('AZURE_DEVOPS_PAT', ''),
            'area': 'mindzieStudio1',
            'iteration': 'mindzieStudio1\\New AI Coding Sprint',
            'default_type': 'Bug',
            'cache_dir': '../shared/cache',
            'log_dir': '../logs',
            'batch_size': 10,
            'similarity_threshold': 0.85
        }
    
    def setup_logging(self):
        """Configure logging for the agent"""
        log_dir = Path(self.config['log_dir'])
        log_dir.mkdir(parents=True, exist_ok=True)
        
        logging.basicConfig(
            level=logging.INFO,
            format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
            handlers=[
                logging.FileHandler(log_dir / f'devops_sync_{datetime.now():%Y%m%d}.log'),
                logging.StreamHandler()
            ]
        )
        self.logger = logging.getLogger(__name__)
    
    def authenticate(self):
        """Set up Azure DevOps authentication"""
        if self.config['pat']:
            os.environ['AZURE_DEVOPS_PAT'] = self.config['pat']
            self.logger.info("Azure DevOps PAT configured")
        else:
            self.logger.warning("No PAT configured - authentication may fail")
    
    def search_existing_work_items(self, error_signature: str, limit: int = 10) -> List[Dict]:
        """Search for existing work items that might match the error"""
        # Build WIQL query
        query = f"""
        SELECT [System.Id], [System.Title], [System.State], [System.Description], 
               [System.Tags], [System.CreatedDate], [System.ChangedDate]
        FROM workitems
        WHERE [System.TeamProject] = '{self.config['project']}'
            AND [System.WorkItemType] = 'Bug'
            AND [System.State] <> 'Closed'
            AND [System.State] <> 'Resolved'
        ORDER BY [System.ChangedDate] DESC
        """
        
        try:
            cmd = [
                'az', 'boards', 'query',
                '--wiql', query,
                '--project', self.config['project'],
                '-o', 'json'
            ]
            
            result = subprocess.run(cmd, capture_output=True, text=True, check=True)
            work_items = json.loads(result.stdout)
            
            # Filter to top N items
            if isinstance(work_items, list):
                work_items = work_items[:limit]
            
            # Get detailed information for each work item
            detailed_items = []
            for item in work_items:
                details = self.get_work_item_details(item.get('id'))
                if details:
                    detailed_items.append(details)
            
            self.logger.info(f"Found {len(detailed_items)} existing work items to check")
            return detailed_items
            
        except Exception as e:
            self.logger.error(f"Failed to search work items: {str(e)}")
            return []
    
    def get_work_item_details(self, work_item_id: int) -> Optional[Dict]:
        """Get detailed information for a specific work item"""
        try:
            cmd = [
                'az', 'boards', 'work-item', 'show',
                '--id', str(work_item_id),
                '--project', self.config['project'],
                '-o', 'json'
            ]
            
            result = subprocess.run(cmd, capture_output=True, text=True, check=True)
            return json.loads(result.stdout)
            
        except Exception as e:
            self.logger.error(f"Failed to get work item {work_item_id}: {str(e)}")
            return None
    
    def create_work_item(self, error: Dict, similarity_results: Dict = None) -> Optional[Dict]:
        """Create a new work item for an error"""
        title = self.generate_work_item_title(error)
        description = self.generate_work_item_description(error, similarity_results)
        
        # Prepare fields
        fields = {
            'System.Title': title,
            'System.Description': description,
            'System.Tags': self.generate_tags(error),
            'Microsoft.VSTS.Common.Priority': error.get('suggested_priority', 2),
            'System.AreaPath': self.config['area'],
            'System.IterationPath': self.config['iteration']
        }
        
        # Add custom fields
        if error.get('affected_component'):
            fields['Custom.Component'] = error['affected_component']
        
        try:
            # Build command
            cmd = [
                'az', 'boards', 'work-item', 'create',
                '--title', title,
                '--type', self.config['default_type'],
                '--project', self.config['project'],
                '--area', self.config['area'],
                '--iteration', self.config['iteration'],
                '--description', description,
                '--fields'
            ]
            
            # Add fields
            for key, value in fields.items():
                if key not in ['System.Title', 'System.Description']:
                    cmd.append(f'{key}={value}')
            
            cmd.extend(['-o', 'json'])
            
            result = subprocess.run(cmd, capture_output=True, text=True, check=True)
            work_item = json.loads(result.stdout)
            
            self.logger.info(f"Created work item #{work_item['id']}: {title}")
            
            # Add comment with error details
            self.add_work_item_comment(work_item['id'], error)
            
            return work_item
            
        except Exception as e:
            self.logger.error(f"Failed to create work item: {str(e)}")
            return None
    
    def update_work_item(self, work_item_id: int, error: Dict, update_type: str = 'recurrence') -> bool:
        """Update an existing work item with new error information"""
        try:
            if update_type == 'recurrence':
                # Add comment about recurrence
                comment = self.generate_recurrence_comment(error)
                
                cmd = [
                    'az', 'boards', 'work-item', 'update',
                    '--id', str(work_item_id),
                    '--discussion', comment,
                    '--project', self.config['project'],
                    '-o', 'json'
                ]
                
                subprocess.run(cmd, capture_output=True, text=True, check=True)
                
                # Update tags to include recurrence count
                self.update_work_item_tags(work_item_id, error)
                
                self.logger.info(f"Updated work item #{work_item_id} with recurrence info")
                return True
            
        except Exception as e:
            self.logger.error(f"Failed to update work item {work_item_id}: {str(e)}")
            return False
    
    def generate_work_item_title(self, error: Dict) -> str:
        """Generate a concise title for the work item"""
        message = error.get('log_message', 'Unknown Error')
        component = error.get('affected_component', '')
        
        # Truncate message if too long
        if len(message) > 100:
            message = message[:97] + '...'
        
        if component:
            return f"[{component}] {message}"
        return message
    
    def generate_work_item_description(self, error: Dict, similarity_results: Dict = None) -> str:
        """Generate detailed description for the work item"""
        description = f"""
<h2>Error Details</h2>
<p><strong>First Occurred:</strong> {error.get('first_seen', 'Unknown')}</p>
<p><strong>Last Occurred:</strong> {error.get('last_seen', 'Unknown')}</p>
<p><strong>Occurrences:</strong> {error.get('occurrence_count', 1)}</p>
<p><strong>Severity Score:</strong> {error.get('severity_score', 'N/A')}/10</p>
<p><strong>Category:</strong> {error.get('category', 'Unknown')}</p>
<p><strong>Component:</strong> {error.get('affected_component', 'Unknown')}</p>

<h3>Error Message</h3>
<pre>{error.get('log_message', 'No message available')}</pre>

<h3>Source</h3>
<p>{error.get('log_source', 'Unknown source')}</p>
"""
        
        # Add stack trace if available
        if error.get('stack_trace'):
            description += f"""
<h3>Stack Trace</h3>
<pre>{error['stack_trace'][:2000]}</pre>
"""
        
        # Add application info
        if error.get('application_name'):
            description += f"""
<h3>Application Information</h3>
<p><strong>Application:</strong> {error['application_name']}</p>
<p><strong>Version:</strong> {error.get('application_version', 'Unknown')}</p>
"""
        
        # Add AI analysis if available
        if similarity_results:
            description += f"""
<h3>AI Analysis</h3>
<p>{similarity_results.get('analysis', 'No analysis available')}</p>
<p><strong>Confidence:</strong> {similarity_results.get('confidence', 'N/A')}%</p>
"""
        
        description += """
<hr>
<p><em>This work item was automatically created by NightlyErrorParsy</em></p>
"""
        
        return description
    
    def generate_tags(self, error: Dict) -> str:
        """Generate tags for the work item"""
        tags = ['AutoGenerated', 'ErrorMonitoring']
        
        # Add category as tag
        if error.get('category'):
            tags.append(error['category'])
        
        # Add severity tag
        severity = error.get('severity_score', 5)
        if severity >= 8:
            tags.append('Critical')
        elif severity >= 6:
            tags.append('High')
        elif severity >= 4:
            tags.append('Medium')
        else:
            tags.append('Low')
        
        # Add component tag
        if error.get('affected_component'):
            component = error['affected_component'].replace('.', '_')
            tags.append(component)
        
        return ','.join(tags)
    
    def add_work_item_comment(self, work_item_id: int, error: Dict):
        """Add a detailed comment to the work item"""
        comment = f"""
## Error Tracking Information

**Error ID:** {error.get('log_id', 'N/A')}
**Session ID:** {error.get('session_id', 'N/A')}
**User ID:** {error.get('user_id', 'N/A')}

### Additional Details
{json.dumps(error.get('log_details', {}), indent=2) if error.get('log_details') else 'No additional details'}

### Recommended Actions
1. Review the error message and stack trace
2. Check recent deployments for related changes
3. Verify the affected component's health
4. Review similar errors in the same timeframe

*Timestamp: {datetime.now().isoformat()}*
"""
        
        try:
            cmd = [
                'az', 'boards', 'work-item', 'update',
                '--id', str(work_item_id),
                '--discussion', comment,
                '--project', self.config['project'],
                '-o', 'json'
            ]
            
            subprocess.run(cmd, capture_output=True, text=True, check=True)
            
        except Exception as e:
            self.logger.warning(f"Failed to add comment to work item {work_item_id}: {str(e)}")
    
    def generate_recurrence_comment(self, error: Dict) -> str:
        """Generate comment for recurring error"""
        return f"""
## Error Recurrence Detected

This error has occurred again:
- **New Occurrences:** {error.get('occurrence_count', 1)}
- **Last Seen:** {error.get('last_seen', 'Unknown')}
- **Severity Score:** {error.get('severity_score', 'N/A')}/10

The error appears to be recurring. Please prioritize investigation.

*Updated by NightlyErrorParsy at {datetime.now().isoformat()}*
"""
    
    def update_work_item_tags(self, work_item_id: int, error: Dict):
        """Update tags to reflect recurrence"""
        try:
            # Get current work item
            work_item = self.get_work_item_details(work_item_id)
            if not work_item:
                return
            
            current_tags = work_item.get('fields', {}).get('System.Tags', '')
            tags = current_tags.split(',') if current_tags else []
            
            # Add recurrence tag
            if 'Recurring' not in tags:
                tags.append('Recurring')
            
            # Update occurrence count tag
            tags = [t for t in tags if not t.startswith('Occurrences_')]
            tags.append(f"Occurrences_{error.get('occurrence_count', 1)}")
            
            # Update work item
            cmd = [
                'az', 'boards', 'work-item', 'update',
                '--id', str(work_item_id),
                '--fields', f"System.Tags={','.join(tags)}",
                '--project', self.config['project'],
                '-o', 'json'
            ]
            
            subprocess.run(cmd, capture_output=True, text=True, check=True)
            
        except Exception as e:
            self.logger.warning(f"Failed to update tags for work item {work_item_id}: {str(e)}")
    
    def process_error_batch(self, errors: List[Dict], similarity_results: Dict = None) -> Dict[str, Any]:
        """Process a batch of errors for work item creation/updates"""
        results = {
            'created': [],
            'updated': [],
            'failed': [],
            'skipped': []
        }
        
        for error in errors:
            try:
                # Generate error signature for matching
                error_signature = self.generate_error_signature(error)
                
                # Check if we should create or update
                matching_item = self.find_matching_work_item(error, similarity_results)
                
                if matching_item:
                    # Update existing work item
                    if self.update_work_item(matching_item['id'], error):
                        results['updated'].append({
                            'work_item_id': matching_item['id'],
                            'error_id': error.get('log_id')
                        })
                    else:
                        results['failed'].append(error.get('log_id'))
                else:
                    # Create new work item
                    work_item = self.create_work_item(error, similarity_results)
                    if work_item:
                        results['created'].append({
                            'work_item_id': work_item['id'],
                            'error_id': error.get('log_id'),
                            'url': work_item.get('url')
                        })
                    else:
                        results['failed'].append(error.get('log_id'))
                        
            except Exception as e:
                self.logger.error(f"Failed to process error {error.get('log_id')}: {str(e)}")
                results['failed'].append(error.get('log_id'))
        
        return results
    
    def generate_error_signature(self, error: Dict) -> str:
        """Generate a signature for error matching"""
        message = error.get('log_message', '')[:200]
        source = error.get('log_source', '')
        
        signature = f"{source}:{message}"
        return hashlib.md5(signature.encode()).hexdigest()
    
    def find_matching_work_item(self, error: Dict, similarity_results: Dict = None) -> Optional[Dict]:
        """Find existing work item that matches the error"""
        if similarity_results and similarity_results.get('matching_work_items'):
            # Use AI-powered matching results
            matches = similarity_results['matching_work_items']
            for match in matches:
                if match.get('similarity_score', 0) >= self.config['similarity_threshold']:
                    return self.get_work_item_details(match['work_item_id'])
        
        # Fallback to signature-based matching
        existing_items = self.search_existing_work_items(self.generate_error_signature(error))
        
        for item in existing_items:
            # Simple title matching for now
            if self.is_similar_error(error, item):
                return item
        
        return None
    
    def is_similar_error(self, error: Dict, work_item: Dict) -> bool:
        """Check if error matches existing work item"""
        # Simple implementation - can be enhanced
        error_message = error.get('log_message', '').lower()
        work_item_title = work_item.get('fields', {}).get('System.Title', '').lower()
        
        # Check for common words
        error_words = set(error_message.split())
        title_words = set(work_item_title.split())
        
        common_words = error_words.intersection(title_words)
        
        # If more than 50% of words match, consider it similar
        if len(common_words) > len(title_words) * 0.5:
            return True
        
        return False
    
    def get_statistics(self) -> Dict[str, Any]:
        """Get statistics about work item operations"""
        cache_file = Path(self.config['cache_dir']) / 'devops_stats.json'
        
        if cache_file.exists():
            with open(cache_file, 'r') as f:
                return json.load(f)
        
        return {
            'total_created': 0,
            'total_updated': 0,
            'last_run': None
        }
    
    def save_statistics(self, stats: Dict):
        """Save statistics about work item operations"""
        cache_dir = Path(self.config['cache_dir'])
        cache_dir.mkdir(parents=True, exist_ok=True)
        
        cache_file = cache_dir / 'devops_stats.json'
        with open(cache_file, 'w') as f:
            json.dump(stats, f, indent=2)


def main():
    """Main entry point for DevOpsSync agent"""
    import argparse
    
    parser = argparse.ArgumentParser(description='DevOpsSync - Azure DevOps Work Item Management Agent')
    parser.add_argument('--config', '-c', help='Configuration file path')
    parser.add_argument('--errors-file', '-e', help='JSON file with errors to process')
    parser.add_argument('--test', action='store_true', help='Test connection only')
    
    args = parser.parse_args()
    
    # Initialize agent
    sync = DevOpsSyncAgent(args.config)
    
    if args.test:
        # Test connection
        items = sync.search_existing_work_items('test', limit=1)
        print(f"Connection test successful. Found {len(items)} work items.")
        
    elif args.errors_file:
        # Process errors from file
        with open(args.errors_file, 'r') as f:
            data = json.load(f)
            errors = data.get('errors', [])
        
        results = sync.process_error_batch(errors)
        print(json.dumps(results, indent=2))
    
    else:
        print("Please specify --errors-file or --test")


if __name__ == '__main__':
    main()