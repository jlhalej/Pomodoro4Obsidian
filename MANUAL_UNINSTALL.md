# Manual Uninstall Instructions

## 🗑️ How to Manually Uninstall Pomodoro4Obsidian

Since the current version doesn't include an automatic uninstaller, here are the steps to completely remove the application from your system:

### Step 1: Close the Application
```
1. Right-click the system tray icon and select "Exit"
2. Or close all app windows
3. Check Task Manager to ensure no PomodoroForObsidian processes are running
```

### Step 2: Remove Application Files and Settings
Squirrel installs the app in your user profile, and settings are now stored alongside the application. The location is:
```
%LOCALAPPDATA%\PomodoroForObsidian\
```

**To remove:**
1. Open File Explorer
2. Navigate to: `%LOCALAPPDATA%\PomodoroForObsidian\` 
   - Or type in address bar: `C:\Users\[YourUsername]\AppData\Local\PomodoroForObsidian\`
3. Delete the entire `PomodoroForObsidian` folder
   - This removes both the application and your settings (settings.json, debug logs, etc.)

### Step 3: Remove Shortcuts
**Start Menu:**
1. Open Start Menu
2. Find "Pomodoro For Obsidian" in the app list
3. Right-click → "Uninstall" or "Remove"
4. If that doesn't work, manually delete from:
   ```
   %APPDATA%\Microsoft\Windows\Start Menu\Programs\PomodoroForObsidian.lnk
   ```

**Desktop:**
- Delete any desktop shortcut if created

### Step 4: Remove from Windows Programs List
1. Open **Settings** → **Apps** → **Installed apps**
2. Search for "Pomodoro" or "PomodoroForObsidian"
3. Click **...** → **Uninstall**

If it doesn't appear in the list, it may not be properly registered.

### Step 5: Clean Registry (Optional - Advanced Users)
⚠️ **Warning**: Only do this if you're comfortable editing the registry

1. Open **Registry Editor** (regedit)
2. Navigate to: `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall\`
3. Look for entries containing "PomodoroForObsidian" 
4. Delete any found entries

### Step 7: Restart System (Optional)
- Restart to ensure all components are fully removed
- This clears any remaining processes or file locks

## 📂 Summary of Locations to Check

| Component | Location | Purpose |
|-----------|----------|---------|
| **Application & Settings** | `%LOCALAPPDATA%\PomodoroForObsidian\` | Main app files and user settings |
| **Start Menu** | `%APPDATA%\Microsoft\Windows\Start Menu\Programs\` | Shortcuts |
| **Desktop** | `%USERPROFILE%\Desktop\` | Desktop shortcut |

## 🔍 Verification

After completing all steps, verify removal by:
- [ ] No entries in Add/Remove Programs
- [ ] No shortcuts in Start Menu
- [ ] No desktop shortcuts
- [ ] No folders in AppData locations
- [ ] No processes in Task Manager

## 🚀 Future Enhancement

In a future version, we'll add a proper uninstaller that handles all these steps automatically. For now, this manual process ensures complete removal.

---

💡 **Tip**: If you're planning to reinstall, you can backup the settings file (`%LOCALAPPDATA%\PomodoroForObsidian\settings.json`) before deletion to preserve your configuration.
