# Release Guide for Pomodoro4Obsidian

This guide provides step-by-step instructions for creating and publishing new releases of Pomodoro4Obsidian.

## 📋 Prerequisites

Before creating a release, ensure you have:

- **Git** installed and authenticated with GitHub
- **GitHub CLI (gh)** installed and authenticated (`gh auth login`)
- **.NET 8.0 SDK** installed. You can verify your version with `dotnet --version`.
- **PowerShell** (Windows PowerShell or PowerShell Core)
- **Write access** to the GitHub repository

## 🔄 Release Process Overview

Follow these steps **in order**:
1. **Code changes & version bump** (commit & push)
2. **Build Squirrel release package**
3. **Create GitHub release**

## 🔢 Version Management

### Step 1: Update Version Numbers

1. **Update the project version** in `App/PomodoroForObsidian.csproj`:
   ```xml
   <Version>1.5.5</Version>
   <AssemblyVersion>1.5.5.0</AssemblyVersion>
   <FileVersion>1.5.5.0</FileVersion>
   ```

2. **Test the build** to ensure everything compiles:
   ```powershell
   cd App
   dotnet build --configuration Release
   ```

3. **Commit ALL changes** to Git:
   
   a) **First, commit all code changes** (if any):
   ```powershell
   git status  # Check for any uncommitted changes
   git add .   # Add all changed files
   git commit -m "Fix [brief description of what was changed]"
   ```
   
   b) **Then, commit the version change**:
   ```powershell
   git add App/PomodoroForObsidian.csproj
   git commit -m "Bump version to 1.5.5"
   git push origin master
   ```
   ⚠️ **Important:** Push ALL changes (code + version bump) BEFORE building the release package.

## 🏗️ Build Process

### Step 2: Build Squirrel Release Package

**Ensure all code and version changes are committed and pushed BEFORE this step.**

From the repository root directory:

```powershell
.\scripts\build-squirrel-release.ps1
```

**Note:** The script automatically detects the version from `PomodoroForObsidian.csproj`. You can also specify it manually with `-Version "1.5.5"` if needed.

**What this does:**
- Builds the application in Release configuration
- Creates the staging directory with all necessary files
- Generates the Squirrel package (.nupkg)
- Creates the installer (Setup.exe)
- Generates the RELEASES manifest file

**Expected output:**
```
Squirrel release v1.5.5 created successfully!
Release files located in: C:\...\releases
  Releases\PomodoroForObsidian-1.5.5-full.nupkg
  Releases\PomodoroForObsidianSetup.exe
  Releases\RELEASES
```

### Step 3: Create GitHub Release

**Do NOT skip or reorder this step. Only create the GitHub release after:**
- ✅ All code changes are committed and pushed
- ✅ Version number is updated and committed
- ✅ Squirrel release package has been built successfully

From the repository root directory:
**Release Notes Format:**
Provide a brief, clear description of changes. The script will format it properly for GitHub.

**What this does:**
- Creates a new GitHub release with tag v1.5.5
- Uploads the installer files (Setup.exe, .nupkg, RELEASES)
- Marks the release as "Latest"
- Makes it available for the auto-update system
- Returns the release URL for verification

⚠️ **Important:** Do not create the GitHub release until all changes are committed and pushed. The release should correspond to a specific commit on the master branch.

## 🧹 Cleanup

Build artifacts are automatically cleaned up during the release process. The `releases` folder is already in `.gitignore` and will not be committed.

## ✅ Pre-Release Checklist

Before deploying, verify:
- [ ] All code changes are committed to `master` branch
- [ ] Version bumped in `App/PomodoroForObsidian.csproj` (both `<Version>` and `<AssemblyVersion>`)
- [ ] Version commit pushed to GitHub
- [ ] Project builds without errors: `cd App && dotnet build --configuration Release`
- [ ] All changes are pushed (verify with `git status` - should show "nothing to commit")
- [ ] Squirrel release package built successfully
- [ ] Release files exist in `releases/Releases/` directory
- [ ] Creating GitHub release **after** all above steps are complete

## ✅ Post-Release Verification

- Visit: https://github.com/jlhalej/Pomodoro4Obsidian/releases
- Confirm the release appears with the correct version and assets
- Verify the release tag matches the version number (e.g., v1.5.5)
- Test the auto-update mechanism in the application

## 🚨 Troubleshooting

### PowerShell Script Issues

- **`ParserError`:** If you encounter a `ParserError` when running a script, the file may be corrupted. Restore the script from your version control to get a clean copy.
- **`UnauthorizedAccessException` on `RELEASES` file:** This error means the script can't write to the `releases` directory. To fix this, manually delete the `releases` directory from the project root before running the build script. The script is designed to recreate it.

### Build Script Fails

- Ensure you're in the repository root directory.
- Check that the correct .NET SDK is installed: `dotnet --version`.

### GitHub Release Fails

- Ensure GitHub CLI is authenticated: `gh auth status`.
- Check your internet connection and repository permissions.