using System;
using System.Windows.Threading;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;
using PomodoroForObsidian.Interfaces;

namespace PomodoroForObsidian
{
    public class PomodoroSessionManager
    {
        private readonly ITaskHistoryRepository _taskHistoryRepository;
        private DispatcherTimer _timer = new DispatcherTimer();
        private DispatcherTimer _updateTimer = new DispatcherTimer();
        private TimeSpan _timeLeft;
        private bool _isRunning;
        private int _pomodoroLength;

        private bool _reverseCountdown = false;
        private TimeSpan _reverseTime = TimeSpan.Zero;
        public event EventHandler? ReverseCountdownStarted;

        public event EventHandler<TimeSpan>? Tick;
        public event EventHandler? Started;
        public event EventHandler? Stopped;
        public event EventHandler? Reset;

        private string? _lastTask;
        private string? _lastProject;

        private DispatcherTimer? _negativeTimer;
        private TimeSpan _negativeTimeElapsed = TimeSpan.Zero;
        public event EventHandler<TimeSpan>? NegativeTimerTick;

        public PomodoroSessionManager(ITaskHistoryRepository taskHistoryRepository)
        {
            _taskHistoryRepository = taskHistoryRepository;
            _pomodoroLength = 25;
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _updateTimer.Interval = TimeSpan.FromMinutes(3);
            _updateTimer.Tick += _updateTimer_Tick;
            ResetTimer();
        }

        public void SetPomodoroLength(int minutes)
        {
            _pomodoroLength = minutes;
            if (!_isRunning)
                ResetTimer();
        }

