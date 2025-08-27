using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

namespace PomodoroForObsidian
{
    public class AppSettings
    {
        public string ObsidianJournalPath { get; set; } = string.Empty;
        public string JournalNoteFormat { get; set; } = "YYYY-MM-DD";
        public int PomodoroTimerLength { get; set; } = 25;
        public bool MiniModeActive { get; set; } = true; // Changed to true so mini window is shown by default
        // Removed MainWindowVisible, MainWindowLeft, MainWindowTop
        public double? MiniWindowLeft { get; set; } = null;
        public double? MiniWindowTop { get; set; } = null;
        public double? MiniWindowWidth { get; set; } = null;
        public double? MiniWindowHeight { get; set; } = null;
        public bool FirstRun { get; set; } = true;
        public string? CurrentSessionTimestamp { get; set; }
        public DateTime? CurrentSessionStartTime { get; set; }
        public string? CurrentSessionInputField { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        public string Header { get; set; } = "# Pomodoro Sessions";
        public bool DebugLogEnabled { get; set; } = false;
        public int MaximumSessionLength { get; set; } = 120;
        public string ObsidianVaultPath { get; set; } = string.Empty;

        // Taskbar management settings
        public bool TaskbarModificationEnabled { get; set; } = false;
        public int TaskbarNotchWidth { get; set; } = 340;
        public int TaskbarNotchHeight { get; set; } = 36;
        public int TaskbarNotchPosition { get; set; } = 0;
        public int TaskbarCornerRadius { get; set; } = 6;

        // Auto-update settings
        public bool AutoCheckForUpdates { get; set; } = true;
        public DateTime? LastUpdateCheck { get; set; } = null;

        private static readonly string SettingsFileName = "settings.json";
        private static readonly string SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFileName);

        public static AppSettings Load()
        {
            if (File.Exists(SettingsFilePath))
            {
                // Only log if you want to debug settings file location, not every load
                // Utils.LogDebug("AppSettings.Load", $"settings.json found at '{SettingsFilePath}'");
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                settings.FirstRun = false;
                return settings;
            }
            else
            {
                Utils.LogDebug("AppSettings.Load", $"settings.json NOT found at '{SettingsFilePath}', creating with defaults.");
                var settings = new AppSettings();
                settings.Save();
                Utils.LogDebug("AppSettings.Load", $"settings.json created at '{SettingsFilePath}' with default values.");
                return settings;
            }
        }

        public static AppSettings Load(out bool isFirstRun)
        {
            if (File.Exists(SettingsFilePath))
            {
                // Only log if you want to debug settings file location, not every load
                // Utils.LogDebug("AppSettings.Load", $"settings.json found at '{SettingsFilePath}'");
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                settings.FirstRun = false;
                isFirstRun = false;
                return settings;
            }
            else
            {
                Utils.LogDebug("AppSettings.Load", $"settings.json NOT found at '{SettingsFilePath}', creating with defaults.");
                var settings = new AppSettings();
                settings.Save();
                Utils.LogDebug("AppSettings.Load", $"settings.json created at '{SettingsFilePath}' with default values.");
                isFirstRun = true;
                return settings;
            }
        }

        public void Save()
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
    }
}
