<!-- Use this file to provide workspace-specific custom instructions to Copilot. For more details, visit https://code.visualstudio.com/docs/copilot/copilot-customization#_use-a-githubcopilotinstructionsmd-file -->

# Pomodoro4Obsidian - GitHub Copilot Instructions

## Project Overview
This project is a **C# .NET 8.0 WPF desktop application** for Windows that integrates the Pomodoro Technique with Obsidian. The app provides a timer with Obsidian vault integration, automatic session logging, task autocomplete based on history, and project/tag organization. It uses Squirrel.Windows for automatic updates and includes an installer.

### Key Features
- Circular timer display with click/scroll adjustments
- Task input with project and tag dropdowns
- Automatic Obsidian daily note logging
- Task autocomplete from session history
- System tray integration with taskbar notch support
- Session history persistence (JSON-based)
- Modern, clean UI following WPF/XAML best practices

## Technology Stack
- **Framework**: .NET 8.0 with WPF
- **UI**: XAML markup with code-behind
- **Persistence**: JSON files for task history
- **Versioning**: .NET assemblies (currently v1.7.0)
- **Deployment**: Squirrel.Windows installer with automatic updates
- **Language**: C#

## Code Organization & Architecture

### Directory Structure
```
App/
├── XAML Views (MainWindow, SettingsWindow, ProjectPickerWindow, MiniWindow)
├── Code-behind (.xaml.cs files)
├── Managers/ (AutoCompleteManager for tag/task suggestions)
├── Model/ (TaskHistoryEntry data model)
├── Persistence/ (JsonTaskHistoryRepository for data storage)
├── Interfaces/ (ITaskHistoryRepository contract)
├── Utils.cs (Utility functions)
├── AppSettings.cs (Configuration management)
└── [Manager classes for tasks, UI, updates]

_private_notes/
└── [Development documentation, debugging notes, planning docs]

scripts/
└── PowerShell release and build automation
```

### Core Components
- **MainWindow.xaml/cs**: Primary UI with timer, task input, dropdowns
- **PomodoroSessionManager.cs**: Session logic and Obsidian integration
- **AutoCompleteManager.cs**: Task/tag suggestion engine
- **JsonTaskHistoryRepository.cs**: Persistence layer for task history
- **UpdateManager.cs**: Squirrel-based update handling
- **SettingsWindow.xaml/cs**: Configuration UI for paths, timer lengths, etc.
- **TrayIcon.cs**: System tray functionality

## Coding Standards & Conventions

### C# & .NET
- Use **nullable reference types** (`Nullable>enable</Nullable>` is set in .csproj)
- Follow **PascalCase** for class/method names, **camelCase** for local variables
- Use meaningful variable names; avoid abbreviations except common ones (async = `Task`, collection = `List<T>`)
- Mark async methods with `async` keyword and return `Task` or `Task<T>`
- Use null-coalescing operators (`??`) and pattern matching where appropriate

### XAML & WPF
- Keep XAML clean and readable; avoid overly complex markup
- Use data binding (`{Binding}`) for UI-to-logic communication
- Define UI resources in `App.xaml` when shared across multiple windows
- Follow naming convention: `Window` for main windows, `[Name]Window` for dialogs
- Use `Visibility` bindings and converters for conditional UI display

### Error Handling
- Catch specific exceptions, not generic `Exception`
- Log exceptions with context (use existing logging patterns in codebase)
- Avoid silent failures; provide user feedback through UI or logs
- For file operations (Obsidian paths), validate paths exist before access

### Comments & Documentation
- Write clear, concise comments explaining *why*, not *what*
- Document public methods with XML comments (`///`)
- Keep comments up-to-date when code changes
- For complex logic, include example usage or edge cases

## File Management and Security
- **Private Development**: All AI-generated documents, brainstorming notes, and non-code assets go in `_private_notes/`
  - This directory is gitignored and never committed
  - Place planning documents, debug notes, architecture sketches here
  - Exception: Public-facing documentation (README, MANUAL_UNINSTALL, etc.) stays in repo root
- **Code Files**: All production code and public-facing docs belong in the main repo tree

