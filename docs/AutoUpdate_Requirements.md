# Auto-Update Feature Requirements Document
## Pomodoro4Obsidian

### Current Application Analysis

#### Current State
- **Framework**: .NET 8.0 WPF Application  
- **Current Version**: 1.0.7.0 (defined in `PomodoroForObsidian.csproj`)
- **Distribution**: Currently no GitHub releases available, transitioning from portable to installer-based
- **Architecture**: Transitioning from portable to Squirrel.Windows installer-based
- **Target Platform**: Windows 10/11
- **Deployment Model**: Moving to Squirrel.Windows with NuGet packaging

#### Current Version Management
- Version defined in project file: `<Version>1.0.7.0</Version>`
- Version displayed in tray tooltip: `PomodoroForObsidian v{version}`
- Version logged to debug.csv when debug logging enabled
- No current update mechanism

#### Application Structure (Post-Squirrel)
```
Installation Directory (%LocalAppData%\PomodoroForObsidian\):
├── Update.exe                      # Squirrel updater
├── packages\                       # Downloaded update packages
│   ├── PomodoroForObsidian-1.0.7.0-full.nupkg
│   └── PomodoroForObsidian-1.0.8.0-delta.nupkg
├── app-1.0.7.0\                   # Current version folder
│   ├── PomodoroForObsidian.exe     # Main executable
│   ├── settings.json               # Local configuration
│   ├── VaultFilesList.json         # Obsidian vault files
│   ├── VaultTagsList.json          # Obsidian tags
│   ├── debug.csv                   # Debug logs (optional)
│   └── timer16.ico                 # Application icon
└── app-1.0.8.0\                   # New version folder (after update)
    └── [updated files]
```

---

## Auto-Update Feature Requirements (Squirrel.Windows)

### 1. Update Check System

#### 1.1 Version Detection
- **Current Version Source**: Read from `System.Reflection.Assembly.GetExecutingAssembly().GetName().Version`
- **Remote Version Source**: GitHub Releases with Squirrel UpdateManager
- **Version Format**: Semantic versioning (e.g., 1.0.7.0, 1.1.0.0)
- **Check Frequency**: 
  - Manual: "Check for Updates" button in Settings window
  - Automatic: Configurable (daily/weekly/on startup)
  - Background: Squirrel can check silently while app runs

#### 1.2 Update Discovery
- **Squirrel Integration**: Use `UpdateManager.GitHubUpdateManager()` for GitHub integration
- **Release Assets**: Squirrel automatically looks for `.nupkg` files and `RELEASES` file
- **Delta Updates**: Squirrel automatically handles delta packages for faster downloads
- **Version Comparison**: Squirrel handles version comparison automatically
- **Pre-release Handling**: Option to include/exclude pre-release versions

### 2. User Interface Integration

#### 2.1 Settings Window Updates
**Location**: Add new "Updates" panel to existing Settings window

**New UI Elements**:
```
┌─ Updates Panel ─────────────────────────────┐
│ Current Version: 1.0.7.0                   │
│                                             │
│ ☐ Check for updates automatically          │
│   ○ On startup                             │
│   ○ Daily                                  │
│   ○ Weekly                                 │
│                                             │
│ ☐ Include pre-release versions             │
│                                             │
│ [Check for Updates Now]                    │
│                                             │
│ Last checked: Never                        │
│ Status: Up to date                          │
└─────────────────────────────────────────────┘
```

#### 2.2 Update Notification System
- **System Tray Notification**: When update is available
- **Status Indicator**: Visual indicator in main/mini window
- **Update Dialog**: Dedicated window for update process

### 3. Download and Installation Process

#### 3.1 Download Management
- **Automatic Download**: Squirrel handles all download operations automatically
- **Download Location**: Managed by Squirrel in `%LocalAppData%\PomodoroForObsidian\packages\`
- **Progress Indication**: Access progress through Squirrel's event system
- **Integrity Verification**: Squirrel automatically verifies SHA1 hashes from RELEASES file
- **Delta Updates**: Only downloads changes between versions for efficiency
- **Error Handling**: Built-in retry logic and network error handling

#### 3.2 Installation Strategy
Squirrel.Windows implements **Background Delta Updates** with no restart required:

**Process Flow**:
1. Check for updates using `UpdateManager.CheckForUpdate()`
2. Download delta packages automatically to packages folder
3. Extract and apply updates to new `app-x.x.x` folder
4. Update application shortcuts and registry entries
5. Next application launch uses new version automatically
6. Clean up old version folders (configurable retention)

**Key Benefits**:
- **No restart required**: Updates apply in background while app runs
- **Delta updates**: Only downloads changed files (typically <1MB vs full package)
- **Atomic updates**: Either fully succeeds or fully rolls back
- **No file locking issues**: Updates to separate folder, switches on restart

#### 3.3 File Preservation
**Automatic Data Preservation**:
- User settings and data remain in consistent locations
- `%AppData%\PomodoroForObsidian\` for roaming data
- `%LocalAppData%\PomodoroForObsidian\` for application versions
- Settings persist across all updates automatically
- No manual migration required

### 4. Technical Implementation

#### 4.1 New Components Required

**Squirrel Integration Setup**:
```csharp
// Install NuGet package: Squirrel.Windows
// PM> Install-Package Squirrel.Windows

