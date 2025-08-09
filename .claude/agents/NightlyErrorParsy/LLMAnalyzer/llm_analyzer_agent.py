#!/usr/bin/env python3
"""
LLMAnalyzer Agent - AI-Powered Error Analysis and Matching
Uses LLM to analyze errors, find patterns, and match with existing work items
"""

import os
import json
import openai
from typing import List, Dict, Optional, Any, Tuple
from pathlib import Path
import logging
from datetime import datetime
import hashlib
import re

class LLMAnalyzerAgent:
    """Agent for AI-powered error analysis using LLMs"""
    
    def __init__(self, config_path: str = None):
        """Initialize LLMAnalyzer with OpenAI/Azure OpenAI configuration"""
        self.config = self.load_config(config_path)
        self.setup_logging()
        self.setup_llm_client()
        self.analysis_cache = {}
        
    def load_config(self, config_path: str = None) -> Dict:
        """Load configuration from file or environment"""
        if config_path and Path(config_path).exists():
            with open(config_path, 'r') as f:
                return json.load(f)
        
        return {
            'provider': os.getenv('LLM_PROVIDER', 'openai'),  # 'openai' or 'azure'
            'api_key': os.getenv('OPENAI_API_KEY', ''),
            'azure_endpoint': os.getenv('AZURE_OPENAI_ENDPOINT', ''),
            'azure_deployment': os.getenv('AZURE_OPENAI_DEPLOYMENT', 'gpt-4'),
            'model': os.getenv('LLM_MODEL', 'gpt-4'),
            'temperature': 0.3,
            'max_tokens': 2000,
            'cache_dir': '../shared/cache',
            'log_dir': '../logs',
            'similarity_threshold': 0.85,
            'batch_size': 5
        }
    
    def setup_logging(self):
        """Configure logging for the agent"""
        log_dir = Path(self.config['log_dir'])
        log_dir.mkdir(parents=True, exist_ok=True)
        
        logging.basicConfig(
            level=logging.INFO,
            format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
            handlers=[
                logging.FileHandler(log_dir / f'llm_analyzer_{datetime.now():%Y%m%d}.log'),
                logging.StreamHandler()
            ]
        )
        self.logger = logging.getLogger(__name__)
    
    def setup_llm_client(self):
        """Initialize the LLM client (OpenAI or Azure OpenAI)"""
        if self.config['provider'] == 'azure':
            # Azure OpenAI setup
            openai.api_type = "azure"
            openai.api_base = self.config['azure_endpoint']
            openai.api_version = "2023-05-15"
            openai.api_key = self.config['api_key']
            self.deployment_name = self.config['azure_deployment']
            self.logger.info("Configured Azure OpenAI client")
        else:
            # Standard OpenAI setup
            openai.api_key = self.config['api_key']
            self.deployment_name = self.config['model']
            self.logger.info("Configured OpenAI client")
    
    def analyze_error(self, error: Dict) -> Dict[str, Any]:
        """Analyze a single error using LLM"""
        # Check cache first
        error_hash = self.get_error_hash(error)
        if error_hash in self.analysis_cache:
            self.logger.info(f"Using cached analysis for error {error.get('log_id')}")
            return self.analysis_cache[error_hash]
        
        prompt = self.create_analysis_prompt(error)
        
        try:
            response = self.call_llm(prompt, system_prompt=self.get_analysis_system_prompt())
            
            analysis = self.parse_analysis_response(response)
            analysis['error_hash'] = error_hash
            analysis['analyzed_at'] = datetime.now().isoformat()
            
            # Cache the result
            self.analysis_cache[error_hash] = analysis
            self.save_cache()
            
            self.logger.info(f"Analyzed error {error.get('log_id')}")
            return analysis
            
        except Exception as e:
            self.logger.error(f"Failed to analyze error: {str(e)}")
            return self.get_default_analysis(error)
    
    def create_analysis_prompt(self, error: Dict) -> str:
        """Create prompt for error analysis"""
        return f"""
Analyze the following application error and provide structured insights:

Error Message: {error.get('log_message', 'Unknown')}
Source: {error.get('log_source', 'Unknown')}
Component: {error.get('affected_component', 'Unknown')}
Category: {error.get('category', 'Unknown')}
Occurrences: {error.get('occurrence_count', 1)}
Severity Score: {error.get('severity_score', 'N/A')}/10
First Seen: {error.get('first_seen', 'Unknown')}
Last Seen: {error.get('last_seen', 'Unknown')}

Stack Trace (if available):
{error.get('stack_trace', 'No stack trace available')[:1000]}

Please provide:
1. ROOT_CAUSE: What is the likely root cause of this error?
2. IMPACT: What is the potential impact on the system/users?
3. RESOLUTION: What are the recommended steps to resolve this?
4. PREVENTION: How can this error be prevented in the future?
5. URGENCY: On a scale of 1-10, how urgent is this issue?
6. KEYWORDS: List 5 key technical terms related to this error
7. SIMILAR_PATTERNS: What common error patterns does this match?
8. AFFECTED_AREAS: What other system components might be affected?
"""
    
    def get_analysis_system_prompt(self) -> str:
        """Get system prompt for error analysis"""
        return """You are an expert software engineer specializing in error analysis and debugging.
Your task is to analyze application errors and provide actionable insights.
Be concise, technical, and focus on practical solutions.
Format your response with clear sections as requested."""
    
    def parse_analysis_response(self, response: str) -> Dict[str, Any]:
        """Parse the LLM response into structured data"""
        analysis = {
            'root_cause': '',
            'impact': '',
            'resolution': '',
            'prevention': '',
            'urgency': 5,
            'keywords': [],
            'similar_patterns': '',
            'affected_areas': []
        }
        
        # Parse sections from response
        sections = response.split('\n')
        current_section = None
        
        for line in sections:
            line = line.strip()
            
            if line.startswith('ROOT_CAUSE:'):
                current_section = 'root_cause'
                analysis['root_cause'] = line.replace('ROOT_CAUSE:', '').strip()
            elif line.startswith('IMPACT:'):
                current_section = 'impact'
                analysis['impact'] = line.replace('IMPACT:', '').strip()
            elif line.startswith('RESOLUTION:'):
                current_section = 'resolution'
                analysis['resolution'] = line.replace('RESOLUTION:', '').strip()
            elif line.startswith('PREVENTION:'):
                current_section = 'prevention'
                analysis['prevention'] = line.replace('PREVENTION:', '').strip()
            elif line.startswith('URGENCY:'):
                try:
                    urgency_text = line.replace('URGENCY:', '').strip()
                    analysis['urgency'] = int(re.search(r'\d+', urgency_text).group())
                except:
                    analysis['urgency'] = 5
            elif line.startswith('KEYWORDS:'):
                keywords_text = line.replace('KEYWORDS:', '').strip()
                analysis['keywords'] = [k.strip() for k in keywords_text.split(',')][:5]
            elif line.startswith('SIMILAR_PATTERNS:'):
                current_section = 'similar_patterns'
                analysis['similar_patterns'] = line.replace('SIMILAR_PATTERNS:', '').strip()
            elif line.startswith('AFFECTED_AREAS:'):
                areas_text = line.replace('AFFECTED_AREAS:', '').strip()
                analysis['affected_areas'] = [a.strip() for a in areas_text.split(',')]
            elif current_section and line:
                # Continue adding to current section
                analysis[current_section] += ' ' + line
        
        return analysis
    
    def match_with_work_items(self, error: Dict, work_items: List[Dict]) -> List[Dict]:
        """Match error with existing work items using LLM"""
        if not work_items:
            return []
        
        matches = []
        
        # Process in batches
        batch_size = self.config['batch_size']
        for i in range(0, len(work_items), batch_size):
            batch = work_items[i:i+batch_size]
            batch_matches = self.match_batch(error, batch)
            matches.extend(batch_matches)
        
        # Sort by similarity score
        matches.sort(key=lambda x: x['similarity_score'], reverse=True)
        
        return matches
    
    def match_batch(self, error: Dict, work_items: List[Dict]) -> List[Dict]:
        """Match error with a batch of work items"""
        prompt = self.create_matching_prompt(error, work_items)
        
        try:
            response = self.call_llm(prompt, system_prompt=self.get_matching_system_prompt())
            return self.parse_matching_response(response, work_items)
            
        except Exception as e:
            self.logger.error(f"Failed to match work items: {str(e)}")
            return []
    
    def create_matching_prompt(self, error: Dict, work_items: List[Dict]) -> str:
        """Create prompt for work item matching"""
        prompt = f"""
Compare the following error with existing work items and determine similarity:

ERROR TO MATCH:
Message: {error.get('log_message', 'Unknown')[:500]}
Source: {error.get('log_source', 'Unknown')}
Component: {error.get('affected_component', 'Unknown')}
Keywords: {', '.join(error.get('keywords', []))}

EXISTING WORK ITEMS:
"""
        
        for i, item in enumerate(work_items, 1):
            fields = item.get('fields', {})
            prompt += f"""
{i}. Work Item #{item.get('id', 'Unknown')}
   Title: {fields.get('System.Title', 'No title')[:200]}
   Description: {fields.get('System.Description', 'No description')[:300]}
   Tags: {fields.get('System.Tags', 'No tags')}
"""
        
        prompt += """
For each work item, provide a similarity score (0-100) and brief explanation.
Format: ITEM_[number]: [score] - [explanation]
Example: ITEM_1: 85 - Same error message and component
"""
        
        return prompt
    
    def get_matching_system_prompt(self) -> str:
        """Get system prompt for work item matching"""
        return """You are an expert at matching software errors with existing bug reports.
Consider error messages, stack traces, components, and patterns.
Be precise with similarity scores:
- 90-100: Almost certainly the same issue
- 70-89: Very likely the same issue
- 50-69: Possibly related
- Below 50: Different issues"""
    
    def parse_matching_response(self, response: str, work_items: List[Dict]) -> List[Dict]:
        """Parse matching response into structured data"""
        matches = []
        
        lines = response.split('\n')
        for line in lines:
            if line.startswith('ITEM_'):
                try:
                    # Parse format: ITEM_1: 85 - explanation
                    parts = line.split(':', 1)
                    if len(parts) == 2:
                        item_num = int(parts[0].replace('ITEM_', '').strip())
                        score_text = parts[1].strip()
                        
                        # Extract score
                        score_match = re.search(r'\d+', score_text)
                        if score_match:
                            score = int(score_match.group())
                            
                            # Extract explanation
                            explanation = re.sub(r'^\d+\s*-?\s*', '', score_text).strip()
                            
                            if 0 < item_num <= len(work_items):
                                matches.append({
                                    'work_item_id': work_items[item_num-1].get('id'),
                                    'similarity_score': score / 100.0,  # Convert to 0-1 scale
                                    'explanation': explanation
                                })
                except Exception as e:
                    self.logger.warning(f"Failed to parse matching line: {line}")
        
        return matches
    
    def find_error_patterns(self, errors: List[Dict]) -> Dict[str, Any]:
        """Identify patterns across multiple errors"""
        if not errors:
            return {}
        
        prompt = self.create_pattern_prompt(errors)
        
        try:
            response = self.call_llm(prompt, system_prompt=self.get_pattern_system_prompt())
            return self.parse_pattern_response(response)
            
        except Exception as e:
            self.logger.error(f"Failed to find patterns: {str(e)}")
            return {}
    
    def create_pattern_prompt(self, errors: List[Dict]) -> str:
        """Create prompt for pattern identification"""
        prompt = "Analyze these errors and identify common patterns:\n\n"
        
        for i, error in enumerate(errors[:10], 1):  # Limit to 10 errors
            prompt += f"""
Error {i}:
- Message: {error.get('log_message', '')[:200]}
- Component: {error.get('affected_component', 'Unknown')}
- Category: {error.get('category', 'Unknown')}
- Occurrences: {error.get('occurrence_count', 1)}
"""
        
        prompt += """
Identify:
1. COMMON_CAUSES: What are the common root causes?
2. PATTERNS: What patterns emerge across these errors?
3. CORRELATIONS: Are there timing or component correlations?
4. RECOMMENDATIONS: What system-wide improvements would help?
"""
        
        return prompt
    
    def get_pattern_system_prompt(self) -> str:
        """Get system prompt for pattern analysis"""
        return """You are a systems architect analyzing error patterns.
Look for systemic issues, architectural problems, and common failure modes.
Provide strategic recommendations for system improvement."""
    
    def parse_pattern_response(self, response: str) -> Dict[str, Any]:
        """Parse pattern analysis response"""
        patterns = {
            'common_causes': [],
            'patterns': [],
            'correlations': [],
            'recommendations': []
        }
        
        current_section = None
        lines = response.split('\n')
        
        for line in lines:
            line = line.strip()
            
            if 'COMMON_CAUSES:' in line:
                current_section = 'common_causes'
            elif 'PATTERNS:' in line:
                current_section = 'patterns'
            elif 'CORRELATIONS:' in line:
                current_section = 'correlations'
            elif 'RECOMMENDATIONS:' in line:
                current_section = 'recommendations'
            elif current_section and line and line[0] in '-*•':
                # Add list items
                patterns[current_section].append(line.lstrip('-*• '))
        
        return patterns
    
    def call_llm(self, prompt: str, system_prompt: str = None) -> str:
        """Make a call to the LLM API"""
        messages = []
        
        if system_prompt:
            messages.append({"role": "system", "content": system_prompt})
        
        messages.append({"role": "user", "content": prompt})
        
        try:
            if self.config['provider'] == 'azure':
                response = openai.ChatCompletion.create(
                    engine=self.deployment_name,
                    messages=messages,
                    temperature=self.config['temperature'],
                    max_tokens=self.config['max_tokens']
                )
            else:
                response = openai.ChatCompletion.create(
                    model=self.deployment_name,
                    messages=messages,
                    temperature=self.config['temperature'],
                    max_tokens=self.config['max_tokens']
                )
            
            return response.choices[0].message.content
            
        except Exception as e:
            self.logger.error(f"LLM API call failed: {str(e)}")
            raise
    
    def get_error_hash(self, error: Dict) -> str:
        """Generate hash for error caching"""
        key_parts = [
            error.get('log_message', '')[:200],
            error.get('log_source', ''),
            error.get('affected_component', '')
        ]
        
        hash_input = '|'.join(key_parts)
        return hashlib.md5(hash_input.encode()).hexdigest()
    
    def get_default_analysis(self, error: Dict) -> Dict[str, Any]:
        """Return default analysis when LLM fails"""
        return {
            'root_cause': 'Unable to determine - LLM analysis failed',
            'impact': 'Unknown impact',
            'resolution': 'Manual investigation required',
            'prevention': 'Review error logs and code',
            'urgency': error.get('severity_score', 5),
            'keywords': [],
            'similar_patterns': 'Unable to identify patterns',
            'affected_areas': [],
            'error_hash': self.get_error_hash(error),
            'analyzed_at': datetime.now().isoformat(),
            'analysis_failed': True
        }
    
    def save_cache(self):
        """Save analysis cache to disk"""
        cache_dir = Path(self.config['cache_dir'])
        cache_dir.mkdir(parents=True, exist_ok=True)
        
        cache_file = cache_dir / 'llm_analysis_cache.json'
        
        # Keep only recent entries (last 1000)
        if len(self.analysis_cache) > 1000:
            # Sort by analyzed_at and keep recent ones
            sorted_items = sorted(
                self.analysis_cache.items(),
                key=lambda x: x[1].get('analyzed_at', ''),
                reverse=True
            )
            self.analysis_cache = dict(sorted_items[:1000])
        
        with open(cache_file, 'w') as f:
            json.dump(self.analysis_cache, f, indent=2)
    
    def load_cache(self):
        """Load analysis cache from disk"""
        cache_file = Path(self.config['cache_dir']) / 'llm_analysis_cache.json'
        
        if cache_file.exists():
            try:
                with open(cache_file, 'r') as f:
                    self.analysis_cache = json.load(f)
                self.logger.info(f"Loaded {len(self.analysis_cache)} cached analyses")
            except Exception as e:
                self.logger.warning(f"Failed to load cache: {str(e)}")
                self.analysis_cache = {}
    
    def generate_summary(self, errors: List[Dict], analyses: List[Dict]) -> str:
        """Generate an executive summary of all errors and analyses"""
        prompt = f"""
Create an executive summary of {len(errors)} system errors:

Top Issues by Urgency:
"""
        
        # Sort by urgency
        urgent_errors = sorted(
            zip(errors, analyses),
            key=lambda x: x[1].get('urgency', 5),
            reverse=True
        )[:5]
        
        for error, analysis in urgent_errors:
            prompt += f"""
- {error.get('log_message', '')[:100]}
  Urgency: {analysis.get('urgency', 'N/A')}/10
  Root Cause: {analysis.get('root_cause', 'Unknown')[:100]}
"""
        
        prompt += """
Provide:
1. EXECUTIVE_SUMMARY: 2-3 sentence overview
2. KEY_RISKS: Top 3 risks to the system
3. IMMEDIATE_ACTIONS: Top 3 actions to take now
4. TREND_ANALYSIS: Are errors increasing/decreasing?
"""
        
        try:
            response = self.call_llm(prompt, system_prompt="You are a CTO providing executive insights on system health.")
            return response
        except:
            return "Summary generation failed. Please review individual error analyses."


