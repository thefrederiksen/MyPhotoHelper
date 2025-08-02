# MyPhotoHelper Release Process

This guide explains how to create and publish a new release using AutoUpdater.NET for automatic updates.

## Quick Steps to Release

### 1. Update Version Number
Edit `src\MyPhotoHelper\MyPhotoHelper.csproj` and `installer.iss` with the new version:

**MyPhotoHelper.csproj:**
```xml
<AssemblyVersion>1.2.1.0</AssemblyVersion>
<FileVersion>1.2.1.0</FileVersion>
<ProductVersion>1.2.1</ProductVersion>
```

**installer.iss:**
```ini
AppVersion=1.2.1
```

### 2. Commit Your Changes
```bash
git add .
git commit -m "Prepare release v1.2.1"
git push
```

### 3. Create and Push a Version Tag
```bash
git tag v1.2.1
git push origin v1.2.1
```

**That's it!** GitHub Actions will automatically:
- Build the release with Inno Setup
- Create MyPhotoHelper-Setup.exe installer
- Generate update.xml for AutoUpdater.NET
- Upload to GitHub Releases
- Users will get the update notification automatically

## What Happens Next

1. **GitHub Actions** (5-10 minutes)
   - Builds the application with .NET 9
   - Creates Inno Setup installer (MyPhotoHelper-Setup.exe)
   - Generates update.xml for AutoUpdater.NET
   - Publishes to GitHub Releases
   - Check progress at: https://github.com/thefrederiksen/MyPhotoHelper/actions

2. **User Updates** (automatic)
   - App checks for updates automatically
   - AutoUpdater.NET handles the download and installation
   - Users see notification when update is available
   - One-click update process

## Version Numbering Guide

Use semantic versioning: `MAJOR.MINOR.PATCH`

- **PATCH** (1.0.1): Bug fixes, small improvements
- **MINOR** (1.1.0): New features, backwards compatible
- **MAJOR** (2.0.0): Breaking changes

## Pre-Release Checklist

Before creating a release:

- [ ] Test the application locally
- [ ] Update version in MyPhotoHelper.csproj
- [ ] Run `build-release.bat X.X.X` locally to test build
- [ ] Commit all changes
- [ ] Push to main branch

## Manual Release (If GitHub Actions Fails)

1. **Build locally**:
   ```cmd
   dotnet publish src/MyPhotoHelper -c Release -r win-x64 --self-contained false -o publish
   iscc installer.iss
   ```

2. **Create GitHub Release manually**:
   - Go to https://github.com/thefrederiksen/MyPhotoHelper/releases/new
   - Tag: `v1.2.1`
   - Title: `MyPhotoHelper v1.2.1`
   - Upload these files:
     - `Output/MyPhotoHelper-Setup.exe`
     - `update.xml`

## Troubleshooting Releases

**GitHub Action fails**
- Check: https://github.com/thefrederiksen/MyPhotoHelper/actions
- Common issues:
  - Missing GitHub token permissions
  - Build errors in code
  - Tag format must be `vX.X.X`

**Users not getting updates**
- AutoUpdater.NET checks automatically
- Users can manually check through app settings
- Firewall might block GitHub access
- Check update.xml file is valid and accessible

**Emergency Rollback**
If a release has critical issues:
1. Delete the release on GitHub (keeps the tag)
2. Fix the issue
3. Push a new patch version (e.g., 1.0.2)

## Release Notes Best Practices

When creating a release on GitHub:

```markdown
## What's New
- Feature: Added dark mode support
- Feature: Improved photo scanning speed

## Bug Fixes
- Fixed crash when scanning large folders
- Fixed memory leak in thumbnail generation

## Known Issues
- HEIC conversion may be slow on older systems
```

## Testing Before Release

Always test these scenarios:
1. Fresh installation works
2. Update from previous version works
3. Core features still function
4. No startup crashes

## Important URLs

- **Check Actions**: https://github.com/thefrederiksen/MyPhotoHelper/actions
- **View Releases**: https://github.com/thefrederiksen/MyPhotoHelper/releases
- **Download Latest**: https://github.com/thefrederiksen/MyPhotoHelper/releases/latest

## Summary

Creating a release is as simple as:
1. Update version in .csproj and installer.iss
2. Commit and push changes
3. Tag with `git tag vX.X.X` and push tag
4. Wait 5-10 minutes for automatic build and deployment
5. Users get updates automatically via AutoUpdater.NET

No manual building or uploading needed - completely automated with modern deployment!