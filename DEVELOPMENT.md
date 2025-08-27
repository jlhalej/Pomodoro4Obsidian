# Development & Build Guide

## Auto-Update System

This application uses **Clowd.Squirrel** for professional auto-update functionality. The implementation provides:

- ✅ Background update checking every 24 hours
- ✅ Manual update checking via Settings > Updates tab  
- ✅ Seamless download and installation of updates
- ✅ GitHub-integrated release distribution

### Technical Details

- **Update Framework**: Clowd.Squirrel 2.11.1 (.NET 8 compatible)
- **Distribution**: GitHub Releases with Squirrel-compatible packages
- **Installation**: Professional installer (`Setup.exe`) with automatic updates
- **Versioning**: SemVer-compatible (3-part versioning for Squirrel)

## Build & Release Process

### Prerequisites
- .NET 8.0 SDK
- PowerShell 5.0+ 
- GitHub CLI (`gh`) for automated releases

### Building a Release

1. **Update version** in `App/PomodoroForObsidian.csproj`
2. **Build Squirrel package**:
   ```powershell
   .\scripts\build-squirrel-release.ps1
   ```
3. **Create GitHub release**:
   ```powershell
   .\scripts\create-github-release.ps1
   ```

### Testing
```powershell
.\scripts\test-update-functionality.ps1
```

## Architecture

- **UpdateManager.cs**: Core update functionality with GitHub integration
- **SettingsWindow**: Updates tab for user control
- **App.xaml.cs**: Background update checking on startup
- **Build Scripts**: Automated release creation and publishing
