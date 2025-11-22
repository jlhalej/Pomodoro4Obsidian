# Pomodoro4Obsidian

A Windows desktop application that integrates the Pomodoro Technique with Obsidian. Track focused work sessions and automatically log them to your Obsidian daily notes.

## Features

- Customizable timer with adjustable session lengths (5-2400 minutes)
- Click or scroll to adjust timer in increments of 5, 10 (Shift), or 30 (Alt) minutes
- Minimal interface with system tray integration
- Automatic session logging to Obsidian daily notes
- Task autocomplete based on session history
- Tag organization with `#` quick access
- Maximum session length enforcement
- Taskbar notch integration for persistent visibility

## Installation

### Requirements
- Windows 10/11
- .NET 8.0 Runtime
- Obsidian (for session logging)

### Setup

1. Download `PomodoroForObsidianSetup.exe` from the [latest release](../../releases/latest)
2. Run the installer
3. Configure settings on first launch:
   - **Obsidian Vault Path**: Root directory of your Obsidian vault
   - **Obsidian Journal Path**: Directory containing daily notes
   - **Journal Note Format**: Date format for daily notes (e.g., `YYYY-MM-DD`)
   - **Pomodoro Timer Length**: Session duration in HH:MM:SS format
   - **Maximum Session Length**: Maximum allowed session duration

## Usage

### Basic Operation
1. Enter task description in the input field
2. Adjust timer by clicking (increase) or right-clicking (decrease) on the timer display
3. Use modifier keys for larger adjustments: Shift (10 min) or Alt (30 min)
4. Click play to start the session
5. Sessions are automatically logged to your daily note when stopped

### Timer Adjustment
- **Left-click**: Increase by 5 minutes (10 with Shift, 30 with Alt)
- **Right-click**: Decrease by 5 minutes (10 with Shift, 30 with Alt)
- **Scroll wheel**: Adjust by 5 minutes per scroll

### Session Logging
Sessions are appended to your daily note under the configured header:
```markdown
# Pomodoro Sessions
- 14:00 - 14:25 Task description #tag 202507031400000
```

### Autocomplete
- Type to trigger suggestions based on recent and frequent tasks
- `Ctrl+R`: Show recent tasks
- `Ctrl+F`: Show frequent tasks
- `Ctrl+Space`: Force suggestions
- `#`: Access tag picker

## Configuration

Settings are accessible via the system tray icon. Available options:

- **Timer Duration**: Set default Pomodoro length
- **Maximum Session Length**: Automatic stop threshold
- **Taskbar Integration**: Enable taskbar notch for mini window
- **Auto-update**: Automatic update checking

## Uninstallation

Use Windows Settings → Apps → Installed apps, or see [MANUAL_UNINSTALL.md](MANUAL_UNINSTALL.md) for manual removal instructions.

## Development

Built with .NET 8.0 and WPF. Uses Clowd.Squirrel for auto-update functionality.

```bash
# Build
dotnet build -c Release

# Create release package
.\scripts\build-squirrel-release.ps1
```

See [DEVELOPMENT.md](_private_notes/general/DEVELOPMENT.md) for detailed development instructions.

## License

MIT License - see [LICENSE](LICENSE) file for details.


