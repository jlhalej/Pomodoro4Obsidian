using System.Windows;
using Microsoft.Win32;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using System.Windows.Media;
using System;
using System.Threading.Tasks;
using Squirrel;

namespace PomodoroForObsidian
{
    public partial class SettingsWindow : Window
    {
        private AppSettings _settings;
        private UpdateManager? _updateManager;
        public static bool DebugLogEnabled = false;

        // Minimum dimensions for the MiniWindow, which must be respected by the notch
        private const int MIN_NOTCH_WIDTH = 220;  // Matches MiniWindow.MinWidth
        private const int MIN_NOTCH_HEIGHT = 36;  // Matches MiniWindow.MinHeight

        // Colors for active and inactive menu buttons
        private static readonly SolidColorBrush ActiveButtonBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2d223a"));
        private static readonly SolidColorBrush InactiveButtonBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("Transparent"));
        private static readonly SolidColorBrush ActiveButtonForeground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#d18be9"));
        private static readonly SolidColorBrush InactiveButtonForeground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("White"));

        public SettingsWindow()
        {
            InitializeComponent();
            _settings = AppSettings.Load();

            // Initialize UpdateManager
            _updateManager = new UpdateManager("https://github.com/jlhalej/Pomodoro4Obsidian");

            // Load general settings
            ObsidianJournalPathTextBox.Text = _settings.ObsidianJournalPath;
            ObsidianVaultPathTextBox.Text = _settings.ObsidianVaultPath;
            JournalNoteFormatTextBox.Text = _settings.JournalNoteFormat;
            PomodoroTimerLengthTextBox.Text = _settings.PomodoroTimerLength.ToString();
            MaximumSessionLengthTextBox.Text = _settings.MaximumSessionLength.ToString();
            HeaderTextBox.Text = string.IsNullOrWhiteSpace(_settings.Header) ? "# Pomodoro Sessions" : _settings.Header;

            // Taskbar settings
            TaskbarModificationCheckBox.IsChecked = _settings.TaskbarModificationEnabled;
            TaskbarNotchWidthTextBox.Text = _settings.TaskbarNotchWidth.ToString();
            TaskbarNotchHeightTextBox.Text = _settings.TaskbarNotchHeight.ToString();
            TaskbarNotchPositionTextBox.Text = _settings.TaskbarNotchPosition.ToString();

            // Advanced: Debug log
            DebugLogEnabled = _settings.DebugLogEnabled;
            if (DebugLogCheckBox != null)
                DebugLogCheckBox.IsChecked = DebugLogEnabled;

            // Initialize Updates panel
            InitializeUpdatesPanel();

            // Show General panel by default
            ShowGeneralPanel();
        }

        private void GeneralButton_Click(object sender, RoutedEventArgs e)
        {
            ShowGeneralPanel();
        }

        private void TaskbarButton_Click(object sender, RoutedEventArgs e)
        {
            ShowTaskbarPanel();
        }

        private void ShowGeneralPanel()
        {
            // Update UI
            GeneralPanel.Visibility = Visibility.Visible;
            TaskbarPanel.Visibility = Visibility.Collapsed;
            UpdatesPanel.Visibility = Visibility.Collapsed;

            // Update button styles
            GeneralButton.Background = ActiveButtonBackground;
            GeneralButton.Foreground = ActiveButtonForeground;
            TaskbarButton.Background = InactiveButtonBackground;
            TaskbarButton.Foreground = InactiveButtonForeground;
            UpdatesButton.Background = InactiveButtonBackground;
            UpdatesButton.Foreground = InactiveButtonForeground;
        }

        private void ShowTaskbarPanel()
        {
            // Update UI
            GeneralPanel.Visibility = Visibility.Collapsed;
            TaskbarPanel.Visibility = Visibility.Visible;
            UpdatesPanel.Visibility = Visibility.Collapsed;

            // Update button styles
            GeneralButton.Background = InactiveButtonBackground;
            GeneralButton.Foreground = InactiveButtonForeground;
            TaskbarButton.Background = ActiveButtonBackground;
            TaskbarButton.Foreground = ActiveButtonForeground;
            UpdatesButton.Background = InactiveButtonBackground;
            UpdatesButton.Foreground = InactiveButtonForeground;
        }

        private void BrowseJournalPath_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select the folder containing your Obsidian daily journal.";
                dialog.UseDescriptionForTitle = true;
                dialog.ShowNewFolderButton = false;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    ObsidianJournalPathTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void BrowseVaultPath_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select the root folder of your Obsidian vault.";
                dialog.UseDescriptionForTitle = true;
                dialog.ShowNewFolderButton = false;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    ObsidianVaultPathTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void ApplyTaskbarSettings_Click(object sender, RoutedEventArgs e)
        {
            // Get the values from text boxes
            if (!int.TryParse(TaskbarNotchWidthTextBox?.Text, out int width))
            {
                MessageBox.Show("Please enter a valid number for the notch width.", "Invalid Value", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (width < MIN_NOTCH_WIDTH)
            {
                MessageBox.Show($"The notch width must be at least {MIN_NOTCH_WIDTH} pixels to accommodate the mini window.",
                                "Notch Too Small", MessageBoxButton.OK, MessageBoxImage.Warning);
                TaskbarNotchWidthTextBox.Text = MIN_NOTCH_WIDTH.ToString();
                return;
            }

            if (width > 2000)
            {
                MessageBox.Show("The notch width cannot exceed 2000 pixels.", "Invalid Value", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(TaskbarNotchHeightTextBox?.Text, out int height))
            {
                MessageBox.Show("Please enter a valid number for the notch height.", "Invalid Value", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (height < MIN_NOTCH_HEIGHT)
            {
                MessageBox.Show($"The notch height must be at least {MIN_NOTCH_HEIGHT} pixels to accommodate the mini window.",
                               "Notch Too Small", MessageBoxButton.OK, MessageBoxImage.Warning);
                TaskbarNotchHeightTextBox.Text = MIN_NOTCH_HEIGHT.ToString();
                return;
            }

            if (height > 100)
            {
                MessageBox.Show("The notch height cannot exceed 100 pixels.", "Invalid Value", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(TaskbarNotchPositionTextBox?.Text, out int position) || position < -1000 || position > 1000)
            {
                MessageBox.Show("Notch position must be between -1000 and 1000.", "Invalid Value", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Update the settings
            _settings.TaskbarNotchWidth = width;
            _settings.TaskbarNotchHeight = height;
            _settings.TaskbarNotchPosition = position;
            _settings.TaskbarModificationEnabled = TaskbarModificationCheckBox?.IsChecked == true;

            // Apply the settings
            var app = System.Windows.Application.Current as App;
            if (app != null)
            {
                app.SetTaskbarModification(_settings.TaskbarModificationEnabled);
                app.UpdateTaskbarNotch(width, height, position);

                // Resize and reposition the mini window to match the notch dimensions and position
                ResizeMiniWindowToMatchNotch(width, height, position);
            }

            // Save settings
            _settings.Save();

            MessageBox.Show("Taskbar settings applied successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ResizeMiniWindowToMatchNotch(int width, int height, int position = 0)
        {
            var app = System.Windows.Application.Current as App;
            if (app == null) return;

            // Get the TaskbarManager using reflection
            var taskbarManagerField = app.GetType().GetField("_taskbarManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (taskbarManagerField == null) return;

            var taskbarManager = taskbarManagerField.GetValue(app) as TaskbarManager;
            if (taskbarManager == null) return;

            // Get the mini window using reflection
            var miniWindowField = app.GetType().GetField("_miniWindow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (miniWindowField == null) return;

            var miniWindow = miniWindowField.GetValue(app) as MiniWindow;
            if (miniWindow == null || !miniWindow.IsVisible) return;

            // First resize the mini window to match the notch dimensions
            Utils.LogDebug("SettingsWindow", $"Resizing mini window to match notch: {width}x{height}");

            miniWindow.Dispatcher.Invoke(() =>
            {
                // First resize the window
                miniWindow.Width = width;
                miniWindow.Height = height;

                // Update the settings to save the new dimensions
                _settings.MiniWindowWidth = width;
                _settings.MiniWindowHeight = height;

                // Then get the ideal position for the window based on the new size
                var idealPosition = taskbarManager.GetIdealMiniWindowPosition(width, height);

                // Now reposition the window to align with the notch
                Utils.LogDebug("SettingsWindow", $"Repositioning mini window to: X={idealPosition.X}, Y={idealPosition.Y}");
                miniWindow.Left = idealPosition.X;
                miniWindow.Top = idealPosition.Y;

                // Update the settings to save the new position
                _settings.MiniWindowLeft = idealPosition.X;
                _settings.MiniWindowTop = idealPosition.Y;
            });
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Save general settings
            _settings.ObsidianJournalPath = ObsidianJournalPathTextBox.Text;
            _settings.ObsidianVaultPath = ObsidianVaultPathTextBox.Text;
            _settings.JournalNoteFormat = JournalNoteFormatTextBox.Text;
            if (int.TryParse(PomodoroTimerLengthTextBox.Text, out int length) && length > 0)
            {
                _settings.PomodoroTimerLength = length;
            }
            if (int.TryParse(MaximumSessionLengthTextBox.Text, out int maxLength) && maxLength >= 1 && maxLength <= 2400)
            {
                _settings.MaximumSessionLength = maxLength;
            }
            _settings.Header = string.IsNullOrWhiteSpace(HeaderTextBox.Text) ? "# Pomodoro Sessions" : HeaderTextBox.Text;

            // Save taskbar settings
            if (int.TryParse(TaskbarNotchWidthTextBox?.Text, out int width))
            {
                // Ensure minimum width requirement
                if (width < MIN_NOTCH_WIDTH)
                {
                    MessageBox.Show($"The notch width must be at least {MIN_NOTCH_WIDTH} pixels to accommodate the mini window.",
                                  "Notch Too Small", MessageBoxButton.OK, MessageBoxImage.Warning);
                    TaskbarNotchWidthTextBox.Text = MIN_NOTCH_WIDTH.ToString();
                    return;
                }
                else if (width <= 2000)
                {
                    _settings.TaskbarNotchWidth = width;
                }
            }

            if (int.TryParse(TaskbarNotchHeightTextBox?.Text, out int height))
            {
                // Ensure minimum height requirement
                if (height < MIN_NOTCH_HEIGHT)
                {
                    MessageBox.Show($"The notch height must be at least {MIN_NOTCH_HEIGHT} pixels to accommodate the mini window.",
                                  "Notch Too Small", MessageBoxButton.OK, MessageBoxImage.Warning);
                    TaskbarNotchHeightTextBox.Text = MIN_NOTCH_HEIGHT.ToString();
                    return;
                }
                else if (height <= 100)
                {
                    _settings.TaskbarNotchHeight = height;
                }
            }

            if (int.TryParse(TaskbarNotchPositionTextBox?.Text, out int position) && position >= -1000 && position <= 1000)
            {
                _settings.TaskbarNotchPosition = position;
            }

            _settings.TaskbarModificationEnabled = TaskbarModificationCheckBox?.IsChecked == true;

            // Advanced: Debug log
            if (DebugLogCheckBox != null)
            {
                _settings.DebugLogEnabled = DebugLogCheckBox.IsChecked == true;
                DebugLogEnabled = _settings.DebugLogEnabled;
            }

            // Save auto-update settings
            _settings.AutoCheckForUpdates = AutoUpdateCheckBox?.IsChecked == true;

            // Apply taskbar settings if changed
            var app = System.Windows.Application.Current as App;
            if (app != null)
            {
                app.SetTaskbarModification(_settings.TaskbarModificationEnabled);
                app.UpdateTaskbarNotch(_settings.TaskbarNotchWidth, _settings.TaskbarNotchHeight, _settings.TaskbarNotchPosition);

                // If we're in the taskbar panel, also resize the mini window to match the notch
                if (TaskbarPanel.IsVisible)
                {
                    ResizeMiniWindowToMatchNotch(_settings.TaskbarNotchWidth, _settings.TaskbarNotchHeight, _settings.TaskbarNotchPosition);
                }
            }

            System.Diagnostics.Debug.WriteLine("saving settings in SaveButton_Click");
            _settings.Save();
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void HeaderTextBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var tb = sender as System.Windows.Controls.TextBox;
            if (tb != null)
            {
                tb.SelectAll();
                e.Handled = true;
            }
        }

        #region Updates Panel Event Handlers

        private void UpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            ShowUpdatesPanel();
        }

        private async void CheckForUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            await CheckForUpdatesAsync();
        }

        private async void DownloadUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            await DownloadAndApplyUpdateAsync();
        }

        private void SkipUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateInfoPanel.Visibility = Visibility.Collapsed;
            UpdateCheckStatusLabel.Text = "Update skipped";
        }

        #endregion

        #region Updates Panel Methods

        private void InitializeUpdatesPanel()
        {
            if (_updateManager == null) return;

            CurrentVersionLabel.Text = _updateManager.GetCurrentVersion().ToString();
            InstallationTypeLabel.Text = "Installer";
            UpdateStatusLabel.Text = "Ready to check";

            // Load auto-update setting from AppSettings
            AutoUpdateCheckBox.IsChecked = _settings.AutoCheckForUpdates;
        }

        private void ShowUpdatesPanel()
        {
            // Update UI
            GeneralPanel.Visibility = Visibility.Collapsed;
            TaskbarPanel.Visibility = Visibility.Collapsed;
            UpdatesPanel.Visibility = Visibility.Visible;

            // Update button styles
            GeneralButton.Background = InactiveButtonBackground;
            GeneralButton.Foreground = InactiveButtonForeground;
            TaskbarButton.Background = InactiveButtonBackground;
            TaskbarButton.Foreground = InactiveButtonForeground;
            UpdatesButton.Background = ActiveButtonBackground;
            UpdatesButton.Foreground = ActiveButtonForeground;
        }

        private async Task CheckForUpdatesAsync()
        {
            if (_updateManager == null) return;

            CheckForUpdatesButton.IsEnabled = false;
            UpdateCheckStatusLabel.Text = "Checking for updates...";

            try
            {
                var updateInfo = await _updateManager.CheckForUpdatesAsync();
                if (updateInfo != null)
                {
                    // Update available
                    NewVersionLabel.Text = $"Version: {updateInfo.FutureReleaseEntry?.Version}";
                    UpdateNotesLabel.Text = "A new version is available for download.";
                    UpdateInfoPanel.Visibility = Visibility.Visible;
                    UpdateCheckStatusLabel.Text = "Update available!";
                }
                else
                {
                    // No updates
                    UpdateInfoPanel.Visibility = Visibility.Collapsed;
                    UpdateCheckStatusLabel.Text = "You have the latest version.";
                }
            }
            catch (Exception ex)
            {
                UpdateCheckStatusLabel.Text = $"Error checking for updates: {ex.Message}";
                Utils.LogDebug("SettingsWindow", $"Update check failed: {ex.Message}");
            }
            finally
            {
                CheckForUpdatesButton.IsEnabled = true;
            }
        }

        private async Task DownloadAndApplyUpdateAsync()
        {
            if (_updateManager == null) return;

            DownloadUpdateButton.IsEnabled = false;
            UpdateProgressPanel.Visibility = Visibility.Visible;

            try
            {
                _updateManager.UpdateProgress += OnUpdateProgress;

                var success = await _updateManager.DownloadAndApplyUpdatesAsync();
                if (success)
                {
                    UpdateProgressLabel.Text = "Update completed! Restart required.";
                    MessageBox.Show("Update downloaded successfully! The application will restart to apply the update.",
                                  "Update Ready", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    UpdateProgressLabel.Text = "Update failed.";
                    MessageBox.Show("Failed to download or apply the update. Please try again later.",
                                  "Update Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                UpdateProgressLabel.Text = $"Update failed: {ex.Message}";
                Utils.LogDebug("SettingsWindow", $"Update download failed: {ex.Message}");
            }
            finally
            {
                _updateManager.UpdateProgress -= OnUpdateProgress;
                DownloadUpdateButton.IsEnabled = true;
                UpdateProgressPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void OnUpdateProgress(object? sender, int progress)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateProgressBar.Value = progress;
                UpdateProgressLabel.Text = $"{progress}%";
            });
        }

        #endregion
    }
}
