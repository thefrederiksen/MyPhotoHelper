# NightlyErrorParsy - Automated Error Monitoring System

A comprehensive Claude sub-agent system for automated error monitoring, analysis, and Azure DevOps integration.

## 🎯 Overview

NightlyErrorParsy is a modular system that:
1. **Extracts** error logs from SQL Server databases
2. **Analyzes** errors using AI/LLM technology
3. **Matches** errors with existing Azure DevOps work items
4. **Creates/Updates** work items automatically
5. **Generates** comprehensive reports
6. **Sends** email notifications with insights

## 🏗️ Architecture

The system consists of 5 specialized agents:

```
NightlyErrorParsy (Orchestrator)
├── ErrorMonitor      - SQL error extraction & deduplication
├── DevOpsSync        - Azure DevOps work item management
├── LLMAnalyzer       - AI-powered error analysis
├── ReportGenerator   - Report generation & email notifications
└── Shared Resources  - Cache, data, logs
```

## 🚀 Quick Start

### Prerequisites

1. **Python 3.8+**
2. **Azure CLI** with DevOps extension
3. **SQL Server** access
4. **Azure DevOps** PAT token
5. **OpenAI API** key (or Azure OpenAI)
6. **SendGrid API** key (optional)

### Installation

```bash
# Clone or navigate to the agent directory
cd C:\ReposFred\MyPhotoHelper\.claude\agents\NightlyErrorParsy

# Create virtual environment
python -m venv venv
venv\Scripts\activate

# Install dependencies
pip install -r requirements.txt

# Install Azure DevOps extension
az extension add --name azure-devops

# Copy and configure environment variables
copy .env.example .env
# Edit .env with your credentials
```

### Configuration

1. **Edit `.env` file** with your credentials:
   - SQL Server connection details
   - Azure DevOps PAT and project info
   - OpenAI/Azure OpenAI API keys
   - Email configuration

2. **Customize `config/config.json`** for:
   - Processing modes and limits
   - Error deduplication thresholds
   - Work item creation rules
   - Report formats

### Running Manually

```bash
# Full pipeline (extract, analyze, create work items, report, email)
python nightly_error_parsy.py --mode full

# Dry run (no work items created, no emails sent)
python nightly_error_parsy.py --dry-run

# Extract and analyze only
python nightly_error_parsy.py --mode analyze

# Generate reports from existing data
python nightly_error_parsy.py --mode report

# Limit processing
python nightly_error_parsy.py --max-errors 50
```

### Running Individual Agents

```bash
# Test error extraction
python ErrorMonitor/error_monitor_agent.py --test

# Test DevOps connection
python DevOpsSync/devops_sync_agent.py --test

# Test LLM analysis
python LLMAnalyzer/llm_analyzer_agent.py --test

# Generate test report
python ReportGenerator/report_generator_agent.py --test
```

## 📅 Scheduling with Windows Task Scheduler

### Automatic Setup

```bash
# Run the setup script
setup_task_scheduler.bat
```

### Manual Setup

1. Open **Task Scheduler**
2. Click **Create Basic Task**
3. Name: "NightlyErrorParsy"
4. Trigger: Daily at 2:00 AM
5. Action: Start a program
6. Program: `C:\ReposFred\MyPhotoHelper\.claude\agents\NightlyErrorParsy\run_nightly.bat`
7. Start in: `C:\ReposFred\MyPhotoHelper\.claude\agents\NightlyErrorParsy`
8. Run whether user is logged on or not
9. Run with highest privileges

## 📊 Features

### Error Extraction (ErrorMonitor)
- Connects to SQL Server `tbl_global_log`
- Filters by date range and visibility flags
- Deduplicates similar errors
- Enriches with severity scores and categories
- Tracks processing history

### AI Analysis (LLMAnalyzer)
- Analyzes root causes and impact
- Provides resolution recommendations
- Identifies patterns across errors
- Matches with existing work items
- Generates executive summaries

### DevOps Integration (DevOpsSync)
- Searches existing work items
- Creates new bugs/tasks automatically
- Updates recurring issues
- Manages tags and priorities
- Links related items

### Reporting (ReportGenerator)
- HTML reports with charts and visualizations
- CSV exports for data analysis
- JSON outputs for integration
- Email notifications with summaries
- Customizable templates

## 📁 Directory Structure

```
NightlyErrorParsy/
├── ErrorMonitor/
│   └── error_monitor_agent.py
├── DevOpsSync/
│   └── devops_sync_agent.py
├── LLMAnalyzer/
│   └── llm_analyzer_agent.py
├── ReportGenerator/
│   ├── report_generator_agent.py
│   └── templates/
├── config/
│   └── config.json
├── shared/
│   ├── data/         # Extracted error data
│   └── cache/        # LLM analysis cache
├── reports/          # Generated reports
├── logs/             # Execution logs
├── nightly_error_parsy.py  # Main orchestrator
├── requirements.txt
├── .env.example
├── run_nightly.bat   # Task scheduler script
└── README.md
```

## 🔧 Advanced Configuration

### Processing Modes

