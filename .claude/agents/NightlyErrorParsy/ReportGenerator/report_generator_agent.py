#!/usr/bin/env python3
"""
ReportGenerator Agent - Email Notification and Reporting
Generates comprehensive reports and sends email notifications
"""

import os
import json
import smtplib
from email.mime.text import MIMEText
from email.mime.multipart import MIMEMultipart
from email.mime.base import MIMEBase
from email import encoders
from datetime import datetime, timedelta
from typing import List, Dict, Optional, Any
from pathlib import Path
import logging
import csv

# Optional: SendGrid support
try:
    from sendgrid import SendGridAPIClient
    from sendgrid.helpers.mail import Mail, Attachment, FileContent, FileName, FileType, Disposition
    import base64
    SENDGRID_AVAILABLE = True
except ImportError:
    SENDGRID_AVAILABLE = False

class ReportGeneratorAgent:
    """Agent for generating reports and sending notifications"""
    
    def __init__(self, config_path: str = None):
        """Initialize ReportGenerator with email configuration"""
        self.config = self.load_config(config_path)
        self.setup_logging()
        self.templates_dir = Path(__file__).parent / 'templates'
        self.templates_dir.mkdir(exist_ok=True)
        
    def load_config(self, config_path: str = None) -> Dict:
        """Load configuration from file or environment"""
        if config_path and Path(config_path).exists():
            with open(config_path, 'r') as f:
                return json.load(f)
        
        return {
            'email_provider': os.getenv('EMAIL_PROVIDER', 'sendgrid'),  # 'sendgrid' or 'smtp'
            'sendgrid_api_key': os.getenv('SENDGRID_API_KEY', ''),
            'smtp_server': os.getenv('SMTP_SERVER', 'smtp.gmail.com'),
            'smtp_port': int(os.getenv('SMTP_PORT', '587')),
            'smtp_username': os.getenv('SMTP_USERNAME', ''),
            'smtp_password': os.getenv('SMTP_PASSWORD', ''),
            'from_email': os.getenv('FROM_EMAIL', 'noreply@mindzie.com'),
            'from_name': os.getenv('FROM_NAME', 'NightlyErrorParsy'),
            'to_emails': os.getenv('TO_EMAILS', '').split(','),
            'cc_emails': os.getenv('CC_EMAILS', '').split(',') if os.getenv('CC_EMAILS') else [],
            'report_dir': '../reports',
            'log_dir': '../logs',
            'enable_csv': True,
            'enable_json': True,
            'enable_html': True
        }
    
    def setup_logging(self):
        """Configure logging for the agent"""
        log_dir = Path(self.config['log_dir'])
        log_dir.mkdir(parents=True, exist_ok=True)
        
        logging.basicConfig(
            level=logging.INFO,
            format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
            handlers=[
                logging.FileHandler(log_dir / f'report_generator_{datetime.now():%Y%m%d}.log'),
                logging.StreamHandler()
            ]
        )
        self.logger = logging.getLogger(__name__)
    
    def generate_report(self, processing_results: Dict[str, Any]) -> Dict[str, str]:
        """Generate comprehensive report in multiple formats"""
        reports = {}
        report_dir = Path(self.config['report_dir'])
        report_dir.mkdir(parents=True, exist_ok=True)
        
        timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
        
        # Generate HTML report
        if self.config['enable_html']:
            html_content = self.generate_html_report(processing_results)
            html_file = report_dir / f'error_report_{timestamp}.html'
            with open(html_file, 'w', encoding='utf-8') as f:
                f.write(html_content)
            reports['html'] = str(html_file)
            self.logger.info(f"Generated HTML report: {html_file}")
        
        # Generate CSV report
        if self.config['enable_csv']:
            csv_file = report_dir / f'error_report_{timestamp}.csv'
            self.generate_csv_report(processing_results, csv_file)
            reports['csv'] = str(csv_file)
            self.logger.info(f"Generated CSV report: {csv_file}")
        
        # Generate JSON report
        if self.config['enable_json']:
            json_file = report_dir / f'error_report_{timestamp}.json'
            with open(json_file, 'w') as f:
                json.dump(processing_results, f, indent=2, default=str)
            reports['json'] = str(json_file)
            self.logger.info(f"Generated JSON report: {json_file}")
        
        return reports
    
    def generate_html_report(self, results: Dict[str, Any]) -> str:
        """Generate HTML report with styling"""
        errors = results.get('errors', [])
        work_items = results.get('work_items', {})
        summary = results.get('summary', {})
        patterns = results.get('patterns', {})
        
        html = f"""
<!DOCTYPE html>
<html>
<head>
    <title>NightlyErrorParsy Report - {datetime.now().strftime('%Y-%m-%d')}</title>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 1200px;
            margin: 0 auto;
            padding: 20px;
            background: #f4f4f4;
        }}
        .header {{
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 30px;
            border-radius: 10px;
            margin-bottom: 30px;
        }}
        .header h1 {{
            margin: 0;
            font-size: 2.5em;
        }}
        .header p {{
            margin: 10px 0 0 0;
            opacity: 0.9;
        }}
        .summary-grid {{
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 20px;
            margin-bottom: 30px;
        }}
        .summary-card {{
            background: white;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }}
        .summary-card h3 {{
            margin: 0 0 10px 0;
            color: #667eea;
            font-size: 0.9em;
            text-transform: uppercase;
            letter-spacing: 1px;
        }}
        .summary-card .value {{
            font-size: 2em;
            font-weight: bold;
            color: #333;
        }}
        .section {{
            background: white;
            padding: 25px;
            border-radius: 8px;
            margin-bottom: 20px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }}
        .section h2 {{
            color: #667eea;
            border-bottom: 2px solid #f0f0f0;
            padding-bottom: 10px;
            margin-bottom: 20px;
        }}
        table {{
            width: 100%;
            border-collapse: collapse;
        }}
        th {{
            background: #667eea;
            color: white;
            padding: 12px;
            text-align: left;
            font-weight: 500;
        }}
        td {{
            padding: 10px;
            border-bottom: 1px solid #f0f0f0;
        }}
        tr:hover {{
            background: #f9f9f9;
        }}
        .severity-critical {{
            background: #ff4444;
            color: white;
            padding: 2px 8px;
            border-radius: 3px;
            font-size: 0.85em;
        }}
        .severity-high {{
            background: #ff8800;
            color: white;
            padding: 2px 8px;
            border-radius: 3px;
            font-size: 0.85em;
        }}
        .severity-medium {{
            background: #ffbb33;
            color: white;
            padding: 2px 8px;
            border-radius: 3px;
            font-size: 0.85em;
        }}
        .severity-low {{
            background: #00C851;
            color: white;
            padding: 2px 8px;
            border-radius: 3px;
            font-size: 0.85em;
        }}
        .work-item-link {{
            color: #667eea;
            text-decoration: none;
            font-weight: 500;
        }}
        .work-item-link:hover {{
            text-decoration: underline;
        }}
        .pattern-item {{
            background: #f8f9fa;
            padding: 10px;
            margin: 5px 0;
            border-left: 3px solid #667eea;
        }}
        .footer {{
            text-align: center;
            padding: 20px;
            color: #666;
            font-size: 0.9em;
        }}
        .chart-container {{
            margin: 20px 0;
        }}
        .bar {{
            height: 30px;
            background: linear-gradient(90deg, #667eea 0%, #764ba2 100%);
            margin: 5px 0;
            border-radius: 3px;
            position: relative;
        }}
        .bar-label {{
            position: absolute;
            left: 10px;
            top: 50%;
            transform: translateY(-50%);
            color: white;
            font-size: 0.9em;
        }}
    </style>
</head>
<body>
    <div class="header">
        <h1>🔍 NightlyErrorParsy Report</h1>
        <p>Automated Error Monitoring & Analysis | {datetime.now().strftime('%B %d, %Y at %I:%M %p')}</p>
    </div>
    
    <div class="summary-grid">
        <div class="summary-card">
            <h3>Total Errors</h3>
            <div class="value">{summary.get('total_errors', 0)}</div>
        </div>
        <div class="summary-card">
            <h3>Unique Errors</h3>
            <div class="value">{summary.get('unique_errors', 0)}</div>
        </div>
        <div class="summary-card">
            <h3>Work Items Created</h3>
            <div class="value">{len(work_items.get('created', []))}</div>
        </div>
        <div class="summary-card">
            <h3>Work Items Updated</h3>
            <div class="value">{len(work_items.get('updated', []))}</div>
        </div>
        <div class="summary-card">
            <h3>Critical Issues</h3>
            <div class="value">{summary.get('critical_count', 0)}</div>
        </div>
        <div class="summary-card">
            <h3>Processing Time</h3>
            <div class="value">{summary.get('processing_time', 0):.1f}s</div>
        </div>
    </div>
"""
        
        # Add critical errors section
        critical_errors = [e for e in errors if e.get('severity_score', 0) >= 8]
        if critical_errors:
            html += """
    <div class="section">
        <h2>🚨 Critical Errors Requiring Immediate Attention</h2>
        <table>
            <thead>
                <tr>
                    <th>Error Message</th>
                    <th>Component</th>
                    <th>Occurrences</th>
                    <th>Severity</th>
                    <th>Work Item</th>
                </tr>
            </thead>
            <tbody>
"""
            for error in critical_errors[:10]:
                severity_class = self.get_severity_class(error.get('severity_score', 5))
                work_item_link = self.get_work_item_link(error, work_items)
                
                html += f"""
                <tr>
                    <td>{self.truncate(error.get('log_message', 'Unknown'), 100)}</td>
                    <td>{error.get('affected_component', 'Unknown')}</td>
                    <td>{error.get('occurrence_count', 1)}</td>
                    <td><span class="{severity_class}">{error.get('severity_score', 'N/A')}/10</span></td>
                    <td>{work_item_link}</td>
                </tr>
"""
            html += """
            </tbody>
        </table>
    </div>
"""
        
        # Add error categories breakdown
        if summary.get('categories'):
            html += """
    <div class="section">
        <h2>📊 Error Distribution by Category</h2>
        <div class="chart-container">
"""
            max_count = max(summary['categories'].values()) if summary['categories'] else 1
            for category, count in sorted(summary['categories'].items(), key=lambda x: x[1], reverse=True):
                width = (count / max_count) * 100
                html += f"""
            <div style="margin: 10px 0;">
                <div style="display: flex; align-items: center;">
                    <div style="width: 150px; font-weight: 500;">{category}</div>
                    <div style="flex: 1; position: relative;">
                        <div class="bar" style="width: {width}%;">
                            <span class="bar-label">{count} errors</span>
                        </div>
                    </div>
                </div>
            </div>
"""
            html += """
        </div>
    </div>
"""
        
        # Add patterns section if available
        if patterns:
            html += """
    <div class="section">
        <h2>🔮 Identified Patterns & Insights</h2>
"""
            if patterns.get('common_causes'):
                html += "<h3>Common Root Causes</h3>"
                for cause in patterns['common_causes'][:5]:
                    html += f'<div class="pattern-item">{cause}</div>'
            
            if patterns.get('recommendations'):
                html += "<h3>System Improvements Recommended</h3>"
                for rec in patterns['recommendations'][:5]:
                    html += f'<div class="pattern-item">• {rec}</div>'
            
            html += "</div>"
        
        # Add all errors table
        html += """
    <div class="section">
        <h2>📋 All Unique Errors</h2>
        <table>
            <thead>
                <tr>
                    <th>Time</th>
                    <th>Error Message</th>
                    <th>Component</th>
                    <th>Category</th>
                    <th>Count</th>
                    <th>Severity</th>
                    <th>Action</th>
                </tr>
            </thead>
            <tbody>
"""
        
        for error in errors[:50]:  # Limit to 50 errors in email
            severity_class = self.get_severity_class(error.get('severity_score', 5))
            work_item_link = self.get_work_item_link(error, work_items)
            
            html += f"""
                <tr>
                    <td>{error.get('last_seen', 'Unknown')[:19]}</td>
                    <td>{self.truncate(error.get('log_message', 'Unknown'), 80)}</td>
                    <td>{error.get('affected_component', 'Unknown')}</td>
                    <td>{error.get('category', 'Unknown')}</td>
                    <td>{error.get('occurrence_count', 1)}</td>
                    <td><span class="{severity_class}">{error.get('severity_score', 'N/A')}/10</span></td>
                    <td>{work_item_link}</td>
                </tr>
"""
        
        html += """
            </tbody>
        </table>
    </div>
    
    <div class="footer">
        <p>Generated by NightlyErrorParsy | Mindzie Error Monitoring System</p>
        <p>Full reports attached to this email</p>
    </div>
</body>
</html>
"""
        
        return html
    
    def generate_csv_report(self, results: Dict[str, Any], output_file: Path):
        """Generate CSV report of errors"""
        errors = results.get('errors', [])
        work_items = results.get('work_items', {})
        
        with open(output_file, 'w', newline='', encoding='utf-8') as csvfile:
            fieldnames = [
                'Timestamp', 'Error Message', 'Component', 'Category', 
                'Occurrences', 'Severity', 'Work Item ID', 'Work Item URL',
                'Source', 'Application', 'Version', 'First Seen', 'Last Seen'
            ]
            
            writer = csv.DictWriter(csvfile, fieldnames=fieldnames)
            writer.writeheader()
            
            for error in errors:
                work_item_info = self.get_work_item_info(error, work_items)
                
                row = {
                    'Timestamp': error.get('log_timestamp', ''),
                    'Error Message': error.get('log_message', ''),
                    'Component': error.get('affected_component', ''),
                    'Category': error.get('category', ''),
                    'Occurrences': error.get('occurrence_count', 1),
                    'Severity': error.get('severity_score', ''),
                    'Work Item ID': work_item_info.get('id', ''),
                    'Work Item URL': work_item_info.get('url', ''),
                    'Source': error.get('log_source', ''),
                    'Application': error.get('application_name', ''),
                    'Version': error.get('application_version', ''),
                    'First Seen': error.get('first_seen', ''),
                    'Last Seen': error.get('last_seen', '')
                }
                
                writer.writerow(row)
    
    def send_email_report(self, reports: Dict[str, str], results: Dict[str, Any]) -> bool:
        """Send email report with attachments"""
        if not self.config['to_emails']:
            self.logger.warning("No recipient emails configured")
            return False
        
        subject = self.generate_email_subject(results)
        
        if self.config['email_provider'] == 'sendgrid' and SENDGRID_AVAILABLE:
            return self.send_via_sendgrid(subject, reports, results)
        else:
            return self.send_via_smtp(subject, reports, results)
    
    def generate_email_subject(self, results: Dict[str, Any]) -> str:
        """Generate email subject line"""
        summary = results.get('summary', {})
        critical_count = summary.get('critical_count', 0)
        unique_errors = summary.get('unique_errors', 0)
        
        date_str = datetime.now().strftime('%Y-%m-%d')
        
        if critical_count > 0:
            return f"🚨 CRITICAL: {critical_count} Critical Errors Detected - NightlyErrorParsy Report {date_str}"
        elif unique_errors > 10:
            return f"⚠️ ALERT: {unique_errors} Unique Errors Found - NightlyErrorParsy Report {date_str}"
        elif unique_errors > 0:
            return f"ℹ️ INFO: {unique_errors} Errors Detected - NightlyErrorParsy Report {date_str}"
        else:
            return f"✅ All Clear - NightlyErrorParsy Report {date_str}"
    
    def send_via_sendgrid(self, subject: str, reports: Dict[str, str], results: Dict[str, Any]) -> bool:
        """Send email via SendGrid API"""
        try:
            sg = SendGridAPIClient(self.config['sendgrid_api_key'])
            
            # Create message
            message = Mail(
                from_email=(self.config['from_email'], self.config['from_name']),
                to_emails=self.config['to_emails'],
                subject=subject,
                html_content=self.generate_html_report(results)
            )
            
            # Add CC if configured
            if self.config['cc_emails']:
                message.cc = self.config['cc_emails']
            
            # Add attachments
            for report_type, file_path in reports.items():
                if os.path.exists(file_path):
                    with open(file_path, 'rb') as f:
                        file_data = f.read()
                        encoded = base64.b64encode(file_data).decode()
                    
                    attachment = Attachment(
                        FileContent(encoded),
                        FileName(os.path.basename(file_path)),
                        FileType(self.get_mime_type(file_path)),
                        Disposition('attachment')
                    )
                    message.add_attachment(attachment)
            
            # Send
            response = sg.send(message)
            
            self.logger.info(f"Email sent successfully via SendGrid. Status: {response.status_code}")
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to send email via SendGrid: {str(e)}")
            return False
    
    def send_via_smtp(self, subject: str, reports: Dict[str, str], results: Dict[str, Any]) -> bool:
        """Send email via SMTP"""
        try:
            # Create message
            msg = MIMEMultipart('alternative')
            msg['Subject'] = subject
            msg['From'] = f"{self.config['from_name']} <{self.config['from_email']}>"
            msg['To'] = ', '.join(self.config['to_emails'])
            
            if self.config['cc_emails']:
                msg['Cc'] = ', '.join(self.config['cc_emails'])
            
            # Add HTML content
            html_part = MIMEText(self.generate_html_report(results), 'html')
            msg.attach(html_part)
            
            # Add attachments
            for report_type, file_path in reports.items():
                if os.path.exists(file_path):
                    with open(file_path, 'rb') as f:
                        part = MIMEBase('application', 'octet-stream')
                        part.set_payload(f.read())
                        encoders.encode_base64(part)
                        part.add_header(
                            'Content-Disposition',
                            f'attachment; filename="{os.path.basename(file_path)}"'
                        )
                        msg.attach(part)
            
            # Send email
            with smtplib.SMTP(self.config['smtp_server'], self.config['smtp_port']) as server:
                server.starttls()
                server.login(self.config['smtp_username'], self.config['smtp_password'])
                
                recipients = self.config['to_emails'] + self.config.get('cc_emails', [])
                server.send_message(msg, to_addrs=recipients)
            
            self.logger.info("Email sent successfully via SMTP")
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to send email via SMTP: {str(e)}")
            return False
    
    def get_severity_class(self, severity: int) -> str:
        """Get CSS class for severity level"""
        if severity >= 9:
            return 'severity-critical'
        elif severity >= 7:
            return 'severity-high'
        elif severity >= 4:
            return 'severity-medium'
        else:
            return 'severity-low'
    
    def get_work_item_link(self, error: Dict, work_items: Dict) -> str:
        """Get work item link HTML for an error"""
        # Check if this error has an associated work item
        error_id = error.get('log_id')
        
        for item in work_items.get('created', []):
            if item.get('error_id') == error_id:
                return f'<a href="{item.get("url", "#")}" class="work-item-link">#{item.get("work_item_id", "N/A")} (New)</a>'
        
        for item in work_items.get('updated', []):
            if item.get('error_id') == error_id:
                return f'<a href="#" class="work-item-link">#{item.get("work_item_id", "N/A")} (Updated)</a>'
        
        return '<span style="color: #999;">Not created</span>'
    
    def get_work_item_info(self, error: Dict, work_items: Dict) -> Dict:
        """Get work item information for an error"""
        error_id = error.get('log_id')
        
        for item in work_items.get('created', []):
            if item.get('error_id') == error_id:
                return item
        
        for item in work_items.get('updated', []):
            if item.get('error_id') == error_id:
                return item
        
        return {}
    
    def truncate(self, text: str, max_length: int) -> str:
        """Truncate text to specified length"""
        if len(text) <= max_length:
            return text
        return text[:max_length-3] + '...'
    
    def get_mime_type(self, file_path: str) -> str:
        """Get MIME type for file"""
        if file_path.endswith('.html'):
            return 'text/html'
        elif file_path.endswith('.csv'):
            return 'text/csv'
        elif file_path.endswith('.json'):
            return 'application/json'
        else:
            return 'application/octet-stream'
    
    def generate_summary_stats(self, results: Dict[str, Any]) -> Dict[str, Any]:
        """Generate summary statistics"""
        errors = results.get('errors', [])
        work_items = results.get('work_items', {})
        
        critical_count = sum(1 for e in errors if e.get('severity_score', 0) >= 8)
        high_count = sum(1 for e in errors if 6 <= e.get('severity_score', 0) < 8)
        
        categories = {}
        components = {}
        
        for error in errors:
            cat = error.get('category', 'Unknown')
            categories[cat] = categories.get(cat, 0) + 1
            
            comp = error.get('affected_component', 'Unknown')
            components[comp] = components.get(comp, 0) + 1
        
        return {
            'total_errors': sum(e.get('occurrence_count', 1) for e in errors),
            'unique_errors': len(errors),
            'critical_count': critical_count,
            'high_count': high_count,
            'categories': categories,
            'components': components,
            'work_items_created': len(work_items.get('created', [])),
            'work_items_updated': len(work_items.get('updated', [])),
            'processing_time': results.get('processing_time', 0)
        }


