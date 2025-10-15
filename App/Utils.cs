using System;
using System.Globalization;
using System.IO;

namespace PomodoroForObsidian
{
    public static class Utils
    {
        public static void CheckTodayJournal(AppSettings settings)
        {
            string today = DateTime.Now.ToString(settings.JournalNoteFormat.Replace("YYYY", "yyyy").Replace("DD", "dd"), CultureInfo.InvariantCulture);
            string journalFile = Path.Combine(settings.ObsidianJournalPath, today + ".md");
            if (File.Exists(journalFile))
            {
                BubbleNotification($"Journal already exists:\n{today}.md");
            }
            else
            {
                MessageBoxNotification($"The Journal does not exist, Open Obsidian and create your note:\n{journalFile}");
            }
        }

        public static void BubbleNotification(string message)
        {
            // Show a balloon notification from the tray icon if available
            var app = System.Windows.Application.Current as PomodoroForObsidian.App;
            var trayIconField = app?.GetType().GetField("_trayIcon", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var trayIcon = trayIconField?.GetValue(app) as TrayIcon;
            if (trayIcon != null)
            {
                trayIcon.ShowBalloon(message);
            }
            else
            {
                System.Windows.MessageBox.Show(message, "PomodoroForObsidian", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }

        public static void MessageBoxNotification(string message)
        {
            System.Windows.MessageBox.Show(message, "PomodoroForObsidian", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }

        // Log debug information to debug.csv if enabled in settings
        public static void LogDebug(string method, string logMessage)
        {
            // Use the static DebugLogEnabled from SettingsWindow
            if (!SettingsWindow.DebugLogEnabled)
                return;
            string logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.csv");
            string timestamp = DateTime.Now.ToString("yyyyMMdd HH:mm:ss");
            string line = $"{timestamp}|{method}|{logMessage}";
            try
            {
                File.AppendAllText(logFile, line + Environment.NewLine);
            }
            catch (Exception)
            {
                // Optionally, ignore or show a message
            }
        }
    }
}