        public void Start(string? task = null, string? project = null)
        {
            Utils.LogDebug(nameof(Start), $"Start called. _isRunning={_isRunning}, task={task}, project={project}");
            if (!_isRunning)
            {
                var settings = AppSettings.Load();

                // ALWAYS check if today's journal exists before starting (new or resumed session)
                string journalFilePath;
                bool journalExists = Utils.DoesTodayJournalExist(settings, out journalFilePath);

                if (!journalExists)
                {
                    // Only notify once per day
                    bool shouldNotify = !settings.LastJournalCheckDate.HasValue ||
                                      settings.LastJournalCheckDate.Value.Date != DateTime.Now.Date;

                    if (shouldNotify)
                    {
                        Utils.BubbleNotification($"Cannot start timer: Today's journal does not exist.\n\nPlease create it in Obsidian first:\n{journalFilePath}");
                        settings.LastJournalCheckDate = DateTime.Now;
                        settings.Save();
                    }

                    Utils.LogDebug(nameof(Start), $"Journal file does not exist: {journalFilePath}. Timer start blocked.");
                    return; // Block timer start
                }

                // Always create new session timestamp
                SetPomodoroLength(settings.PomodoroTimerLength);
                var newTimestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
                Utils.LogDebug(nameof(Start), $"Assigning new session timestamp: {newTimestamp}");
                settings.CurrentSessionTimestamp = newTimestamp;
                settings.CurrentSessionStartTime = DateTime.Now;
                settings.Save();
                Utils.LogDebug(nameof(Start), $"Saved session timestamp to settings: {settings.CurrentSessionTimestamp}");
                LogRunningSessionToObsidian(true);
                _updateTimer.Start();
                _lastTask = task;
                _lastProject = project;
                _isRunning = true;
                _timer.Start();
                Started?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Stop()
        {
            Utils.LogDebug(nameof(Stop), $"Stop called. _isRunning={_isRunning}");
            if (_isRunning)
            {
                _isRunning = false;
                _timer.Stop();
                _updateTimer.Stop();
                StopNegativeTimer();
                Utils.LogDebug(nameof(Stop), "Calling LogStoppedSessionToObsidian");
                LogStoppedSessionToObsidian();

                if (!string.IsNullOrEmpty(_lastTask))
                {
                    _taskHistoryRepository.AddOrUpdateTaskAsync(_lastTask);
                }

                // Reset timer in mini window and reset session timestamp/start time
                ResetTimer();
                ResetSessionTimestamp();
                Stopped?.Invoke(this, EventArgs.Empty);
            }
        }

        public void ResetTimer()
        {
            _timeLeft = TimeSpan.FromMinutes(_pomodoroLength);
            _isRunning = false;
            _timer.Stop();
            _reverseCountdown = false;
            _reverseTime = TimeSpan.Zero;
            Reset?.Invoke(this, EventArgs.Empty);
            Tick?.Invoke(this, _timeLeft);
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            var settings = AppSettings.Load();
            if (!_reverseCountdown)
            {
                if (_timeLeft.TotalSeconds > 0)
                {
                    _timeLeft = _timeLeft.Add(TimeSpan.FromSeconds(-1));
                    Tick?.Invoke(this, _timeLeft);
                    // Check for maximum session length
                    if (settings.MaximumSessionLength > 0 && (DateTime.Now - (settings.CurrentSessionStartTime ?? DateTime.Now)).TotalMinutes >= settings.MaximumSessionLength)
                    {
                        Utils.LogDebug(nameof(Timer_Tick), "Maximum session length reached. Stopping timer.");
                        Stop();
                        return;
                    }
                }
                else
                {
                    // Timer reached 0: notify and start negative countdown
                    Utils.BubbleNotification("Pomodoro completed.");
                    _reverseCountdown = true;
                    _reverseTime = TimeSpan.Zero;
                    ReverseCountdownStarted?.Invoke(this, EventArgs.Empty);
                    Tick?.Invoke(this, TimeSpan.Zero); // Show 00:00 at transition
                    if (!_timer.IsEnabled)
                        _timer.Start();
                    System.Diagnostics.Debug.WriteLine("[PomodoroSessionManager] Reverse countdown started.");
                    StartNegativeTimer();
                }
            }
            else
            {
                // Reverse countdown: keep timer running, but NegativeTimer handles display
            }
        }

        private void StartNegativeTimer()
        {
            if (_negativeTimer == null)
            {
                _negativeTimer = new DispatcherTimer();
                _negativeTimer.Interval = TimeSpan.FromSeconds(1);
                _negativeTimer.Tick += (s, e) =>
                {
                    _negativeTimeElapsed = _negativeTimeElapsed.Add(TimeSpan.FromSeconds(1));
                    NegativeTimerTick?.Invoke(this, _negativeTimeElapsed);
                };
            }
            _negativeTimeElapsed = TimeSpan.Zero;
            _negativeTimer.Start();
        }

        private void StopNegativeTimer()
        {
            if (_negativeTimer != null)
                _negativeTimer.Stop();
            _negativeTimeElapsed = TimeSpan.Zero;
        }

        private void _updateTimer_Tick(object? sender, EventArgs e)
        {
            LogRunningSessionToObsidian();
        }

        private void LogRunningSessionToObsidian(bool isInitialLog = false)
        {
            Utils.LogDebug(nameof(LogRunningSessionToObsidian), "Begin logging session to journal");
            var settings = AppSettings.Load();
            string timestamp = settings.CurrentSessionTimestamp ?? DateTime.Now.ToString("yyyyMMddHHmmssfff");
            DateTime start = settings.CurrentSessionStartTime ?? DateTime.Now;
            DateTime end = isInitialLog ? start.AddMinutes(3) : DateTime.Now;
            if (start.Hour == end.Hour && start.Minute == end.Minute)
            {
                Utils.LogDebug(nameof(LogRunningSessionToObsidian), "Start and end time are in the same minute, not logging session.");
                return;
            }
            if (start == end)
            {
                Utils.LogDebug(nameof(LogRunningSessionToObsidian), "Start and end time are the same, not logging session.");
                return;
            }
            string today = DateTime.Now.ToString(settings.JournalNoteFormat.Replace("YYYY", "yyyy").Replace("DD", "dd"), CultureInfo.InvariantCulture);
            string journalFile = Path.Combine(settings.ObsidianJournalPath, today + ".md");
            string header = string.IsNullOrWhiteSpace(settings.Header) ? "# Pomodoro Sessions" : settings.Header;
            string task = settings.CurrentSessionInputField ?? string.Empty;
            string project = string.Empty;
            string entry = $"- {start:HH:mm} - {end:HH:mm} {task} {project} {timestamp}";

            Utils.LogDebug(nameof(LogRunningSessionToObsidian), $"Preparing to log session. Timestamp: {timestamp}, Start: {start:HH:mm}, End: {end:HH:mm}, Task: '{task}', JournalFile: {journalFile}");
            Utils.LogDebug(nameof(LogRunningSessionToObsidian), $"Looking for header: '{header}' in journal file: {journalFile}");

            if (!File.Exists(journalFile))
            {
                using (var sw = new StreamWriter(journalFile, false))
                {
                    sw.WriteLine(header);
                    sw.WriteLine(entry);
                }
                Utils.LogDebug(nameof(LogRunningSessionToObsidian), "Journal file did not exist, created and wrote header/entry.");
                return;
            }

            // Efficient update: only update or append the entry, not rewrite the whole file if not needed
            var linesList = new System.Collections.Generic.List<string>(File.ReadAllLines(journalFile));
            bool foundHeader = false;
            int headerIndex = -1;
            int foundLine = -1;
            for (int i = 0; i < linesList.Count; i++)
            {
                if (linesList[i].Trim() == header)
                {
                    foundHeader = true;
                    headerIndex = i;
                }
                if (linesList[i].Contains(timestamp))
                {
                    foundLine = i;
                }
            }

            if (foundLine != -1)
            {
                // Update the line in place
                linesList[foundLine] = entry;
                File.WriteAllLines(journalFile, linesList);
                Utils.LogDebug(nameof(LogRunningSessionToObsidian), $"Updated session entry at line {foundLine} in journal file: {journalFile}");
            }
            else if (foundHeader)
            {
                // Find the end of the header section (either next header or end of file)
                int insertIndex = linesList.Count; // Default to end of file
                for (int i = headerIndex + 1; i < linesList.Count; i++)
                {
                    string line = linesList[i].Trim();
                    // Check if this line is another header (starts with #)
                    if (line.StartsWith("#") && line.Length > 1)
                    {
                        insertIndex = i;
                        break;
                    }
                }

                linesList.Insert(insertIndex, entry);
                File.WriteAllLines(journalFile, linesList);
                Utils.LogDebug(nameof(LogRunningSessionToObsidian), $"Appended new session entry at end of section at line {insertIndex} in journal file: {journalFile}");
            }
            else
            {
                // Append at end
                linesList.Add("");
                linesList.Add(header);
                linesList.Add(entry);
                File.WriteAllLines(journalFile, linesList);
                Utils.LogDebug(nameof(LogRunningSessionToObsidian), $"Header not found, appended header and new session entry at end of journal file: {journalFile}");
            }
        }

        private void LogStoppedSessionToObsidian()
        {
            Utils.LogDebug(nameof(LogStoppedSessionToObsidian), "Begin logging stopped session to journal");
            var settings = AppSettings.Load();
            DateTime start = settings.CurrentSessionStartTime ?? DateTime.Now;
            DateTime end = DateTime.Now;
            if (start.Hour == end.Hour && start.Minute == end.Minute)
            {
                Utils.LogDebug(nameof(LogStoppedSessionToObsidian), "Start and end time are in the same minute, not logging session.");
                return;
            }
            if (start == end)
            {
                Utils.LogDebug(nameof(LogStoppedSessionToObsidian), "Start and end time are the same, not logging session.");
                return;
            }
            string today = DateTime.Now.ToString(settings.JournalNoteFormat.Replace("YYYY", "yyyy").Replace("DD", "dd"), CultureInfo.InvariantCulture);
            string journalFile = Path.Combine(settings.ObsidianJournalPath, today + ".md");
            string header = string.IsNullOrWhiteSpace(settings.Header) ? "# Pomodoro Sessions" : settings.Header;
            string task = settings.CurrentSessionInputField ?? string.Empty;
            string project = string.Empty;
            string timestamp = settings.CurrentSessionTimestamp ?? string.Empty;
            string entry = $"- {start:HH:mm} - {end:HH:mm} {task} {project}";

            Utils.LogDebug(nameof(LogStoppedSessionToObsidian), $"Preparing to log stopped session. Start: {start:HH:mm}, End: {end:HH:mm}, Task: '{task}', JournalFile: {journalFile}, Timestamp: {timestamp}");
            Utils.LogDebug(nameof(LogStoppedSessionToObsidian), $"Looking for header: '{header}' in journal file: {journalFile}");

            if (!File.Exists(journalFile))
            {
                using (var sw = new StreamWriter(journalFile, false))
                {
                    sw.WriteLine(header);
                    sw.WriteLine(entry);
                }
                Utils.LogDebug(nameof(LogStoppedSessionToObsidian), "Journal file did not exist, created and wrote header/entry.");
                return;
            }

            var linesList = new System.Collections.Generic.List<string>(File.ReadAllLines(journalFile));
            int foundLine = -1;
            if (!string.IsNullOrEmpty(timestamp))
            {
                for (int i = 0; i < linesList.Count; i++)
                {
                    if (linesList[i].Contains(timestamp))
                    {
                        foundLine = i;
                        break;
                    }
                }
            }

            if (foundLine != -1)
            {
                // Update the line in place
                linesList[foundLine] = entry;
                File.WriteAllLines(journalFile, linesList);
                Utils.LogDebug(nameof(LogStoppedSessionToObsidian), $"Updated session entry at line {foundLine} in journal file: {journalFile}");
            }
            else
            {
                // If the line with the timestamp was not found, add the entry at the end of the section
                bool foundHeader = false;
                int headerIndex = -1;
                for (int i = 0; i < linesList.Count; i++)
                {
                    if (linesList[i].Trim() == header)
                    {
                        foundHeader = true;
                        headerIndex = i;
                    }
                }

                if (foundHeader)
                {
                    // Find the end of the header section (either next header or end of file)
                    int insertIndex = linesList.Count; // Default to end of file
                    for (int i = headerIndex + 1; i < linesList.Count; i++)
                    {
                        string line = linesList[i].Trim();
                        // Check if this line is another header (starts with #)
                        if (line.StartsWith("#") && line.Length > 1)
                        {
                            insertIndex = i;
                            break;
                        }
                    }

                    linesList.Insert(insertIndex, entry);
                    File.WriteAllLines(journalFile, linesList);
                    Utils.LogDebug(nameof(LogStoppedSessionToObsidian), $"Appended stopped session entry at end of section at line {insertIndex} in journal file: {journalFile}");
                }
                else
                {
                    linesList.Add("");
                    linesList.Add(header);
                    linesList.Add(entry);
                    File.WriteAllLines(journalFile, linesList);
                    Utils.LogDebug(nameof(LogStoppedSessionToObsidian), $"Header not found, appended header and stopped session entry at end of journal file: {journalFile}");
                }
            }
        }

        public bool IsRunning => _isRunning;
        public TimeSpan TimeLeft => _timeLeft;

        // Add: Update the current session input field (task)
        public void UpdateCurrentTask(string task)
        {
            var settings = AppSettings.Load();
            settings.CurrentSessionInputField = task;
            System.Diagnostics.Debug.WriteLine("Saving Settings in UpdateCurrentTask");

            settings.Save();
        }

        // Add: Reset session state (timestamp, start time, input field)
        public void ResetSessionState()
        {
            var settings = AppSettings.Load();
            settings.CurrentSessionTimestamp = null;
            settings.CurrentSessionStartTime = null;
            settings.CurrentSessionInputField = null;
            System.Diagnostics.Debug.WriteLine("Resetting Settings in ResetSessionState");

            settings.Save();
        }

        // Add: Reset session timestamp and start time only
        public void ResetSessionTimestamp()
        {
            var settings = AppSettings.Load();
            settings.CurrentSessionTimestamp = null;
            settings.CurrentSessionStartTime = null;
            System.Diagnostics.Debug.WriteLine("Resetting Timestamp and StartTime in ResetSessionTimestamp");
            settings.Save();
        }

        // Add: Update Pomodoro length and save
        public void UpdatePomodoroLength(int newLength)
        {
            var settings = AppSettings.Load();
            settings.PomodoroTimerLength = newLength;
            System.Diagnostics.Debug.WriteLine("saving settings in UpdatePomodoroLenthth");

            settings.Save();
            SetPomodoroLength(newLength);
        }

        // Add: Add a tag to settings
        public void AddTag(string tag)
        {
            var settings = AppSettings.Load();
            if (!string.IsNullOrEmpty(tag) && !settings.Tags.Contains(tag))
            {
                settings.Tags.Add(tag);
                System.Diagnostics.Debug.WriteLine("saving settings in AddTag");
                settings.Save();
            }
        }

        // Add: Get all .md files from the Obsidian Vault path and save to VaultFilesList.json and VaultTagsList.json
        public void GetFileListFromVault()
        {
            var settings = AppSettings.Load();
            string vaultPath = settings.ObsidianVaultPath;
            if (string.IsNullOrWhiteSpace(vaultPath) || !Directory.Exists(vaultPath))
            {
                Utils.LogDebug(nameof(GetFileListFromVault), $"Vault path is not set or does not exist: '{vaultPath}'");
                return;
            }
            var startTime = DateTime.Now;
            Utils.LogDebug(nameof(GetFileListFromVault), $"Started scanning vault at {startTime:O}");
            try
            {
                var mdFiles = Directory.GetFiles(vaultPath, "*.md", SearchOption.AllDirectories)
                    .ToList();
                var fileNames = mdFiles.Select(f => Path.GetFileNameWithoutExtension(f)).Distinct().ToList();
                var tagSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var tagFileMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase); // For debug
                // Updated regex: only match # if at start of line or preceded by space/tab, and not followed by /
                var tagRegex = new Regex(@"(^|[ \t])#(?!/)[^\s#]+", RegexOptions.Compiled);
                foreach (var file in mdFiles)
                {
                    try
                    {
                        var content = File.ReadAllText(file);
                        foreach (Match match in tagRegex.Matches(content))
                        {
                            // Use group 0 if match at start, group 2 if preceded by whitespace
                            string tag = match.Groups.Count > 2 ? match.Groups[0].Value.TrimStart() : match.Value;
                            // Clean up: ignore tags with forbidden chars or malformed
                            if (tag.Length <= 1) continue;
                            tag = tag.Normalize(System.Text.NormalizationForm.FormC).Trim();
                            if (tag.Any(c => char.IsControl(c) || c == ',' || c == '"' || c == '\'' || c == '\\' || c == '%' || c == '^' || c == '&' || c == '(' || c == ')')) continue;
                            tag = tag.Trim(' ', ',', '"', '\'', ')', '�', '�', '�', '.', ';', ':', '-', '_');
                            while (tag.Length > 1 && !char.IsLetterOrDigit(tag[tag.Length - 1]) && tag[tag.Length - 1] != '/')
                                tag = tag.Substring(0, tag.Length - 1);
                            while (tag.Length > 1 && !char.IsLetterOrDigit(tag[1]) && tag[1] != '/')
                                tag = tag[0] + tag.Substring(2);
                            if (tag.Length <= 1) continue;
                            var tagBody = tag.Length > 1 ? tag.Substring(1) : string.Empty;
                            if (System.Text.RegularExpressions.Regex.IsMatch(tagBody, @"^[0-9.]+$")) continue;
                            if (!tagSet.Contains(tag))
                                tagSet.Add(tag);
                            if (SettingsWindow.DebugLogEnabled)
                            {
                                if (!tagFileMap.ContainsKey(tag))
                                    tagFileMap[tag] = new List<string>();
                                tagFileMap[tag].Add(file);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Utils.LogDebug(nameof(GetFileListFromVault), $"Error reading file '{file}': {ex.Message}");
                    }
                }
                string outputFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VaultFilesList.json");
                File.WriteAllText(outputFile, JsonSerializer.Serialize(fileNames, new JsonSerializerOptions { WriteIndented = true }));
                string tagFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VaultTagsList.json");
                File.WriteAllText(tagFile, JsonSerializer.Serialize(tagSet.OrderBy(t => t), new JsonSerializerOptions { WriteIndented = true }));
                if (SettingsWindow.DebugLogEnabled)
                {
                    string debugTagFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VaultTagsListDebug.json");
                    File.WriteAllText(debugTagFile, JsonSerializer.Serialize(tagFileMap.OrderBy(kvp => kvp.Key), new JsonSerializerOptions { WriteIndented = true }));
                }
                var endTime = DateTime.Now;
                Utils.LogDebug(nameof(GetFileListFromVault), $"Finished scanning vault at {endTime:O}. Duration: {(endTime - startTime).TotalSeconds:F2} seconds. Found {fileNames.Count} .md files and {tagSet.Count} tags.");
            }
            catch (Exception ex)
            {
                Utils.LogDebug(nameof(GetFileListFromVault), $"Error reading vault files: {ex.Message}");
            }
        }

        public bool IsReverseCountdown => _reverseCountdown;
    }
}
