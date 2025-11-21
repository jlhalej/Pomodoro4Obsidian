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
   <Version>1.5.5</Version>
   <AssemblyVersion>1.5.5.0</AssemblyVersion>
   <FileVersion>1.5.5.0</FileVersion>
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

### 2. Create GitHub Release

```powershell
.\scripts\create-github-release.ps1 -Version "1.5.5" -ReleaseNotes "Added scroll over timer to change the pomodoro session. Fixed minor bugs."
```

**Release Notes Format:**
Provide a brief, clear description of changes. The script will format it properly for GitHub.

**What this does:**
- Creates a new GitHub release with tag v1.5.5
- Uploads the installer files (Setup.exe, .nupkg, RELEASES)
- Marks the release as "Latest"
- Makes it available for the auto-update system
- Returns the release URL for verification

## 🧹 Cleanup

Build artifacts are automatically cleaned up during the release process. The `releases` folder is already in `.gitignore` and will not be committed.

## 📝 Commit Changes (If Needed)

If you made changes to the version number or other files, commit them:

### 1. Stage and Commit

```powershell
git add App/PomodoroForObsidian.csproj
git commit -m "Bump version to 1.5.5"
```

### 2. Push to GitHub

```powershell
git push origin master
```

**Note:** The Git tag is automatically created by the GitHub release script, so no manual tagging is needed.

**Important:** You can commit and push either before or after creating the GitHub release. The release artifacts (in `/releases/`) are in `.gitignore` and won't be committed.

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