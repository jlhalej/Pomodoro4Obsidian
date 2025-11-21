# Release Guide for Pomodoro4Obsidian

This guide provides step-by-step instructions for creating and publishing new releases of Pomodoro4Obsidian.

## 📋 Prerequisites

Before creating a release, ensure you have:

- **Git** installed and authenticated with GitHub
- **GitHub CLI (gh)** installed and authenticated (`gh auth login`)
- **.NET 8.0 SDK** installed. You can verify your version with `dotnet --version`.
- **PowerShell** (Windows PowerShell or PowerShell Core)
- **Write access** to the GitHub repository

## 🔢 Version Management

### Update Version Numbers

1. **Update the project version** in `App/PomodoroForObsidian.csproj`:
   ```xml
   <Version>1.3.2</Version>
   <AssemblyVersion>1.3.2.0</AssemblyVersion>
   <FileVersion>1.3.2.0</FileVersion>
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
.\scripts\build-squirrel-release.ps1 -Version "1.3.2"
```

**Note:** If the script opens in a text editor instead of running, you may need to execute it explicitly with PowerShell:
```powershell
powershell -File .\scripts\build-squirrel-release.ps1 -Version "1.3.2"
```

**What this does:**
- Builds the application in Release configuration
- Creates the staging directory with all necessary files
- Generates the Squirrel package (.nupkg)
- Creates the installer (Setup.exe)
- Generates the RELEASES manifest file

**Expected output:**
```
✅ Squirrel release v1.3.2 created successfully!
📋 Release files generated:
  📄 Releases\PomodoroForObsidian-1.3.2-full.nupkg
  📄 Releases\PomodoroForObsidianSetup.exe
  📄 Releases\RELEASES
```

### 2. Create GitHub Release

```powershell
.\scripts\create-github-release.ps1 -Version "1.3.2" -ReleaseNotes "Your release notes here"
```

**Release Notes Template:**
```
## ✨ What's New in v1.3.2
- Feature: Description of new features
- Fix: Description of bug fixes
- Improvement: Description of improvements
```

**What this does:**
- Creates a new GitHub release with tag v1.3.2
- Uploads the installer files as release assets
- Marks the release as "Latest"
- Makes it available for the auto-update system

## 🧹 Cleanup

Build artifacts are automatically cleaned up during the release process. The `releases` folder is already in `.gitignore` and will not be committed.

## 📝 Commit Changes

### 1. Stage and Commit

```powershell
git add .
git commit -m "v1.3.2: Release version"
```

### 2. Push to GitHub

```powershell
git push origin master
```

**Note:** The Git tag is automatically created by the GitHub release script, so no manual tagging is needed.

## ✅ Post-Release Verification

- Visit: https://github.com/jlhalej/Pomodoro4Obsidian/releases
- Confirm the release appears with the correct version and assets.

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