def main():
    """Main entry point for ReportGenerator agent"""
    import argparse
    
    parser = argparse.ArgumentParser(description='ReportGenerator - Email Notification Agent')
    parser.add_argument('--config', '-c', help='Configuration file path')
    parser.add_argument('--results-file', '-r', help='JSON file with processing results')
    parser.add_argument('--send-email', action='store_true', help='Send email report')
    parser.add_argument('--test', action='store_true', help='Generate test report')
    
    args = parser.parse_args()
    
    # Initialize agent
    generator = ReportGeneratorAgent(args.config)
    
    if args.test:
        # Generate test report
        test_results = {
            'errors': [
                {
                    'log_id': 1,
                    'log_message': 'Test error message',
                    'affected_component': 'TestComponent',
                    'category': 'Database',
                    'severity_score': 8,
                    'occurrence_count': 5,
                    'last_seen': datetime.now().isoformat()
                }
            ],
            'work_items': {
                'created': [{'work_item_id': 123, 'error_id': 1}],
                'updated': []
            },
            'summary': generator.generate_summary_stats({'errors': []})
        }
        
        reports = generator.generate_report(test_results)
        print("Test reports generated:")
        for report_type, path in reports.items():
            print(f"  {report_type}: {path}")
        
        if args.send_email:
            if generator.send_email_report(reports, test_results):
                print("Test email sent successfully!")
            else:
                print("Failed to send test email")
    
    elif args.results_file:
        # Process actual results
        with open(args.results_file, 'r') as f:
            results = json.load(f)
        
        # Add summary if not present
        if 'summary' not in results:
            results['summary'] = generator.generate_summary_stats(results)
        
        reports = generator.generate_report(results)
        print("Reports generated:")
        for report_type, path in reports.items():
            print(f"  {report_type}: {path}")
        
        if args.send_email:
            if generator.send_email_report(reports, results):
                print("Email report sent successfully!")
            else:
                print("Failed to send email report")
    
    else:
        print("Please specify --results-file or --test")


if __name__ == '__main__':
    main()