using Squirrel;
using System.Threading.Tasks;
```

**UpdateManager Class**:
```csharp
public class UpdateManager
{
    private readonly string _githubUrl;
    
    public event EventHandler<UpdateInfo> UpdateAvailable;
    public event EventHandler<int> UpdateProgress;
    public event EventHandler<UpdateInfo> UpdateCompleted;
    
    public UpdateManager(string githubUrl)
    {
        _githubUrl = githubUrl;
    }
    
    public async Task<UpdateInfo> CheckForUpdatesAsync()
    {
        using (var mgr = await Squirrel.UpdateManager.GitHubUpdateManager(_githubUrl))
        {
            return await mgr.CheckForUpdate();
        }
    }
    
    public async Task<bool> DownloadAndApplyUpdatesAsync()
    {
        using (var mgr = await Squirrel.UpdateManager.GitHubUpdateManager(_githubUrl))
        {
            var updateInfo = await mgr.CheckForUpdate();
            if (updateInfo.ReleasesToApply.Any())
            {
                await mgr.UpdateApp();
                return true;
            }
            return false;
        }
    }
    
    public Version GetCurrentVersion()
    {
        return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
    }
    
    public bool IsSquirrelInstalled()
    {
        var assembly = Assembly.GetEntryAssembly();
        var updateDotExe = Path.Combine(Path.GetDirectoryName(assembly.Location), "..", "Update.exe");
        return File.Exists(updateDotExe);
    }
}
```

**Squirrel Lifecycle Integration**:
```csharp
// Add to App.xaml.cs OnStartup
static int Main(string[] args) 
{
    // Handle Squirrel events FIRST before any other app logic
    using (var mgr = new Squirrel.UpdateManager("https://github.com/jlhalej/Pomodoro4Obsidian"))
    {
        SquirrelAwareApp.HandleEvents(
            onInitialInstall: v => mgr.CreateShortcutForThisExe(),
            onAppUpdate: v => mgr.CreateShortcutForThisExe(),
            onAppUninstall: v => mgr.RemoveShortcutForThisExe(),
            onFirstRun: () => ShowWelcomeDialog = true);
    }
    
    // Normal app startup continues...
}
```

**UpdateInfo Class (Squirrel Provided)**:
```csharp
// Squirrel provides this class automatically
public class UpdateInfo
{
    public ReleaseEntry CurrentlyInstalledVersion { get; set; }
    public ReleaseEntry FutureReleaseEntry { get; set; }
    public List<ReleaseEntry> ReleasesToApply { get; set; }
}

public interface ReleaseEntry
{
    public string SHA1 { get; set; }
    public string Filename { get; set; }
    public long Filesize { get; set; }
    public bool IsDelta { get; set; }
}
```

#### 4.2 Settings Integration
**Extend AppSettings.cs**:
```csharp
public class AppSettings
{
    // Existing properties...
    
    // Update settings
    public bool AutoUpdateEnabled { get; set; } = true; // Default enabled for Squirrel
    public string UpdateFrequency { get; set; } = "Weekly"; // Startup, Daily, Weekly
    public bool IncludePreReleases { get; set; } = false;
    public DateTime? LastUpdateCheck { get; set; }
    public string LastKnownVersion { get; set; } = string.Empty;
    public bool SquirrelFirstRun { get; set; } = false; // Track first run after install/update
}
```

#### 4.3 Project Configuration Changes
**Add to PomodoroForObsidian.csproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <!-- Existing properties -->
    
    <!-- Squirrel Configuration -->
    <AssemblyVersion>1.0.7.0</AssemblyVersion>
    <FileVersion>1.0.7.0</FileVersion>
    <Version>1.0.7.0</Version>
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="Squirrel.Windows" Version="2.0.1" />
  </ItemGroup>
  
  <!-- Squirrel Build Integration -->
  <Target Name="AfterBuild" Condition=" '$(Configuration)' == 'Release'">
    <GetAssemblyIdentity AssemblyFiles="$(TargetPath)">
      <Output TaskParameter="Assemblies" ItemName="myAssemblyInfo"/>
    </GetAssemblyIdentity>
    <Exec Command="nuget pack PomodoroForObsidian.nuspec -Version %(myAssemblyInfo.Version) -Properties Configuration=Release -OutputDirectory $(OutDir) -BasePath $(OutDir)" />
    <Exec Command="squirrel --releasify $(OutDir)PomodoroForObsidian.$([System.Version]::Parse(%(myAssemblyInfo.Version)).ToString(3)).nupkg" />
  </Target>
</Project>
```

