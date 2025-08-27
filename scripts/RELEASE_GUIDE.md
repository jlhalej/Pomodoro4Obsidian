# Release Guide for Pomodoro4Obsidian

This guide provides step-by-step instructions for creating and publishing new releases of Pomodoro4Obsidian.

## 📋 Prerequisites

Before creating a release, ensure you have:

- **Git** installed and authenticated with GitHub
- **GitHub CLI (gh)** installed and authenticated (`gh auth login`)
- **.NET 8.0 SDK** installed
- **PowerShell** (Windows PowerShell or PowerShell Core)
- **Write access** to the GitHub repository

## 🔢 Version Management

### Update Version Numbers

1. **Update the project version** in `App/PomodoroForObsidian.csproj`:
   ```xml
   <Version>1.2.2</Version>
   <AssemblyVersion>1.2.2.0</AssemblyVersion>
   <FileVersion>1.2.2.0</FileVersion>
   ```

2. **Test the build** to ensure everything compiles:
   ```powershell
   cd App
   dotnet build --configuration Release
   ```

## 🏗️ Build Process

### 1. Build Squirrel Release Package

From the repository root directory:

```powershell
.\scripts\build-squirrel-release.ps1 -Version "1.2.2"
```

**What this does:**
- Builds the application in Release configuration
- Creates the staging directory with all necessary files
- Generates the Squirrel package (.nupkg)
- Creates the installer (Setup.exe)
- Generates the RELEASES manifest file

**Expected output:**
```
✅ Squirrel release v1.2.2 created successfully!
📋 Release files generated:
  📄 Releases\PomodoroForObsidian-1.2.2-full.nupkg
  📄 Releases\PomodoroForObsidianSetup.exe
  📄 Releases\RELEASES
```

### 2. Create GitHub Release

```powershell
.\scripts\create-github-release.ps1 -Version "1.2.2" -ReleaseNotes "Your release notes here"
```

**Release Notes Template:**
```
## ✨ What's New in v1.2.2
- Feature: Description of new features
- Fix: Description of bug fixes
- Improvement: Description of improvements

## 🐛 Bug Fixes
- Fixed issue with X
- Resolved problem with Y

## 💡 Improvements
- Enhanced performance for Z
- Better error handling for A

## 🔄 Breaking Changes (if any)
- Changed behavior of X (migration instructions if needed)
```

**What this does:**
- Creates a new GitHub release with tag v1.2.2
- Uploads the installer files as release assets
- Marks the release as "Latest"
- Makes it available for the auto-update system

## 🧹 Cleanup

### Move Build Artifacts

```powershell
Move-Item -Path ".\releases" -Destination ".\NOT_GITHUB\releases_1.2.2" -Force
```

This keeps the repository clean by moving build artifacts to the ignored folder.

## 📝 Commit Changes

### 1. Stage and Commit

```powershell
git add .
git commit -m "v1.2.2: Release version

- Updated version to 1.2.2
- Description of changes made
- Any important notes for this release"
```

### 2. Push to GitHub

```powershell
git push origin master
```

**Note:** The Git tag is automatically created by the GitHub release script, so no manual tagging is needed.

## ✅ Post-Release Verification

### 1. Verify GitHub Release
- Visit: https://github.com/jlhalej/Pomodoro4Obsidian/releases
- Confirm the release appears with correct version
- Check that all files are attached (Setup.exe, .nupkg, RELEASES)

### 2. Test Auto-Update
- Install a previous version (if available)
- Run the application
- Go to Settings → Updates tab
- Click "Check for Updates"
- Verify it detects the new version
- Test the update process

### 3. Test Fresh Installation
- Download the Setup.exe from GitHub releases
- Install on a clean system
- Verify the application launches correctly
- Check that settings are created properly

## 🚨 Troubleshooting

### Common Issues

**Build Script Fails:**
- Ensure you're in the repository root directory
- Check that .NET 8.0 SDK is installed: `dotnet --version`
- Verify the version number format (e.g., "1.2.2", not "v1.2.2")

**GitHub Release Fails:**
- Ensure GitHub CLI is authenticated: `gh auth status`
- Check internet connection
- Verify repository access permissions

**Squirrel Package Issues:**
- Ensure the application builds successfully first
- Check for any missing dependencies in bin/Release
- Verify the .nuspec file is correctly formatted

**Auto-Update Not Working:**
- Confirm the RELEASES file is uploaded
- Check that version numbers are incremented properly
- Verify UpdateManager GitHub URL is correct

## 📂 File Structure After Release

```
NOT_GITHUB/
└── releases_1.2.2/           # Build artifacts (not in Git)
    ├── Releases/
    │   ├── PomodoroForObsidian-1.2.2-full.nupkg
    │   ├── PomodoroForObsidianSetup.exe
    │   └── RELEASES
    └── staging/               # Intermediate build files
```

## 🔄 Version Numbering Strategy

Use semantic versioning (SemVer):
- **Major (1.x.x)**: Breaking changes, major new features
- **Minor (x.2.x)**: New features, backwards compatible
- **Patch (x.x.3)**: Bug fixes, small improvements

Examples:
- `1.2.1` → `1.2.2`: Bug fix release
- `1.2.2` → `1.3.0`: New feature release
- `1.3.0` → `2.0.0`: Major version with breaking changes

## 📞 Emergency Procedures

### Hotfix Release
For critical bugs that need immediate release:

1. Create hotfix branch: `git checkout -b hotfix/1.2.3`
2. Make minimal necessary changes
3. Update version to 1.2.3
4. Follow normal release process
5. Merge back to master: `git checkout master && git merge hotfix/1.2.3`

### Rollback Release
If a release has critical issues:

1. Mark the problematic release as "Pre-release" on GitHub
2. Create a new patch version with fixes
3. The auto-update system will use the latest "Latest" release

---

## 🎯 Quick Reference Commands

```powershell
# Full release process (replace 1.2.2 with actual version)
cd C:\path\to\Pomodoro4Obsidian

# 1. Build package
.\scripts\build-squirrel-release.ps1 -Version "1.2.2"

# 2. Create release
.\scripts\create-github-release.ps1 -Version "1.2.2" -ReleaseNotes "Release notes here"

# 3. Cleanup
Move-Item -Path ".\releases" -Destination ".\NOT_GITHUB\releases_1.2.2" -Force

# 4. Commit
git add .
git commit -m "v1.2.2: Release description"
git push origin master
```

Remember: Always test thoroughly before releasing, and keep release notes clear and helpful for users!
