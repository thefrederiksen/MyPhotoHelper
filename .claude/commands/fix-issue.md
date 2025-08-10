---
name: fix-issue
description: Automatically implement a GitHub issue through the complete development workflow
---

Launch the issue-workflow-manager agent to implement GitHub issue #{{ issue_number }}. Process it completely through implementation, review, and QA testing until the PR is production-ready. Work autonomously without asking for confirmation at each step.

The workflow will:
1. Fetch and analyze the issue
2. Implement the solution following project conventions
3. Create a pull request
4. Run comprehensive code review
5. Fix any issues found in review
6. Perform QA testing with before/after screenshots
7. Iterate until all quality gates pass
8. Clean up test artifacts
9. Report when PR is ready to merge

Only escalate if there are critical blockers that cannot be resolved automatically.