**Create PomodoroForObsidian.nuspec**:
```xml
<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
  <metadata>
    <id>PomodoroForObsidian</id>
    <version>0.0.0.0</version> <!-- Replaced by MSBuild -->
    <title>Pomodoro4Obsidian</title>
    <authors>jlhalej</authors>
    <description>A portable Windows desktop application that combines the Pomodoro Technique with seamless Obsidian integration.</description>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <copyright>Copyright 2025</copyright>
    <dependencies />
  </metadata>
  <files>
    <file src="*.*" target="lib\net8.0-windows\" exclude="*.pdb;*.nupkg;*.vshost.*"/>
  </files>
</package>
```

**Add SquirrelAware Attribute to AssemblyInfo.cs**:
```csharp
using System.Windows;

[assembly: AssemblyMetadata("SquirrelAwareVersion", "1")]

[assembly:ThemeInfo(
    ResourceDictionaryLocation.None,
    ResourceDictionaryLocation.SourceAssembly
)]
```

### 5. Security Considerations

#### 5.1 Download Security
- **HTTPS Only**: Squirrel enforces HTTPS for all downloads automatically
- **Hash Verification**: SHA1 verification built into Squirrel via RELEASES file
- **Code Signing**: Integrate with Squirrel's signing pipeline using SignTool
- **Trusted Sources**: Squirrel only downloads from configured GitHub repository

**Code Signing Integration**:
```powershell
# Sign during build process
PM> Squirrel --releasify PomodoroForObsidian.1.0.8.nupkg -n "/a /f CodeCert.pfx /p CertPassword /fd sha256 /tr http://timestamp.digicert.com /td sha256"
```

#### 5.2 Installation Security
- **User-Level Installation**: Squirrel installs to `%LocalAppData%`, no admin required
- **Sandboxed Updates**: Each version in separate folder, atomic switching
- **Automatic Rollback**: Failed updates automatically revert to previous version
- **No Registry Pollution**: Minimal system changes, easy to uninstall

### 6. Error Handling and Recovery

#### 6.1 Network Issues
- **Built-in Retry Logic**: Squirrel handles network failures and retries automatically
- **Graceful Degradation**: Application continues working if updates unavailable
- **Offline Mode**: No disruption to core functionality when network unavailable

#### 6.2 Installation Failures
- **Automatic Rollback**: Squirrel reverts to previous version if update fails
- **Atomic Updates**: Updates either complete fully or not at all
- **No File Conflicts**: Separate version folders eliminate file locking issues
- **Diagnostic Logging**: Squirrel provides detailed logs for troubleshooting

#### 6.3 User Experience
- **Non-blocking Updates**: Updates download and apply in background
- **Seamless Transitions**: User barely notices update process
- **Clear Status**: Update progress visible in Settings window
- **Optional Manual Control**: Users can disable auto-updates if preferred

### 7. Testing Strategy

#### 7.1 Update Scenarios
- **Fresh Installation**: First-time update check
- **Version Upgrade**: Normal version increment
- **Downgrade Protection**: Prevent installing older versions
- **Settings Migration**: Handle configuration changes between versions

#### 7.2 Failure Testing
- **Network Interruption**: During download
- **Disk Space**: Insufficient space scenarios
- **File Permissions**: Limited write access
- **Corrupted Downloads**: Invalid or incomplete files

### 8. Deployment Strategy

#### 8.1 GitHub Releases Preparation
Squirrel requires specific release structure:

**Release Assets Required**:
- `PomodoroForObsidian-1.0.8.0-full.nupkg` - Full application package
- `PomodoroForObsidian-1.0.8.0-delta.nupkg` - Delta from previous version
- `RELEASES` - Index file with SHA1 hashes and metadata
- `Setup.exe` - Initial installer for new users

**RELEASES File Format**:
```
E3F67244E4166A65310C816221A12685C83F8E6F PomodoroForObsidian-1.0.7.0-full.nupkg 600725
0D777EA94C612E8BF1EA7379164CAEFBA6E24463 PomodoroForObsidian-1.0.8.0-delta.nupkg 6030
85F4D657F8424DD437D1B33CC4511EA7AD86B1A7 PomodoroForObsidian-1.0.8.0-full.nupkg 600752
```

**Release Naming Convention**: `v1.0.8.0`, `v1.1.0.0`, etc.

