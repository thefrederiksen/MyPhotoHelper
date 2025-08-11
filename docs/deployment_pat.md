# GitHub Personal Access Token (PAT) Setup for Release Workflow

This guide explains how to set up a Personal Access Token to allow the GitHub Actions release workflow to bypass branch protection rules and push version updates to the main branch.

## Step 1: Create the Personal Access Token

1. Go to GitHub.com and sign in
2. Click your profile picture → **Settings**
3. Scroll down to **Developer settings** (at the bottom of the left sidebar)
4. Click **Personal access tokens** → **Tokens (classic)**
5. Click **Generate new token** → **Generate new token (classic)**
6. Configure the token:
   - **Note**: `MyPhotoHelper Release Workflow`
   - **Expiration**: Choose your preference (recommend 90 days or custom)
   - **Select scopes**:
     - ✅ **repo** (Full control of private repositories) - Required to bypass branch protection
     - ✅ **workflow** (Update GitHub Action workflows) - Optional, only if updating workflows
7. Click **Generate token** at the bottom
8. **IMPORTANT**: Copy the token immediately - you won't be able to see it again!

## Step 2: Add the PAT to Repository Secrets

1. Navigate to your repository: `https://github.com/thefrederiksen/MyPhotoHelper`
2. Click **Settings** (repository settings, not your profile settings)
3. In the left sidebar, click **Secrets and variables** → **Actions**
4. Click **New repository secret**
5. Configure the secret:
   - **Name**: `RELEASE_PAT`
   - **Value**: Paste the PAT you copied from Step 1
6. Click **Add secret**

## Step 3: Verify the Workflow Configuration

The workflow at `.github/workflows/release.yml` has been updated to use the PAT in three places:

1. **Repository checkout**:
   ```yaml
   - uses: actions/checkout@v4
     with:
       fetch-depth: 0
       token: ${{ secrets.RELEASE_PAT }}
   ```

2. **Creating the GitHub release**:
   ```yaml
   - name: Create Release
     uses: softprops/action-gh-release@v2
     with:
       ...
       token: ${{ secrets.RELEASE_PAT }}
   ```

3. **Pushing version updates** (handled automatically via the checkout token)

## How It Works

When you create a new release by pushing a tag (e.g., `v1.3.4`), the workflow will:

1. Use the PAT to checkout the repository with write permissions
2. Update the version in `MyPhotoHelper.csproj`
3. Build and create the installer
4. Create a GitHub release with the installer
5. Update `update.xml` for AutoUpdater.NET
6. Push these changes back to the protected `main` branch using the PAT

The PAT bypasses branch protection rules because it has full repository access, unlike the default `GITHUB_TOKEN`.

## Security Notes

- Keep your PAT secure and never commit it to the repository
- Set an expiration date and rotate the token periodically
- Only grant the minimum required scopes (`repo` in this case)
- The PAT is only accessible to GitHub Actions through the secret

## Troubleshooting

If the workflow still fails to push:

1. **Verify the PAT has not expired** - Check token expiration in GitHub settings
2. **Ensure the secret name matches** - It must be exactly `RELEASE_PAT`
3. **Check branch protection rules** - Some rules may still block even PATs
4. **Verify PAT permissions** - Must have `repo` scope for private repos

## Token Renewal

When your PAT expires:

1. Generate a new token following Step 1
2. Update the `RELEASE_PAT` secret with the new token value
3. No workflow changes needed - it will use the updated secret automatically