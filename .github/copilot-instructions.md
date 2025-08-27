<!-- Use this file to provide workspace-specific custom instructions to Copilot. For more details, visit https://code.visualstudio.com/docs/copilot/copilot-customization#_use-a-githubcopilotinstructionsmd-file -->

This project is a C# .NET WPF Pomodoro timer app for Windows. The main window should include a circular timer, a task input, project and tags dropdowns, and a start/stop button. The design should be clean, modern, and professional, following the design document. The app uses an installer with automatic update functionality.


# GitHub Copilot Instructions

## File Management and Security
- This project maintains a strict separation between public code and private development files.
- All AI-generated documents, brainstorming notes, and other non-code assets must be stored in a directory named `_private_notes/`.
- This `_private_notes/` directory should be listed in the `.gitignore` file and must never be committed to the repository. 
- When generating new documentation or helper files, always place them inside `_private_notes/` unless explicitly told to create a public-facing document.
- Do not suggest committing files from the `_private_notes/` directory.
- If you create plan documents, not related to the actual code, place them in the `_private_notes/` directory.