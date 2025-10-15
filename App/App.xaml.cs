using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Squirrel;
using PomodoroForObsidian.Managers;
using PomodoroForObsidian.Persistence;
using PomodoroForObsidian.Interfaces;

namespace PomodoroForObsidian
{
    public partial class App : Application
    {
        private TrayIcon? _trayIcon;
        private AppSettings? _settings;
        private MiniWindow? _miniWindow;
        private TaskbarManager? _taskbarManager;
        private UpdateManager? _updateManager;

        private ITaskHistoryRepository _taskHistoryRepository = new JsonTaskHistoryRepository();
        private AutoCompleteManager? _autoCompleteManager;
        private PomodoroSessionManager? _pomodoroSessionManager;

        public static bool IsShuttingDown { get; private set; } = false;

        protected override async void OnStartup(StartupEventArgs e)
        {
            SquirrelAwareApp.HandleEvents(
                onInitialInstall: OnAppInstall,
                onAppUpdate: OnAppUpdate,
                onAppUninstall: OnAppUninstall);

            base.OnStartup(e);
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            bool isFirstRun;
            _settings = AppSettings.Load(out isFirstRun);

            await _taskHistoryRepository.InitializeAsync();
            await _taskHistoryRepository.CleanupOldEntriesAsync();
            _autoCompleteManager = new AutoCompleteManager(_taskHistoryRepository);
            _pomodoroSessionManager = new PomodoroSessionManager(_taskHistoryRepository);

            _trayIcon = new TrayIcon(_pomodoroSessionManager, _settings, _autoCompleteManager);

            _taskbarManager = new TaskbarManager();

            if (_settings != null && _taskbarManager != null)
            {
                _taskbarManager.TaskbarModificationEnabled = _settings.TaskbarModificationEnabled;
                _taskbarManager.TaskbarNotchWidth = _settings.TaskbarNotchWidth;
                _taskbarManager.TaskbarNotchHeight = _settings.TaskbarNotchHeight;
                _taskbarManager.TaskbarNotchPosition = _settings.TaskbarNotchPosition;
                _taskbarManager.CornerRadius = _settings.TaskbarCornerRadius;

                if (_settings.TaskbarModificationEnabled)
                {
                    Utils.LogDebug("App", "Taskbar modification enabled, creating notch");
                    _taskbarManager.CreateNotch();
                }
            }

            if (isFirstRun)
            {
                Utils.LogDebug("App", "First run detected: opening preferences window after creating settings.json.");
                MessageBox.Show(
                    "It seems this is the first time you run Pomodoro for Obsidian. The settings page will be opened now, so you can indicate the path of your Journal",
                    "Welcome to Pomodoro for Obsidian",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                var settingsWindow = new SettingsWindow();
                settingsWindow.ShowDialog();
                _settings = AppSettings.Load();
            }

            bool journalSet = !string.IsNullOrWhiteSpace(_settings?.ObsidianJournalPath);
            bool vaultSet = !string.IsNullOrWhiteSpace(_settings?.ObsidianVaultPath);
            if (!journalSet || !vaultSet)
            {
                Utils.MessageBoxNotification("Please set both the Obsidian Journal Path and Vault Path in Preferences.");
                var settingsWindow = new SettingsWindow();
                settingsWindow.ShowDialog();
                _settings = AppSettings.Load();
            }

            SettingsWindow.DebugLogEnabled = _settings?.DebugLogEnabled ?? false;

            if (SettingsWindow.DebugLogEnabled)
            {
                string logFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.csv");
                try { System.IO.File.AppendAllText(logFile, "\n\n\n"); }
                catch { }
                var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
                Utils.LogDebug("App", $"Build version: {version}");
            }

            _pomodoroSessionManager?.GetFileListFromVault();

            if (_settings?.MiniModeActive == true || _settings?.FirstRun == true)
            {
                if (_settings != null && _autoCompleteManager != null && _pomodoroSessionManager != null)
                {
                    _miniWindow = new MiniWindow(_settings, _autoCompleteManager, _pomodoroSessionManager);
                    WireMiniWindowEvents();

                    if (_settings.TaskbarModificationEnabled && _taskbarManager != null && _miniWindow != null)
                    {
                        var idealPosition = _taskbarManager.GetIdealMiniWindowPosition(_miniWindow.Width, _miniWindow.Height);
                        _miniWindow.Left = idealPosition.X;
                        _miniWindow.Top = idealPosition.Y;
                    }

                    _miniWindow.Show();
                    if (_settings != null)
                    {
                        _settings.MiniModeActive = true;
                        _settings.FirstRun = false;
                        _settings.Save();
                    }
                }
            }

            InitializeAutoUpdateAsync();
        }

        public MiniWindow? GetMiniWindow() => _miniWindow;

        public void SetMiniWindow(MiniWindow window)
        {
            _miniWindow = window;
            WireMiniWindowEvents();
        }

        private void WireMiniWindowEvents()
        {
            if (_miniWindow == null) return;

            _miniWindow.TimerStartStopClicked += (s, e) => { };
            _miniWindow.TimerResetRequested += (s, e) => _pomodoroSessionManager?.ResetTimer();

            if (_pomodoroSessionManager != null)
            {
                _pomodoroSessionManager.Tick += (s, timeLeft) => _miniWindow.Dispatcher.Invoke(() => _miniWindow.SetTimer(_pomodoroSessionManager.TimeLeft));
                _pomodoroSessionManager.Started += (s, e) => _miniWindow.Dispatcher.Invoke(() => _miniWindow.SetTimerRunning(true));
                _pomodoroSessionManager.Stopped += (s, e) => _miniWindow.Dispatcher.Invoke(() => _miniWindow.SetTimerRunning(false));
                _pomodoroSessionManager.Reset += (s, e) =>
                {
                    _miniWindow.Dispatcher.Invoke(() => _miniWindow.SetTimer(_pomodoroSessionManager.TimeLeft));
                    _miniWindow.Dispatcher.Invoke(() => _miniWindow.SetTimerRunning(false));
                };
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
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
            if (_taskbarManager != null && _taskbarManager.TaskbarModificationEnabled)
            {
                _taskbarManager.RestoreTaskbar();
            }
            this.Shutdown();
        }

        private async void InitializeAutoUpdateAsync()
        {
            try
            {
                _updateManager = new UpdateManager("https://github.com/jlhalej/Pomodoro4Obsidian");
                if (_settings?.AutoCheckForUpdates == true)
                {
                    var lastCheck = _settings.LastUpdateCheck;
                    var shouldCheck = lastCheck == null || (DateTime.Now - lastCheck.Value).TotalHours >= 24;
                    if (shouldCheck)
                    {
                        await CheckForUpdatesInBackgroundAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.LogDebug("App", $"Failed to initialize auto-update: {ex.Message}");
            }
        }

        private async Task CheckForUpdatesInBackgroundAsync()
        {
            try
            {
                if (_updateManager == null || _settings == null) return;
                var updateInfo = await _updateManager.CheckForUpdatesAsync();
                _settings.LastUpdateCheck = DateTime.Now;
                _settings.Save();
                if (updateInfo != null && updateInfo.ReleasesToApply.Any())
                {
                    var result = MessageBox.Show(
                        $"A new version ({updateInfo.FutureReleaseEntry?.Version}) is available!\n\nWould you like to download and install it now?",
                        "Update Available",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);
                    if (result == MessageBoxResult.Yes)
                    {
                        await DownloadAndApplyUpdateAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.LogDebug("App", $"Background update check failed: {ex.Message}");
            }
        }

        private async Task DownloadAndApplyUpdateAsync()
        {
            try
            {
                if (_updateManager == null) return;
                var success = await _updateManager.DownloadAndApplyUpdatesAsync();
                if (success)
                {
                    MessageBox.Show(
                        "Update downloaded successfully! The application will restart to complete the installation.",
                        "Update Ready",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                    var fileName = currentProcess.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        System.Diagnostics.Process.Start(fileName);
                    }
                    ExitAppExplicit();
                }
                else
                {
                    MessageBox.Show("Failed to download or apply the update. Please try again later.",
                                  "Update Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Update failed: {ex.Message}", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void OnAppInstall(SemanticVersion version, IAppTools tools)
        {
            tools.CreateShortcutForThisExe(ShortcutLocation.StartMenu | ShortcutLocation.Desktop);
        }

        private static void OnAppUpdate(SemanticVersion version, IAppTools tools)
        {
            tools.CreateShortcutForThisExe(ShortcutLocation.StartMenu | ShortcutLocation.Desktop);
        }

        private static void OnAppUninstall(SemanticVersion version, IAppTools tools)
        {
            tools.RemoveShortcutForThisExe(ShortcutLocation.StartMenu | ShortcutLocation.Desktop);
        }
    }
}
