# Deployment Guide for MyPhotoHelper

## Overview
MyPhotoHelper uses a **fully automated** deployment process through GitHub Actions. Simply create a git tag, and everything else is handled automatically.

## 🚀 Quick Release Process

### One Command Release
```bash
# Create and push a new version tag (e.g., v1.3.1)
git tag v1.3.1
git push origin v1.3.1
```

That's it! GitHub Actions will automatically:
1. ✅ Update version in project file
2. ✅ Build the release
3. ✅ Create the installer
4. ✅ Create GitHub release
5. ✅ Update AutoUpdater XML
6. ✅ Commit version changes back to main

## Version Management (Fully Automated)

### Version Format Guidelines
- **Git Tags**: `v1.3.1` (three parts with 'v' prefix)
- **Everything else is automated!**

### What Gets Updated Automatically
1. **Project File** (`src/MyPhotoHelper/MyPhotoHelper.csproj`)
   - All version fields updated to match tag
   - Committed back to main branch after release

2. **AutoUpdater XML** (`update.xml`)
   - Updated with new version and download URL
   - Committed back to main branch after release

3. **Installer** (`installer.iss`)
   - Version updated during build process

4. **GitHub Release**
   - Created with installer attached
   - Marked as latest release

## Detailed Release Process

### Step 1: Prepare Your Code
```bash
# Make sure all changes are committed
git add .
git commit -m "Your changes"
git push origin main

# Ensure all tests pass
dotnet test src/MyPhotoHelper.Tests
```

### Step 2: Create Release
```bash
# Create version tag (semantic versioning)
git tag v1.3.1

# Push tag to trigger automated release
git push origin v1.3.1
```

### Step 3: Monitor Progress
1. Go to [GitHub Actions](https://github.com/thefrederiksen/MyPhotoHelper/actions)
2. Watch the "Release with AutoUpdater.NET" workflow
3. Once complete, check [Releases](https://github.com/thefrederiksen/MyPhotoHelper/releases)

### Step 4: Verify Release
The workflow will automatically:
- Build with the new version number
- Create installer with correct version
- Upload to GitHub Releases
- Update `update.xml` for auto-updater
- Commit version changes to main branch

## No Manual Version Updates Needed! 🎉

The GitHub Actions workflow now:
- Extracts version from git tag
- Updates project file automatically
- Ensures version consistency everywhere
- Commits changes back to main

You **never** need to manually update version numbers anywhere!

## Testing Auto-Update

After release completes:
1. Run a previous version of the application
2. Go to Help → Check for Updates
3. Verify it detects and downloads the new version

## Common Scenarios

### Creating a Major Release
```bash
git tag v2.0.0
git push origin v2.0.0
```

### Creating a Patch Release
```bash
git tag v1.3.2
git push origin v1.3.2
```

### Creating a Pre-release (Beta)
```bash
git tag v1.4.0-beta.1
git push origin v1.4.0-beta.1
```

## Troubleshooting

### Issue: Workflow fails at "Update main branch" step
**Cause**: Branch protection rules preventing push
**Solution**: 
- Check repository settings → Branches
- Allow GitHub Actions to bypass protection rules
- Or manually merge the auto-generated PR

### Issue: Version not updating in application
**Cause**: Build cache issue
**Solution**: The workflow now handles this automatically by updating the project file before building

### Issue: Auto-updater not detecting new version
**Cause**: `update.xml` not updated
**Solution**: Check if the workflow completed successfully. The XML is updated automatically after release verification.

## Emergency Manual Release

If GitHub Actions is unavailable:

```powershell
# Use the prepare-release script
.\scripts\prepare-release.ps1 -Version 1.3.1

# Or manually:
# 1. Update version in MyPhotoHelper.csproj
# 2. Build release
dotnet publish src/MyPhotoHelper/MyPhotoHelper.csproj -c Release -r win-x64 --self-contained false -o publish

# 3. Create installer (update version in installer.iss first)
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer.iss

# 4. Create GitHub release manually and upload installer
# 5. Update update.xml manually
```

## Version History Format
- v1.3.1 - 2025-08-XX - [Description]
- v1.3.0 - 2025-08-08 - Major UI redesign with Tailwind CSS
- v1.2.8 - 2025-08-04 - Bug fixes and performance improvements

## Best Practices

1. **Always use semantic versioning**: MAJOR.MINOR.PATCH
2. **Test locally before tagging**: Run the application and tests
3. **Write clear commit messages**: They appear in release notes
4. **Monitor the GitHub Actions workflow**: Ensure it completes successfully
5. **Verify auto-update works**: Test from a previous version

## Release Checklist

### Before Creating Tag
- [ ] All tests pass locally
- [ ] Application runs without errors
- [ ] Changes are committed and pushed to main
- [ ] Release notes prepared (in your head or written down)

### After Pushing Tag
- [ ] GitHub Actions workflow started
- [ ] Workflow completed successfully
- [ ] Release appears on GitHub releases page
- [ ] Installer is attached to release
- [ ] update.xml shows new version in main branch
- [ ] Project file shows new version in main branch

## Summary

The deployment process is now **fully automated**:
1. Push code changes to main
2. Create and push a version tag
3. Everything else happens automatically!

No more manual version updates. No more version mismatches. Just tag and deploy! 🚀