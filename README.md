# Pomodoro4Obsidian

A portable Windows desktop application that combines the Pomodoro Technique with seamless Obsidian integration. Track your focused work sessions and automatically log them to your Obsidian daily notes.

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)

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

#### Option 1: Download Release (Recommended)
1. Download the latest release from the [Releases page](../../releases)
2. Extract the ZIP file to your preferred location
3. Run `Pomodoro4Obsidian.exe`

#### Option 2: Build from Source
```bash
git clone https://github.com/jlhalej/Pomodoro4Obsidian.git
cd Pomodoro4Obsidian
dotnet build -c Release -o ./releases
```

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

### Portable Design
- **No installation required** - Run from any location
- **Self-contained** - All settings stored locally
- **No registry changes** - Completely portable
- **USB friendly** - Perfect for shared computers

## 🛠️ Development

### Building the Project
```bash
# Debug build
dotnet build

# Release build
dotnet build -c Release

# Self-contained deployment
dotnet publish -c Release -r win-x64 --self-contained true -o ./releases
```

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

Pomodoro4Obsidian is a portable, self-contained Pomodoro timer app for Windows, built with C# and WPF. It features a modern UI, system tray integration, and seamless logging to Obsidian daily notes.

## Features
- Circular Pomodoro timer
- Task input field
- Project and tags dropdowns
- Start/stop button
- Portable: no installation required
- Obsidian integration for session logging

## Getting Started
1. Run the executable directly from any folder (no installation needed).
2. Configure your preferences, including the Obsidian journal path.
3. Start your Pomodoro sessions and track your work!

## Development
- Built with .NET 8.0 and WPF
- All configuration and data files are stored locally in the app directory

---


