# Build & Release Scripts

This folder contains PowerShell scripts for building and releasing Pomodoro4Obsidian.

## Scripts Overview

### 🔨 `build-squirrel-release.ps1`
Creates a Squirrel-compatible release package.

**Usage:**
```powershell
.\build-squirrel-release.ps1 [-Version "1.0.9"] [-Configuration "Release"]
```

**What it does:**
- Builds the application in Release mode
- Creates Squirrel package (.nupkg)
- Generates installer (Setup.exe)
- Creates RELEASES metadata file

### 🚀 `create-github-release.ps1`
Publishes a GitHub release with Squirrel files.

**Usage:**
```powershell
.\create-github-release.ps1 [-Version "1.0.9"] [-ReleaseNotes "Bug fixes"]
```

**Requirements:**
- GitHub CLI (`gh`) installed and authenticated
- Release files must exist (run build script first)

### 🧪 `test-update-functionality.ps1`
Validates the update system implementation.

**Usage:**
```powershell
.\test-update-functionality.ps1
```

**Checks:**
- Application builds successfully
- Required Squirrel files exist
- UpdateManager integration
- Settings UI integration

## Typical Workflow

📋 **For detailed step-by-step instructions, see [RELEASE_GUIDE.md](RELEASE_GUIDE.md)**

1. Update version in `App/PomodoroForObsidian.csproj`
2. Run build script: `.\build-squirrel-release.ps1`
3. Test functionality: `.\test-update-functionality.ps1`
4. Create release: `.\create-github-release.ps1`

## Prerequisites

- .NET 8.0 SDK
- PowerShell 5.0+
- GitHub CLI for releases
- Clowd.Squirrel NuGet package (auto-installed)

## Security Note

These scripts use GitHub CLI with your authenticated account. Ensure you have the necessary repository permissions before running release scripts.