def main():
    """Main entry point for LLMAnalyzer agent"""
    import argparse
    
    parser = argparse.ArgumentParser(description='LLMAnalyzer - AI-Powered Error Analysis Agent')
    parser.add_argument('--config', '-c', help='Configuration file path')
    parser.add_argument('--errors-file', '-e', help='JSON file with errors to analyze')
    parser.add_argument('--analyze-patterns', action='store_true', help='Analyze patterns across errors')
    parser.add_argument('--test', action='store_true', help='Test LLM connection')
    
    args = parser.parse_args()
    
    # Initialize agent
    analyzer = LLMAnalyzerAgent(args.config)
    analyzer.load_cache()
    
    if args.test:
        # Test LLM connection
        test_error = {
            'log_message': 'Test error: Connection timeout to database',
            'log_source': 'TestComponent',
            'affected_component': 'Database',
            'severity_score': 7
        }
        
        analysis = analyzer.analyze_error(test_error)
        print("LLM Connection Test Successful!")
        print(json.dumps(analysis, indent=2))
        
    elif args.errors_file:
        # Analyze errors from file
        with open(args.errors_file, 'r') as f:
            data = json.load(f)
            errors = data.get('errors', [])
        
        analyses = []
        for error in errors:
            analysis = analyzer.analyze_error(error)
            analyses.append(analysis)
        
        if args.analyze_patterns:
            patterns = analyzer.find_error_patterns(errors)
            print("Error Patterns:")
            print(json.dumps(patterns, indent=2))
        
        # Generate summary
        summary = analyzer.generate_summary(errors, analyses)
        print("\nExecutive Summary:")
        print(summary)
        
        # Save results
        output = {
            'analyses': analyses,
            'patterns': patterns if args.analyze_patterns else {},
            'summary': summary
        }
        
        with open('llm_analysis_results.json', 'w') as f:
            json.dump(output, f, indent=2)
        
        print(f"\nAnalyzed {len(errors)} errors. Results saved to llm_analysis_results.json")
    
    else:
        print("Please specify --errors-file or --test")


if __name__ == '__main__':
    main()