- **full**: Complete pipeline (extract → analyze → create → report → email)
- **analyze**: Extract and analyze only (no work items)
- **report**: Generate reports from existing data

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `PARSY_MODE` | Processing mode | `full` |
| `PARSY_DRY_RUN` | Test mode without side effects | `false` |
| `ENABLE_LLM` | Enable AI analysis | `true` |
| `ENABLE_EMAIL` | Send email notifications | `true` |
| `CREATE_WORK_ITEMS` | Create Azure DevOps items | `true` |
| `MAX_ERRORS` | Maximum errors to process | `100` |

### Customizing Error Analysis

Edit `LLMAnalyzer/llm_analyzer_agent.py` to customize:
- Analysis prompts
- Severity scoring algorithms
- Pattern detection rules
- Matching thresholds

### Email Templates

Modify `ReportGenerator/report_generator_agent.py` to customize:
- HTML email layout
- Report sections
- Chart visualizations
- Summary statistics

## 🧪 Testing

### Unit Tests

```bash
# Run all tests
pytest

# Run specific agent tests
pytest ErrorMonitor/test_error_monitor.py
pytest DevOpsSync/test_devops_sync.py
```

### Integration Testing

```bash
# Test with sample data
python nightly_error_parsy.py --dry-run --max-errors 5

# Verify connections only
python nightly_error_parsy.py --mode analyze --dry-run
```

## 📈 Monitoring

### Log Files

- Main log: `logs/nightly_error_parsy_YYYYMMDD_HHMMSS.log`
- Agent logs: `logs/[agent_name]_YYYYMMDD.log`

### Performance Metrics

The system tracks:
- Total processing time
- Errors processed per minute
- API call counts and latency
- Cache hit rates
- Email delivery success

### Health Checks

```python
# Check system health
python health_check.py

# Verify all components
python verify_setup.py
```

## 🔒 Security Considerations

1. **Never commit `.env` files** to version control
2. **Use service accounts** with minimal permissions
3. **Rotate API keys** regularly
4. **Encrypt sensitive data** in transit and at rest
5. **Audit log** all work item creations
6. **Validate** all SQL inputs to prevent injection

## 🐛 Troubleshooting

### Common Issues

| Issue | Solution |
|-------|----------|
| SQL connection fails | Check firewall, credentials, and server name |
| Azure DevOps auth fails | Verify PAT token and permissions |
| LLM timeout | Reduce batch size or increase timeout |
| Email not sending | Check SendGrid/SMTP credentials |
| No errors found | Verify lookback period and filters |

### Debug Mode

```bash
# Enable verbose logging
set PYTHONVERBOSE=1
python nightly_error_parsy.py --mode full
```

### Reset State

```bash
# Clear cache and state files
python reset_state.py

# Or manually
del shared\data\last_run.json
del shared\cache\*.json
```

## 📝 Best Practices

1. **Start with dry run** to verify configuration
2. **Monitor first few runs** closely
3. **Adjust thresholds** based on your error patterns
4. **Review AI analyses** for accuracy
5. **Customize categories** for your application
6. **Set up alerts** for critical errors
7. **Archive old reports** periodically
8. **Update dependencies** monthly

## 🤝 Integration with Claude

This system is designed as a Claude sub-agent. When using with Claude:

```
"Use NightlyErrorParsy to analyze last night's errors and create work items"

"Run error analysis for the past week with pattern detection"

"Generate an executive report of system errors without creating work items"
```

## 📊 Sample Output

### Console Output
```
========================================
NightlyErrorParsy - Starting Execution
========================================

✓ ErrorMonitor agent initialized
✓ DevOpsSync agent initialized
✓ LLMAnalyzer agent initialized
✓ ReportGenerator agent initialized

========================================
STEP 1: Extracting Errors from Database
========================================
✓ Extracted 523 total errors
✓ Deduplicated to 47 unique errors

========================================
STEP 2: Analyzing Errors with AI
========================================
Analyzing error 1/47...
✓ Identified 5 patterns
✓ AI analysis complete

========================================
STEP 3: Creating/Updating Work Items
========================================
✓ Created 12 new work items
✓ Updated 8 existing work items

========================================
STEP 4: Generating Reports
========================================
✓ Generated 3 report files

========================================
STEP 5: Sending Email Notification
========================================
✓ Email report sent successfully

========================================
NightlyErrorParsy Completed Successfully
Total Processing Time: 124.56 seconds
========================================
```

## 📚 Additional Resources

- [Azure DevOps REST API](https://docs.microsoft.com/en-us/rest/api/azure/devops/)
- [OpenAI API Documentation](https://platform.openai.com/docs/)
- [SendGrid Email API](https://docs.sendgrid.com/)
- [Python pyodbc](https://github.com/mkleehammer/pyodbc/wiki)

## 🔄 Version History

- **v1.0.0** (2025-01-08): Initial release with full pipeline
- Core agents: ErrorMonitor, DevOpsSync, LLMAnalyzer, ReportGenerator
- Full Azure DevOps integration
- AI-powered analysis and matching

## 📄 License

This system is part of the Mindzie project and follows project licensing terms.

## 👥 Support

For issues or questions:
1. Check the troubleshooting section
2. Review log files for detailed errors
3. Contact the development team

---

**Built with ❤️ by Claude AI Assistant for Mindzie**