#### 8.2 Build Pipeline Integration (GitHub Actions)
```yaml
name: Build and Release

on:
  push:
    tags: ['v*']

jobs:
  build:
    runs-on: windows-latest
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'
        
    - name: Install NuGet
      uses: nuget/setup-nuget@v1
      
    - name: Restore dependencies
      run: dotnet restore
      
    - name: Build Release
      run: dotnet build -c Release --no-restore
      
    - name: Install Squirrel
      run: dotnet tool install --global Squirrel.Windows
      
    - name: Package and Releasify
      run: |
        nuget pack App/PomodoroForObsidian.nuspec -Properties Configuration=Release -OutputDirectory ./releases -BasePath ./App/bin/Release/net8.0-windows
        squirrel --releasify releases/PomodoroForObsidian.*.nupkg
        
    - name: Create GitHub Release
      uses: softprops/action-gh-release@v1
      with:
        files: |
          releases/Setup.exe
          releases/RELEASES
          releases/*.nupkg
```

### 9. Implementation Phases

#### Phase 1: Squirrel Integration Setup
1. Install Squirrel.Windows NuGet package
2. Create `.nuspec` file for packaging
3. Add SquirrelAware attribute to AssemblyInfo
4. Implement basic Squirrel lifecycle handling in App.xaml.cs
5. Test local Squirrel installation and updates

#### Phase 2: Update Manager Implementation
1. Create UpdateManager class with Squirrel integration
2. Add update checking functionality to Settings window
3. Implement background update checking with configurable frequency
4. Add update notifications and progress tracking
5. Test update flow with local packages

#### Phase 3: GitHub Integration and UI
1. Configure GitHubUpdateManager for repository
2. Create first GitHub release with Squirrel assets
3. Implement Settings window "Updates" panel
4. Add system tray notifications for available updates
5. Test end-to-end GitHub update process

#### Phase 4: Build Automation and Polish
1. Set up GitHub Actions for automated releases
2. Add code signing to build pipeline
3. Implement staged rollouts (optional)
4. Add comprehensive error handling and logging
5. Create user documentation and migration guide

### 10. Migration from Portable to Squirrel

#### 10.1 User Migration Strategy
**For Existing Portable Users**:
1. **Download new installer**: Users download Setup.exe from GitHub releases
2. **Install Squirrel version**: Installs to `%LocalAppData%\PomodoroForObsidian\`
3. **Import settings**: App detects and imports existing `settings.json` from old location
4. **Remove old version**: Users can manually delete old portable folder
5. **Automatic updates**: Future updates happen automatically

**Settings Migration Logic**:
```csharp
public void MigratePortableSettings()
{
    var portablePaths = new[]
    {
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "PomodoroForObsidian", "settings.json")
    };
    
    foreach (var path in portablePaths)
    {
        if (File.Exists(path))
        {
            var portableSettings = File.ReadAllText(path);
            var newPath = Path.Combine(AppData.LocalAppDataPath, "settings.json");
            File.WriteAllText(newPath, portableSettings);
            break;
        }
    }
}
```

#### 10.2 Advanced Features (Future)
- **Delta Updates**: Already built into Squirrel (downloads only changes)
- **Staged Rollouts**: Release to percentage of users using RELEASES file comments
- **Multiple Channels**: Separate stable/beta releases
- **Silent Updates**: Background updates with minimal user interaction
- **Update Scheduling**: Install updates during off-hours

#### 10.3 Analytics and Monitoring
- **Update Success Rates**: Monitor via GitHub release download statistics
- **Version Adoption**: Track active versions through anonymous telemetry (optional)
- **Error Reporting**: Squirrel provides detailed logging for diagnostics
- **Performance Metrics**: Delta update size vs. full package comparisons

---

## Conclusion

Migrating to Squirrel.Windows transforms Pomodoro4Obsidian from a manually distributed portable application to a professionally managed, auto-updating desktop application. Key benefits include:

**For Users**:
- ✅ Automatic background updates with no restarts
- ✅ Professional installation experience  
- ✅ Delta updates (faster downloads)
- ✅ Automatic rollback on failures
- ✅ No more manual ZIP downloads

**For Developers**:
- ✅ Proven, battle-tested update framework
- ✅ GitHub integration built-in
- ✅ Automated build pipeline support
- ✅ Comprehensive error handling
- ✅ Professional deployment model

**Technical Advantages**:
- ✅ No file locking issues (separate version folders)
- ✅ Atomic updates (all-or-nothing)
- ✅ Delta updates reduce bandwidth by 90%+
- ✅ Built-in code signing support
- ✅ Staged rollout capabilities

The phased implementation approach ensures a smooth transition while maintaining all existing functionality. Squirrel.Windows is the same technology used by Discord, Slack, WhatsApp Desktop, and many other successful Windows applications, providing a proven foundation for reliable auto-updates.
