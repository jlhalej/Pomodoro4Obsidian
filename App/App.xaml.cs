using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using PomodoroForObsidian; // Add this using directive

namespace PomodoroForObsidian
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private TrayIcon? _trayIcon;
        private bool _journalChecked = false;
        private AppSettings? _settings;
        private MiniWindow? _miniWindow;
        private TaskbarManager? _taskbarManager;

        public static bool IsShuttingDown { get; private set; } = false;

        protected override void OnStartup(StartupEventArgs e)
        {            
            base.OnStartup(e);
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _trayIcon = new TrayIcon();
            bool isFirstRun;
            string settingsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            bool settingsExists = System.IO.File.Exists(settingsPath);
            Utils.LogDebug("App", $"settings.json found: {settingsExists}");
            _settings = PomodoroForObsidian.AppSettings.Load(out isFirstRun);

            // Initialize the taskbar manager
            _taskbarManager = new TaskbarManager();
            
            // Configure from settings
            if (_settings != null && _taskbarManager != null)
            {
                _taskbarManager.TaskbarModificationEnabled = _settings.TaskbarModificationEnabled;
                _taskbarManager.TaskbarNotchWidth = _settings.TaskbarNotchWidth;
                _taskbarManager.TaskbarNotchHeight = _settings.TaskbarNotchHeight;
                _taskbarManager.TaskbarNotchPosition = _settings.TaskbarNotchPosition;
                _taskbarManager.CornerRadius = _settings.TaskbarCornerRadius;
                
                // Apply taskbar modification if enabled
                if (_settings.TaskbarModificationEnabled)
                {
                    Utils.LogDebug("App", "Taskbar modification enabled, creating notch");
                    _taskbarManager.CreateNotch();
                }
            }

            // Test blinking functionality after short delay
            Dispatcher.BeginInvoke(new Action(() => {
                Utils.LogDebug("App", "Testing tray icon blinking on startup");
                TestTrayIconBlinking();
            }), System.Windows.Threading.DispatcherPriority.Background);

            // If first run, open preferences window immediately after creating settings
            if (isFirstRun)
            {
                Utils.LogDebug("App", "First run detected: opening preferences window after creating settings.json.");
                MessageBoxResult result = MessageBox.Show(
                    "It seems this is the first time you run Pomodoro for Obsidian. The settings page will be opened now, so you can indicate the path of your Journal",
                    "Welcome to Pomodoro for Obsidian",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                var settingsWindow = new SettingsWindow();
                settingsWindow.ShowDialog();
                // Reload settings after user saves them
                _settings = PomodoroForObsidian.AppSettings.Load();
            }

            // Now check if any of the paths is blank
            bool journalSet = !string.IsNullOrWhiteSpace(_settings.ObsidianJournalPath);
            bool vaultSet = !string.IsNullOrWhiteSpace(_settings.ObsidianVaultPath);
            Utils.LogDebug("App", $"ObsidianJournalPath set: {journalSet}, ObsidianVaultPath set: {vaultSet}");
            if (!journalSet || !vaultSet)
            {
                Utils.LogDebug("App", "Journal or vault path is blank. Prompting user to set them in preferences.");
                Utils.MessageBoxNotification("Please set both the Obsidian Journal Path and Vault Path in Preferences.");
                var settingsWindow = new SettingsWindow();
                settingsWindow.ShowDialog();
                // Reload settings after user saves them
                _settings = PomodoroForObsidian.AppSettings.Load();
            }
            else
            {
                Utils.LogDebug("App", $"ObsidianJournalPath: {_settings.ObsidianJournalPath}");
                Utils.LogDebug("App", $"ObsidianVaultPath: {_settings.ObsidianVaultPath}");
                bool journalExists = System.IO.Directory.Exists(_settings.ObsidianJournalPath);
                bool vaultExists = System.IO.Directory.Exists(_settings.ObsidianVaultPath);
                Utils.LogDebug("App", $"ObsidianJournalPath exists: {journalExists}");
                Utils.LogDebug("App", $"ObsidianVaultPath exists: {vaultExists}");
            }

            // Ensure DebugLogEnabled is set from settings at startup
            SettingsWindow.DebugLogEnabled = _settings.DebugLogEnabled;

            // Log build version if debug log is enabled
            if (SettingsWindow.DebugLogEnabled)
            {
                // Add 3 blank lines for clarity in the log
                string logFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.csv");
                try
                {
                    System.IO.File.AppendAllText(logFile, "\n\n\n");
                }
                catch { }
                var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
                Utils.LogDebug("App", $"Build version: {version}");
            }

            if (!_journalChecked)
            {
                Utils.CheckTodayJournal(_settings);
                _journalChecked = true;
            }

            // Scan vault files/tags and log status
            try
            {
                PomodoroSessionManager.Instance.GetFileListFromVault();
                Utils.LogDebug("App", "Vault scanning executed successfully.");
            }
            catch (Exception ex)
            {
                Utils.LogDebug("App", $"Vault scanning failed: {ex.Message}");
            }

            if (_settings.MiniModeActive || _settings.FirstRun)
            {
                _miniWindow = new MiniWindow(_settings);
                WireMiniWindowEvents();
                
                // Position the mini window if taskbar modification is enabled
                if (_settings.TaskbarModificationEnabled && _taskbarManager != null)
                {
                    // Set position based on the notch position
                    var idealPosition = _taskbarManager.GetIdealMiniWindowPosition(_miniWindow.Width, _miniWindow.Height);
                    _miniWindow.Left = idealPosition.X;
                    _miniWindow.Top = idealPosition.Y;
                }
                
                _miniWindow.Show();
                _settings.MiniModeActive = true;
                _settings.FirstRun = false;
                _settings.Save();
            }
        }
        
        private async void TestTrayIconBlinking()
        {
            try
            {
                // Skip test blinking if debug logging is disabled
                if (!SettingsWindow.DebugLogEnabled)
                    return;
                    
                Utils.LogDebug("App", "Starting tray icon blinking test");
                _trayIcon?.StartBlinking();
                
                // Stop blinking after 3 seconds (reduced from 5)
                await Task.Delay(3000);
                
                Utils.LogDebug("App", "Stopping tray icon blinking test");
                _trayIcon?.StopBlinking();
            }
            catch (Exception ex)
            {
                Utils.LogDebug("App", $"Error in TestTrayIconBlinking: {ex.Message}");
            }
        }

        // Public method to control tray icon blinking
        public void SetTrayIconBlinking(bool blinking)
        {
            Utils.LogDebug("App", $"SetTrayIconBlinking called with blinking={blinking}");
            
            if (_trayIcon != null)
            {
                if (blinking)
                {
                    _trayIcon.StartBlinking();
                }
                else
                {
                    _trayIcon.StopBlinking();
                }
            }
            else
            {
                Utils.LogDebug("App", "TrayIcon is null, cannot set blinking state");
            }
        }

        private void WireMiniWindowEvents()
        {
            if (_miniWindow == null) return;
            // Remove session manager start/stop logic from here
            _miniWindow.TimerStartStopClicked += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine("[App] MiniWindow TimerStartStopClicked");
                // No session manager logic here
            };
            _miniWindow.TimerResetRequested += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine("[App] MiniWindow TimerResetRequested");
                PomodoroSessionManager.Instance.ResetTimer();
            };
            // Subscribe to session manager events to update MiniWindow UI
            PomodoroSessionManager.Instance.Tick += (s, timeLeft) =>
            {
                _miniWindow.Dispatcher.Invoke(() => _miniWindow.SetTimer(PomodoroSessionManager.Instance.TimeLeft));
            };
            PomodoroSessionManager.Instance.Started += (s, e) =>
            {
                _miniWindow.Dispatcher.Invoke(() => _miniWindow.SetTimerRunning(true));
            };
            PomodoroSessionManager.Instance.Stopped += (s, e) =>
            {
                _miniWindow.Dispatcher.Invoke(() => _miniWindow.SetTimerRunning(false));
            };
            PomodoroSessionManager.Instance.Reset += (s, e) =>
            {
                _miniWindow.Dispatcher.Invoke(() => _miniWindow.SetTimer(PomodoroSessionManager.Instance.TimeLeft));
                _miniWindow.Dispatcher.Invoke(() => _miniWindow.SetTimerRunning(false));
            };
        }

        /// <summary>
        /// Enable or disable taskbar modification
        /// </summary>
        public void SetTaskbarModification(bool enabled)
        {
            if (_taskbarManager == null || _settings == null) return;
            
            _taskbarManager.TaskbarModificationEnabled = enabled;
            _settings.TaskbarModificationEnabled = enabled;
            
            if (enabled)
            {
                _taskbarManager.CreateNotch();
                
                // Reposition mini window if it's open
                if (_miniWindow != null && _miniWindow.IsVisible)
                {
                    var idealPosition = _taskbarManager.GetIdealMiniWindowPosition(_miniWindow.Width, _miniWindow.Height);
                    _miniWindow.Left = idealPosition.X;
                    _miniWindow.Top = idealPosition.Y;
                }
            }
            else
            {
                _taskbarManager.RestoreTaskbar();
            }
            
            // Save settings
            _settings.Save();
            
            Utils.LogDebug("App", $"Taskbar modification set to {enabled}");
        }

        /// <summary>
        /// Update the taskbar notch size and position
        /// </summary>
        public void UpdateTaskbarNotch(int width, int height, int position)
        {
            if (_taskbarManager == null || _settings == null) return;
            
            _taskbarManager.TaskbarNotchWidth = width;
            _taskbarManager.TaskbarNotchHeight = height;
            _taskbarManager.TaskbarNotchPosition = position;
            
            _settings.TaskbarNotchWidth = width;
            _settings.TaskbarNotchHeight = height;
            _settings.TaskbarNotchPosition = position;
            
            // If enabled, recreate the notch with new settings
            if (_taskbarManager.TaskbarModificationEnabled)
            {
                _taskbarManager.RestoreTaskbar();
                _taskbarManager.CreateNotch();
                
                // Reposition mini window if it's open
                if (_miniWindow != null && _miniWindow.IsVisible)
                {
                    var idealPosition = _taskbarManager.GetIdealMiniWindowPosition(_miniWindow.Width, _miniWindow.Height);
                    _miniWindow.Left = idealPosition.X;
                    _miniWindow.Top = idealPosition.Y;
                }
            }
            
            // Save settings
            _settings.Save();
            
            Utils.LogDebug("App", $"Taskbar notch updated: width={width}, height={height}, position={position}");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (SettingsWindow.DebugLogEnabled)
            {
                Utils.LogDebug("App", "Application exiting (OnExit)");
            }
            
            // Restore the taskbar if it was modified
            if (_taskbarManager != null && _taskbarManager.TaskbarModificationEnabled)
            {
                _taskbarManager.RestoreTaskbar();
            }
            
            _trayIcon?.Dispose();
            base.OnExit(e);
        }

        public void ExitAppExplicit()
        {
            IsShuttingDown = true;
            if (SettingsWindow.DebugLogEnabled)
            {
                Utils.LogDebug("App", "Application exiting via tray (ExitAppExplicit)");
            }
            
            // Restore the taskbar if it was modified
            if (_taskbarManager != null && _taskbarManager.TaskbarModificationEnabled)
            {
                _taskbarManager.RestoreTaskbar();
            }
            
            // Do NOT save settings here to avoid overwriting with defaults on exit
            this.Shutdown();
        }
    }
}
