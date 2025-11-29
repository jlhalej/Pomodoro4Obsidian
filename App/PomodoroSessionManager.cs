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
        public event EventHandler? ReverseCountdownEnded;

        public event EventHandler<TimeSpan>? Tick;
        public event EventHandler? Started;
        public event EventHandler? Stopped;
        public event EventHandler? Reset;

        private string? _lastTask;
        private string? _lastProject;

        private DispatcherTimer? _negativeTimer;
        private TimeSpan _negativeTimeElapsed = TimeSpan.Zero;
        public event EventHandler<TimeSpan>? NegativeTimerTick;

        // In-memory session state to prevent race conditions
        private string? _currentSessionTimestamp;
        private DateTime? _currentSessionStartTime;
        private string? _currentSessionTask;
        private string? _currentSessionProject;

        public PomodoroSessionManager(ITaskHistoryRepository taskHistoryRepository)
        {
            _taskHistoryRepository = taskHistoryRepository;
            _pomodoroLength = 25;
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _updateTimer.Interval = TimeSpan.FromMinutes(1);
            _updateTimer.Tick += _updateTimer_Tick;
            ResetTimer();
        }

        public void SetPomodoroLength(int minutes)
        {
            _pomodoroLength = minutes;
            if (!_isRunning)
                ResetTimer();
        }

        public void AdjustTimerLength(int deltaMinutes)
        {
            // Adjust the current timer by deltaMinutes (can be positive or negative)
            if (!_isRunning && !_reverseCountdown)
                return; // Only adjust if timer is running or in reverse countdown

            if (_reverseCountdown)
            {
                // In negative countdown: when user adds minutes, we subtract from negative elapsed
                // e.g., showing "-1:00" (elapsed 1 min) + add 5 min = need to go back 4 min
                Utils.LogDebug(nameof(AdjustTimerLength), $"Before adjustment: _negativeTimeElapsed={_negativeTimeElapsed}, deltaMinutes={deltaMinutes}");
                _negativeTimeElapsed = _negativeTimeElapsed.Add(TimeSpan.FromMinutes(-deltaMinutes));
                Utils.LogDebug(nameof(AdjustTimerLength), $"After adjustment: _negativeTimeElapsed={_negativeTimeElapsed}");

                if (_negativeTimeElapsed <= TimeSpan.Zero)
                {
                    // If adjustment brings us back to zero or positive, exit reverse countdown
                    Utils.LogDebug(nameof(AdjustTimerLength), $"Exiting reverse countdown. _negativeTimeElapsed={_negativeTimeElapsed}, TotalMinutes={_negativeTimeElapsed.TotalMinutes}");

                    // Store the value BEFORE stopping the negative timer (which might reset it)
                    TimeSpan negativeValue = _negativeTimeElapsed;

                    _reverseCountdown = false;
                    StopNegativeTimer(); // This might reset _negativeTimeElapsed to zero

                    // Convert back to remaining time using the stored value
                    _timeLeft = negativeValue.Negate();
                    Utils.LogDebug(nameof(AdjustTimerLength), $"Set _timeLeft={_timeLeft} (from negating {negativeValue})");

                    _negativeTimeElapsed = TimeSpan.Zero;
                    ReverseCountdownEnded?.Invoke(this, EventArgs.Empty);
                    Tick?.Invoke(this, _timeLeft);
                    Utils.LogDebug(nameof(AdjustTimerLength), $"After Tick invocation, _timeLeft={_timeLeft}");
                }
                else
                {
                    // Still in negative countdown, update the display
                    Utils.LogDebug(nameof(AdjustTimerLength), $"Still in reverse countdown. _negativeTimeElapsed={_negativeTimeElapsed}");
                    NegativeTimerTick?.Invoke(this, _negativeTimeElapsed);
                }
            }
            else
            {
                // Normal countdown: adjust time left
                _timeLeft = _timeLeft.Add(TimeSpan.FromMinutes(deltaMinutes));

                // Don't allow negative values in normal countdown
                if (_timeLeft < TimeSpan.Zero)
                    _timeLeft = TimeSpan.Zero;

                Tick?.Invoke(this, _timeLeft);
            }

            Utils.LogDebug(nameof(AdjustTimerLength), $"Timer adjusted by {deltaMinutes} minutes. _reverseCountdown={_reverseCountdown}, _negativeTimeElapsed={_negativeTimeElapsed}, _timeLeft={_timeLeft}");
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

                // Store session state in memory to prevent race conditions
                _currentSessionTimestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
                _currentSessionStartTime = DateTime.Now;
                _currentSessionTask = task;
                _currentSessionProject = project;

                Utils.LogDebug(nameof(Start), $"Assigning new session timestamp: {_currentSessionTimestamp}");

                // Also save to disk as backup
                settings.CurrentSessionTimestamp = _currentSessionTimestamp;
                settings.CurrentSessionStartTime = _currentSessionStartTime;
                settings.CurrentSessionInputField = task;
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

                // Clear in-memory session state
                _currentSessionTimestamp = null;
                _currentSessionStartTime = null;
                _currentSessionTask = null;
                _currentSessionProject = null;

                // Reset timer in mini window and reset session timestamp/start time
                ResetTimer();
                ResetSessionTimestamp();
                Stopped?.Invoke(this, EventArgs.Empty);
            }
        }

        public void ResetTimer()
        {
            // Load current pomodoro length from settings to ensure we have the latest value
            var settings = AppSettings.Load();
            _pomodoroLength = settings.PomodoroTimerLength;

            _timeLeft = TimeSpan.FromMinutes(_pomodoroLength);
            _isRunning = false;
            _timer.Stop();
            _reverseCountdown = false;
            _reverseTime = TimeSpan.Zero;
            Utils.LogDebug(nameof(ResetTimer), $"Timer reset to {_pomodoroLength} minutes (_timeLeft={_timeLeft})");
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
                    // Check for maximum session length using in-memory session start time
                    if (settings.MaximumSessionLength > 0 && _currentSessionStartTime.HasValue &&
                        (DateTime.Now - _currentSessionStartTime.Value).TotalMinutes >= settings.MaximumSessionLength)
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
                // Reverse countdown: check for maximum session length using in-memory session start time
                if (settings.MaximumSessionLength > 0 && _currentSessionStartTime.HasValue &&
                    (DateTime.Now - _currentSessionStartTime.Value).TotalMinutes >= settings.MaximumSessionLength)
                {
                    Utils.LogDebug(nameof(Timer_Tick), "Maximum session length reached during negative countdown. Auto-stopping timer.");
                    Utils.BubbleNotification("Maximum session length reached. Timer stopped automatically.");
                    Stop();
                    return;
                }
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
            Utils.LogDebug(nameof(StopNegativeTimer), $"StopNegativeTimer called. _negativeTimeElapsed before reset: {_negativeTimeElapsed}");
            if (_negativeTimer != null)
                _negativeTimer.Stop();
            _negativeTimeElapsed = TimeSpan.Zero;
            Utils.LogDebug(nameof(StopNegativeTimer), $"StopNegativeTimer finished. _negativeTimeElapsed after reset: {_negativeTimeElapsed}");
        }

        private void _updateTimer_Tick(object? sender, EventArgs e)
        {
            // Check for midnight crossover: if session started on previous day and now it's first minute of new day
            if (_isRunning && _currentSessionStartTime.HasValue)
            {
                DateTime now = DateTime.Now;
                DateTime sessionStart = _currentSessionStartTime.Value;

                // Detect midnight crossover: session started on previous day and current time is in first minute after midnight
                if (sessionStart.Date < now.Date && now.Hour == 0 && now.Minute == 0)
                {
                    Utils.LogDebug(nameof(_updateTimer_Tick), $"Midnight crossover detected. Session started: {sessionStart:yyyy-MM-dd HH:mm}, Current time: {now:yyyy-MM-dd HH:mm}. Stopping timer.");

                    // Remove timestamp from the final entry in previous day's file before stopping
                    RemoveTimestampFromCurrentSession();

                    // Stop the timer
                    Stop();

                    // Notify user
                    Utils.BubbleNotification("Stopping timer at midnight. Please create the new day note and start a new timer.");

                    return; // Exit early, don't proceed with normal logging
                }
            }

            LogRunningSessionToObsidian();
        }

        private void LogRunningSessionToObsidian(bool isInitialLog = false)
        {
            Utils.LogDebug(nameof(LogRunningSessionToObsidian), "Begin logging session to journal");

            // Use in-memory session state (primary source)
            string timestamp = _currentSessionTimestamp ?? DateTime.Now.ToString("yyyyMMddHHmmssfff");
            DateTime start = _currentSessionStartTime ?? DateTime.Now;
            string task = _currentSessionTask ?? string.Empty;
            string project = _currentSessionProject ?? string.Empty;

            DateTime end = isInitialLog ? start.AddMinutes(1) : DateTime.Now;
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

            var settings = AppSettings.Load();
            string today = DateTime.Now.ToString(settings.JournalNoteFormat.Replace("YYYY", "yyyy").Replace("DD", "dd"), CultureInfo.InvariantCulture);
            string journalFile = Path.Combine(settings.ObsidianJournalPath, today + ".md");
            string header = string.IsNullOrWhiteSpace(settings.Header) ? "# Pomodoro Sessions" : settings.Header;
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
            string fileContent = File.ReadAllText(journalFile);
            bool endsWithNewLine = fileContent.Length > 0 && fileContent.EndsWith("\n");
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
                string newContent = string.Join(Environment.NewLine, linesList);
                if (endsWithNewLine) newContent += Environment.NewLine;
                File.WriteAllText(journalFile, newContent);
                Utils.LogDebug(nameof(LogRunningSessionToObsidian), $"Updated session entry at line {foundLine} in journal file: {journalFile}");
            }
            else if (foundHeader)
            {
                // Find the end of the header section (either next header or end of file)
                int insertIndex = linesList.Count; // Default to end of file
                for (int i = headerIndex + 1; i < linesList.Count; i++)
                {
                    string line = linesList[i].Trim();
                    // Check if this line is another header (starts with # followed by space)
                    // This distinguishes headers from tags (which don't have space after #)
                    if (line.StartsWith("#") && line.Length > 1 && char.IsWhiteSpace(line[1]))
                    {
                        insertIndex = i;
                        break;
                    }
                }

                linesList.Insert(insertIndex, entry);
                string newContent = string.Join(Environment.NewLine, linesList);
                if (endsWithNewLine) newContent += Environment.NewLine;
                File.WriteAllText(journalFile, newContent);
                Utils.LogDebug(nameof(LogRunningSessionToObsidian), $"Appended new session entry at end of section at line {insertIndex} in journal file: {journalFile}");
            }
            else
            {
                // Append at end
                linesList.Add("");
                linesList.Add(header);
                linesList.Add(entry);
                string newContent = string.Join(Environment.NewLine, linesList);
                if (endsWithNewLine) newContent += Environment.NewLine;
                File.WriteAllText(journalFile, newContent);
                Utils.LogDebug(nameof(LogRunningSessionToObsidian), $"Header not found, appended header and new session entry at end of journal file: {journalFile}");
            }
        }

        private void LogStoppedSessionToObsidian()
        {
            Utils.LogDebug(nameof(LogStoppedSessionToObsidian), "Begin logging stopped session to journal");

            // Use in-memory session state (primary source)
            DateTime start = _currentSessionStartTime ?? DateTime.Now;
            string timestamp = _currentSessionTimestamp ?? string.Empty;
            string task = _currentSessionTask ?? string.Empty;
            string project = _currentSessionProject ?? string.Empty;

            DateTime end = DateTime.Now;

            // Handle case where start and end are in the same minute
            bool isSameMinute = (start.Hour == end.Hour && start.Minute == end.Minute);
            if (isSameMinute)
            {
                Utils.LogDebug(nameof(LogStoppedSessionToObsidian), "Start and end time are in the same minute, adjusting end time to +1 minute.");
                end = start.AddMinutes(1);
            }

            if (start == end)
            {
                Utils.LogDebug(nameof(LogStoppedSessionToObsidian), "Start and end time are the same, not logging session.");
                return;
            }

            // Check if session was auto-stopped by maximum session length
            var settings = AppSettings.Load();
            bool wasAutoStopped = false;
            if (settings.MaximumSessionLength > 0)
            {
                double sessionDuration = (end - start).TotalMinutes;
                if (sessionDuration >= settings.MaximumSessionLength)
                {
                    wasAutoStopped = true;
                }
            }

            string today = DateTime.Now.ToString(settings.JournalNoteFormat.Replace("YYYY", "yyyy").Replace("DD", "dd"), CultureInfo.InvariantCulture);
            string journalFile = Path.Combine(settings.ObsidianJournalPath, today + ".md");
            string header = string.IsNullOrWhiteSpace(settings.Header) ? "# Pomodoro Sessions" : settings.Header;
            string autoStopMarker = wasAutoStopped ? " [auto-stopped]" : "";
            string entry = $"- {start:HH:mm} - {end:HH:mm} {task} {project}{autoStopMarker}"; Utils.LogDebug(nameof(LogStoppedSessionToObsidian), $"Preparing to log stopped session. Start: {start:HH:mm}, End: {end:HH:mm}, Task: '{task}', JournalFile: {journalFile}, Timestamp: {timestamp}");
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

            string fileContent = File.ReadAllText(journalFile);
            bool endsWithNewLine = fileContent.Length > 0 && fileContent.EndsWith("\n");
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
                string newContent = string.Join(Environment.NewLine, linesList);
                if (endsWithNewLine) newContent += Environment.NewLine;
                File.WriteAllText(journalFile, newContent);
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
                        // Check if this line is another header (starts with # followed by space)
                        // This distinguishes headers from tags (which don't have space after #)
                        if (line.StartsWith("#") && line.Length > 1 && char.IsWhiteSpace(line[1]))
                        {
                            insertIndex = i;
                            break;
                        }
                    }

                    linesList.Insert(insertIndex, entry);
                    string newContent = string.Join(Environment.NewLine, linesList);
                    if (endsWithNewLine) newContent += Environment.NewLine;
                    File.WriteAllText(journalFile, newContent);
                    Utils.LogDebug(nameof(LogStoppedSessionToObsidian), $"Appended stopped session entry at end of section at line {insertIndex} in journal file: {journalFile}");
                }
                else
                {
                    linesList.Add("");
                    linesList.Add(header);
                    linesList.Add(entry);
                    string newContent = string.Join(Environment.NewLine, linesList);
                    if (endsWithNewLine) newContent += Environment.NewLine;
                    File.WriteAllText(journalFile, newContent);
                    Utils.LogDebug(nameof(LogStoppedSessionToObsidian), $"Header not found, appended header and stopped session entry at end of journal file: {journalFile}");
                }
            }
        }

        public bool IsRunning => _isRunning;
        public TimeSpan TimeLeft => _timeLeft;

        // Add: Update the current session input field (task)
        public void UpdateCurrentTask(string task)
        {
            // If timer is running, update in-memory session state
            if (_isRunning)
            {
                Utils.LogDebug(nameof(UpdateCurrentTask), $"Timer is running. Updating in-memory task from '{_currentSessionTask}' to '{task}'");
                _currentSessionTask = task;
            }

            // Always update settings (used as default for next session)
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

        private void RemoveTimestampFromCurrentSession()
        {
            if (string.IsNullOrEmpty(_currentSessionTimestamp) || !_currentSessionStartTime.HasValue)
            {
                Utils.LogDebug(nameof(RemoveTimestampFromCurrentSession), "No current session to clean up.");
                return;
            }

            Utils.LogDebug(nameof(RemoveTimestampFromCurrentSession), $"Removing timestamp from session: {_currentSessionTimestamp}");

            var settings = AppSettings.Load();
            DateTime sessionStart = _currentSessionStartTime.Value;
            string sessionDay = sessionStart.ToString(settings.JournalNoteFormat.Replace("YYYY", "yyyy").Replace("DD", "dd"), CultureInfo.InvariantCulture);
            string journalFile = Path.Combine(settings.ObsidianJournalPath, sessionDay + ".md");

            if (!File.Exists(journalFile))
            {
                Utils.LogDebug(nameof(RemoveTimestampFromCurrentSession), $"Journal file does not exist: {journalFile}");
                return;
            }

            try
            {
                string fileContent = File.ReadAllText(journalFile);
                var linesList = new List<string>(File.ReadAllLines(journalFile));
                bool fileModified = false;

                // Find the line with our timestamp and remove the timestamp from it
                for (int i = 0; i < linesList.Count; i++)
                {
                    if (linesList[i].Contains(_currentSessionTimestamp))
                    {
                        // Remove the timestamp from the end of the line
                        string line = linesList[i];
                        int timestampIndex = line.LastIndexOf(_currentSessionTimestamp);
                        if (timestampIndex >= 0)
                        {
                            // Remove timestamp and any trailing whitespace
                            linesList[i] = line.Substring(0, timestampIndex).TrimEnd();
                            fileModified = true;
                            Utils.LogDebug(nameof(RemoveTimestampFromCurrentSession), $"Removed timestamp from line {i}: '{linesList[i]}'");
                            break;
                        }
                    }
                }

                if (fileModified)
                {
                    bool endsWithNewLine = fileContent.Length > 0 && fileContent.EndsWith("\n");
                    string newContent = string.Join(Environment.NewLine, linesList);
                    if (endsWithNewLine) newContent += Environment.NewLine;
                    File.WriteAllText(journalFile, newContent);
                    Utils.LogDebug(nameof(RemoveTimestampFromCurrentSession), $"Updated journal file: {journalFile}");
                }
            }
            catch (Exception ex)
            {
                Utils.LogDebug(nameof(RemoveTimestampFromCurrentSession), $"Error removing timestamp: {ex.Message}");
            }
        }

        public bool IsReverseCountdown => _reverseCountdown;
    }
}
