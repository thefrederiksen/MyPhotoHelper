# Pull Request Review Guide - GitHub Web Interface

This guide explains how to review and merge pull requests using only the GitHub web interface, avoiding the need to switch branches locally.

## Quick Access Links

### Current Repository Pull Requests
**Direct Link:** https://github.com/thefrederiksen/MyPhotoHelper/pulls

### Active Pull Request (Update as needed)
**Current PR:** https://github.com/thefrederiksen/MyPhotoHelper/pull/10

---

## Step-by-Step Review Process

### 1. Navigate to the Pull Request
- Click the direct link above, or
- Go to your repository → "Pull requests" tab
- Click on the specific PR you want to review

### 2. Review the Pull Request Overview
**On the "Conversation" tab:**
- Read the PR title and description
- Check which issue it resolves (should say "Closes #X")
- Look at the summary of changes
- Note any test plans or checklists

### 3. Review the Code Changes
**Click the "Files changed" tab:**
- **Green lines** = Added code
- **Red lines** = Deleted code
- **Gray lines** = Context (unchanged)

**What to look for:**
- Does the code solve the stated problem?
- Is it consistent with existing code style?
- Are there any obvious bugs or issues?
- Does it follow the project patterns?

### 4. Add Comments (Optional)
**To comment on specific lines:**
1. Hover over a line number
2. Click the blue "+" icon that appears
3. Type your comment
4. Click "Add single comment" or "Start a review"

**For general comments:**
- Scroll to bottom of "Files changed" tab
- Use the text box to add overall feedback

### 5. Submit Your Review (Or Skip If You Created the PR)
**⚠️ IMPORTANT:** You cannot approve your own pull requests, even as repository owner!

**If you created the PR:**
- You'll only see "Comment" option (no "Approve" button)
- Skip to Step 6 and merge directly

**If someone else created the PR:**
- Click the "Review changes" button (green, top right)
- Choose: **✅ Approve**, **💬 Comment**, or **❌ Request changes**
- Add optional summary comment, then click **"Submit review"**

### 6. Merge the Pull Request
**You can always merge as repository owner, with or without approval:**

1. Go back to "Conversation" tab (if reviewing)
2. Scroll to bottom - you'll see a green "Merge pull request" button
3. **Choose merge type:**
   - **"Create a merge commit"** (recommended) - keeps PR history
   - **"Squash and merge"** - combines all commits into one
   - **"Rebase and merge"** - replays commits without merge commit

4. Click **"Merge pull request"**
5. Click **"Confirm merge"**
6. **⚠️ CRITICAL:** The "Delete branch" button appears **AFTER** merge, not before!

### 7. Delete the Branch (Post-Merge)
**Immediately after clicking "Confirm merge":**
1. Look for purple success message box
2. You'll see: `Pull request successfully merged and closed`
3. **Click "Delete branch"** button that appears below this message
4. ✅ This removes the feature branch from GitHub

### 8. Clean Up Locally
**After merging and deleting branch on GitHub:**
```bash
git checkout main
git pull origin main
git branch -d feature/branch-name  # Delete local copy if you have one
```

---

## Review Checklist

### ✅ Before Approving
- [ ] Read the PR description and understand what it does
- [ ] Review all changed files in "Files changed" tab
- [ ] Check that it addresses the referenced issue
- [ ] Look for obvious bugs or style inconsistencies
- [ ] Ensure it follows existing patterns in the codebase

### ✅ When Merging
- [ ] Choose appropriate merge type (usually "Create a merge commit")
- [ ] Click "Merge pull request" then "Confirm merge"
- [ ] **Look for "Delete branch" button AFTER merge completes**
- [ ] Always click "Delete branch" to clean up

### ✅ After Merging
- [ ] Pull latest changes to your local main branch
- [ ] Verify the issue was automatically closed
- [ ] Test the feature if needed

---

## Common Review Scenarios

### ✅ Your Own PR (Most Common)
1. Review code changes → Skip approval (can't approve own PR) → Merge directly → Delete branch
2. Usually takes 2-3 minutes

### ✅ Someone Else's PR
1. Review → Approve → Merge → Delete branch
2. Usually takes 2-3 minutes

### ❓ Need More Information
1. Add comments asking questions
2. Submit as "Comment" (not approval)
3. Wait for developer response
4. Re-review when updated

### ❌ Issues Found
1. Add specific comments on problematic lines
2. Submit as "Request changes"
3. Developer will fix and update the PR
4. You'll get notified to re-review

---

## Safety Tips

### ✅ Do This
- Always use GitHub web interface for reviews
- Remember: You can't approve your own PRs (this is normal!)
- Look for "Delete branch" button AFTER merging, not before
- Always delete the branch after merging
- Pull latest main branch after merging
- Read the code changes, don't just trust the description

### ❌ Avoid This
- Don't switch to feature branches in Visual Studio/VS Code
- Don't expect to see "Approve" button on PRs you created
- Don't look for "Delete branch" checkbox before merging
- Don't merge without reviewing the actual code changes
- Don't forget to delete branches after merging

---

## Troubleshooting

### "Merge" Button is Grayed Out
- Usually means there are merge conflicts
- Ask the developer to resolve conflicts
- They'll need to update their branch

### Can't See "Delete Branch" Option
- Only appears AFTER successful merge (not before!)
- Look for purple success box right after clicking "Confirm merge"
- If missed, go to repository → "branches" tab → find the branch → click trash icon

### Only See "Comment" Option, No "Approve"
- This is normal! You cannot approve your own PRs
- Just skip the approval step and merge directly
- This happens even if you're the repository owner

### PR Shows as "Closed" Instead of "Merged"
- This means it was closed without merging
- If it should have been merged, you can reopen it
- Click "Reopen" button if available

---

## Quick Reference Links

- **All Pull Requests:** https://github.com/thefrederiksen/MyPhotoHelper/pulls
- **Issues:** https://github.com/thefrederiksen/MyPhotoHelper/issues
- **Branches:** https://github.com/thefrederiksen/MyPhotoHelper/branches
- **Repository:** https://github.com/thefrederiksen/MyPhotoHelper

---

*Last updated: 2025-01-04*
*This guide assumes you are the repository owner with merge permissions.*