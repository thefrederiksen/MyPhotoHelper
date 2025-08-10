---
name: process-issue
description: Process a GitHub issue through implementer, reviewer, and QA tester agents
---

Process GitHub issue #{{ issue_number }} using all three agents:

1. First, use the github-issue-implementer agent to implement the issue and create a PR
2. Then, use the pr-code-reviewer agent to review the code
3. If changes are needed, use the implementer again to fix them
4. Finally, use the qa-ui-tester agent to validate with screenshots
5. Ensure all tests pass and screenshots are uploaded to the PR
6. Report the final status and PR link