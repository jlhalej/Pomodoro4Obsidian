# Pomodoro4Obsidian

A Windows desktop application that combines the Pomodoro Technique with seamless Obsidian integration. Track your focused work sessions and automatically log them to your Obsidian daily notes.


## ✨ Features

### 🍅 Pomodoro Timer
- **Customizable session lengths** - Set your preferred work and break durations
- **Mini-mode interface** - Minimal, distraction-free timer window
- **System tray integration** - Run silently in the background
- **Visual notifications** - Stay informed when sessions complete

### 📝 Obsidian Integration
- **Automatic logging** - Sessions are automatically written to your daily notes
- **Smart timestamping** - Each session gets a unique identifier for updates
- **Configurable format** - Customize how sessions appear in your notes
- **Project and tag support** - Organize your work with projects and tags

### 🎯 Productivity Features
- **Task descriptions** - Add context to what you're working on
- **Project management** - Categorize work by projects
- **Tag system** - Use Obsidian-style tags for organization
- **Session history** - Track your productivity over time

## 🚀 Quick Start

### Prerequisites
- Windows 10/11
- Obsidian (for note integration)

### Installation

1. Download `PomodoroForObsidianSetup.exe` from the [latest release](../../releases/latest)
2. Run the installer to install the application
3. The app will automatically check for updates in the background

> **Note**: This installer-based version includes automatic update functionality. Once installed, future updates will be downloaded and applied automatically.

### First-time Setup
1. Launch the application
2. Configure your Obsidian paths in the Settings window:
   - **Vault Path**: Path to your Obsidian vault root
   - **Journal Path**: Path to your daily notes folder
3. Customize your Pomodoro settings (optional)
4. Start your first session!

## 🎮 Usage

### Basic Workflow
1. **Set your task** - Describe what you're working on
2. **Choose project/tags** - Organize your work (optional)
3. **Start the timer** - Begin your focused work session
4. **Work without distractions** - The timer runs in mini-mode
5. **Automatic logging** - Session details are saved to Obsidian

### Keyboard Shortcuts
- `@` - Quick access to project picker
- `#` - Quick access to tag picker

### Obsidian Integration
Sessions are logged in this format:
```markdown
# Pomodoro Sessions
- 14:00 - 14:25 Write project documentation #Project/DocumentationA 202507031400000
- 14:30 - 14:55 Code timer logic #Project/Development 202507031430000
```

## ⚙️ Configuration

### Settings Options
- **Timer Duration**: Customize Pomodoro length (default: 25 minutes)
- **Journal Format**: Configure date format for daily notes
- **Auto-start**: Automatically begin breaks and sessions
- **Notifications**: Enable/disable alerts
- **Taskbar Integration**: Optional taskbar modifications

### 🔄 Auto-Update System
- **Background checking** - Automatically checks for updates every 24 hours
- **Manual updates** - Check for updates on-demand via Settings > Updates
- **Seamless installation** - Downloads and installs updates automatically
- **User control** - Enable/disable automatic updates in Settings

## 🛠️ Development

### Building the Project
```bash
# Clone the repository
git clone https://github.com/jlhalej/Pomodoro4Obsidian.git
cd Pomodoro4Obsidian

# Debug build
dotnet build

# Release build
dotnet build -c Release

# Create Squirrel release package
.\scripts\build-squirrel-release.ps1
```

For detailed build and release instructions, see [DEVELOPMENT.md](DEVELOPMENT.md).

### Project Structure
```
Pomodoro4Obsidian/
├── App/                    # Main application code
│   ├── Views/             # WPF windows and controls
│   ├── Models/            # Data models and settings
│   └── Services/          # Business logic
├── releases/              # Build outputs (not in repo)
└── docs/                  # Documentation
```

### Technologies Used
- **Framework**: .NET 8.0 with WPF
- **Language**: C#
- **Platform**: Windows (Win32 API integration)
- **File Format**: JSON for settings, Markdown for logs

## 🤝 Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

### Development Setup
1. Clone the repository
2. Open `Pomodoro4Obsidian.sln` in Visual Studio
3. Build and run the project
4. Make your changes
5. Submit a pull request

### Reporting Issues
Please use the [issue templates](../../issues/new/choose) when reporting bugs or requesting features.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Inspired by the Pomodoro Technique® by Francesco Cirillo
- Built for the Obsidian community
- Thanks to all contributors and users

## 📞 Support

- 📋 [Report Issues](../../issues)
- 💡 [Request Features](../../issues/new?template=feature_request.md)
- 📖 [View Documentation](../../wiki)

---

**Note**: This application is designed to work seamlessly with Obsidian but is not officially affiliated with Obsidian.md.

Pomodoro4Obsidian is a Windows desktop application that helps you implement the Pomodoro Technique with seamless Obsidian integration. Built with .NET 8.0 and WPF, it features automatic updates and professional installation.

## Features
- Circular Pomodoro timer with customizable durations
- Task input field with project and tag support
- System tray integration for background operation
- Automatic session logging to Obsidian daily notes
- Professional installer with automatic updates
- Settings management for personalized experience

## Getting Started
1. Download and run the installer from the releases page
2. Configure your Obsidian journal path in Settings
3. Start your Pomodoro sessions and track your productivity!

## Development
- Built with .NET 8.0 and WPF
- Uses Clowd.Squirrel for reliable auto-update functionality
- Settings stored in user profile for proper multi-user support

## 🗑️ Uninstallation

To uninstall Pomodoro4Obsidian:

1. **Via Windows Settings** (Recommended):
   - Settings → Apps → Installed apps → Search "Pomodoro" → Uninstall

2. **Manual Removal**: 
   - See [MANUAL_UNINSTALL.md](MANUAL_UNINSTALL.md) for detailed instructions
   - Includes removing app files, settings, and shortcuts

## 🔧 Development & Releases

- **Development Setup**: See [DEVELOPMENT.md](DEVELOPMENT.md)
- **Release Process**: See [scripts/RELEASE_GUIDE.md](scripts/RELEASE_GUIDE.md)
- **Build Scripts**: See [scripts/README.md](scripts/README.md)

---