## Git and GitHub Operations
- **Use GitHub CLI** (`gh`) for all Git/GitHub operations (releases, PRs, issues)
- Create meaningful commit messages: `feat: add feature`, `fix: resolve issue`, `docs: update README`
- For releases: Use the `scripts/create-github-release.ps1` PowerShell script
- For updates/versioning: Update version in `PomodoroForObsidian.csproj` (`<Version>` and `<AssemblyVersion>`)
- Use `master` as default branch

## Development Workflow

### Building & Testing
- Build command: `dotnet build` from the App directory
- Release build: `dotnet publish -c Release`
- Use `scripts/build-squirrel-release.ps1` for creating distributable packages
- Run `scripts/test-update-functionality.ps1` to validate update mechanism

### Debugging Tips
- The app logs session history to `AppData/Local/Pomodoro4Obsidian/task_history.json`
- Obsidian paths are stored in `AppSettings.cs` 
- Check Windows Event Viewer for WPF rendering issues
- Squirrel updates cache in `AppData/Local/[App]` - clear when testing fresh installs

### Common Tasks
- **Adding a new setting**: Update `AppSettings.cs`, then add UI in `SettingsWindow.xaml`
- **Modifying session logging**: Edit `PomodoroSessionManager.cs` and corresponding Obsidian integration
- **Extending task autocomplete**: Update `AutoCompleteManager.cs` and test with `JsonTaskHistoryRepository`
- **Releasing a new version**: Bump version in `.csproj`, commit, run release script, merge to master

## Important Patterns & Considerations

### Obsidian Integration
- Always validate vault and journal paths before file operations
- Use date format specified in settings (`AppSettings.JournalNoteFormat`)
- Handle file encoding (UTF-8) for daily note writes
- Gracefully handle missing Obsidian vault (show warning, don't crash)

### Task History & Autocomplete
- Task history is stored as JSON array in user's AppData directory
- `ITaskHistoryRepository` defines the persistence contract
- Respect maximum session length limits (enforced in UI and session manager)
- Autocomplete should be case-insensitive and fuzzy-match friendly

### UI/UX Requirements
- Minimize window chrome; use clean, modern design
- Circular timer should be prominent and responsive to clicks/scrolls
- All interactive elements should have clear visual feedback
- Avoid modal dialogs for quick operations (prefer inline editing or toasts)

### Versioning & Updates
- Follow semantic versioning (MAJOR.MINOR.PATCH)
- Update version in both `<Version>` and `<AssemblyVersion>` in .csproj
- Test updates with `test-update-functionality.ps1` before release
- Squirrel handles delta updates automatically

## Documentation & Notes
- **README.md**: User-facing guide (features, installation, usage)
- **MANUAL_UNINSTALL.md**: Removal instructions
- **scripts/RELEASE_GUIDE.md**: Release process documentation
- **_private_notes/**: Internal design docs, bug analyses, implementation plans

## Key Dependencies
- **Clowd.Squirrel** (v2.11.1): Update mechanism and installer
- **.NET 8.0**: Framework libraries for WPF and system integration

## Version Bumping
- When instructed to **"bump the version"**, increment the patch version (x.x.Z → x.x.Z+1) unless specified otherwise
  - Example: 1.7.0 → 1.7.1
  - Update both `<Version>` and `<AssemblyVersion>` in `PomodoroForObsidian.csproj`
  - If user specifies a different version scheme (e.g., "bump minor"), follow that instead
  
## Deployment Instructions
- When instructed to **"deploy to client"** or similar deployment commands:
  1. **Bump the version** (patch version by default, unless specified)
  2. **Reference and follow** `scripts/RELEASE_GUIDE.md` for the complete deployment process
  3. Use `scripts/build-squirrel-release.ps1` to create the distributable
  4. Use `scripts/create-github-release.ps1` to create the GitHub release
  5. Verify all steps in the deployment guide before completing

---

When assisting with this project, prefer to:
1. **Suggest** changes before implementing if they affect UI/UX or core logic
2. **Maintain consistency** with existing code style and architecture
3. **Test implications** of changes across related components (e.g., settings → session manager)
4. **Reference existing patterns** in codebase rather than introducing new approaches
5. **Keep the user informed** of any changes to public-facing files or breaking changes