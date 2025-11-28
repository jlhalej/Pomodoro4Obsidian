using System;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices; // For Win32 interop
using PomodoroForObsidian.Managers;

namespace PomodoroForObsidian
{
    public partial class MiniWindow : Window
    {
        public event EventHandler? TimerStartStopClicked;
        private TimeSpan _timeLeft;
        private AppSettings _settings;
        private DispatcherTimer _flashTimer;
        private bool _isFlashing = false;
        private bool _flashState = false;
        private AutoCompleteManager _autoCompleteManager;
        private DispatcherTimer _debounceTimer;
        private DispatcherTimer _settingsSaveDebounceTimer;
        private PomodoroSessionManager _pomodoroSessionManager;
        private TagPickerWindow? _tagPickerWindow;
        private bool _isTagModeActive = false;
        private int _tagStartPosition = -1;
        private bool _isWindowLoaded = false;
        private bool _debugMode = true; // Set to false to disable debug messages

        // Win32 interop for resizing
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern IntPtr ReleaseCapture();

        private const int WM_NCLBUTTONDOWN = 0x00A1;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;

        public MiniWindow(AppSettings settings, AutoCompleteManager autoCompleteManager, PomodoroSessionManager pomodoroSessionManager)
        {
            InitializeComponent();
            this.Topmost = true;
            this.Loaded += MiniWindow_Loaded;
            _settings = settings;
            _autoCompleteManager = autoCompleteManager;
            _pomodoroSessionManager = pomodoroSessionManager;

            _debounceTimer = new DispatcherTimer();
            _debounceTimer.Interval = TimeSpan.FromMilliseconds(300);
            _debounceTimer.Tick += DebounceTimer_Tick;

            _settingsSaveDebounceTimer = new DispatcherTimer();
            _settingsSaveDebounceTimer.Interval = TimeSpan.FromSeconds(2);
            _settingsSaveDebounceTimer.Tick += SettingsSaveDebounceTimer_Tick;

            System.Diagnostics.Debug.WriteLine("[MiniWindow] Constructor called");

            // Restore position and size
            if (_settings.MiniWindowLeft.HasValue && _settings.MiniWindowTop.HasValue)
            {
                this.Left = _settings.MiniWindowLeft.Value;
                this.Top = _settings.MiniWindowTop.Value;

                // Also restore size if available
                if (_settings.MiniWindowWidth.HasValue && _settings.MiniWindowHeight.HasValue)
                {
                    this.Width = _settings.MiniWindowWidth.Value;
                    this.Height = _settings.MiniWindowHeight.Value;
                }
            }
            else
            {
                var desktopWorkingArea = SystemParameters.WorkArea;
                this.Left = (desktopWorkingArea.Width - this.Width) / 2;
                this.Top = desktopWorkingArea.Bottom - this.Height - 10;
            }

            var startStopBtn = this.FindName("MiniStartStopButton") as Button;
            if (startStopBtn != null)
                startStopBtn.Click += MiniStartStopButton_Click;
            SetTimer(TimeSpan.FromMinutes(_settings.PomodoroTimerLength));
            SetTimerRunning(_pomodoroSessionManager.IsRunning);

            var miniTaskInput = this.FindName("MiniTaskInput") as TextBox;
            if (miniTaskInput != null)
            {
                miniTaskInput.LostFocus += MiniTaskInput_LostFocus;
                miniTaskInput.TextChanged += MiniTaskInput_TextChanged;
                miniTaskInput.MouseEnter += MiniTaskInput_MouseEnter;
                miniTaskInput.MouseLeave += MiniTaskInput_MouseLeave;
                miniTaskInput.PreviewMouseDown += MiniTaskInput_PreviewMouseDown;

                if (_debugMode) System.Diagnostics.Debug.WriteLine("[MiniWindow] KeyDown handler attached to MiniTaskInput");
                // Load saved value
                if (!string.IsNullOrEmpty(_settings.CurrentSessionInputField))
                    miniTaskInput.Text = _settings.CurrentSessionInputField;
            }

            _pomodoroSessionManager.ReverseCountdownStarted += (s, e) => StartFlashing();
            _pomodoroSessionManager.ReverseCountdownEnded += (s, e) => StopFlashing();
            _pomodoroSessionManager.Tick += (s, t) => UpdateTimerText(t);
            _pomodoroSessionManager.Stopped += (s, e) =>
            {
                StopFlashing();
                SetTimerRunning(false);
            };
            _pomodoroSessionManager.NegativeTimerTick += (s, t) => UpdateNegativeTimerText(t);

            // Attach mouse wheel event to timer text for scroll adjustment
            var timerText = this.FindName("MiniTimerText") as TextBlock;
            if (timerText != null)
            {
                timerText.MouseWheel += MiniTimerText_MouseWheel;
            }

            // Attach resize handle events
            AttachResizeHandleEvents();
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            if (this.IsLoaded && this.WindowState == WindowState.Normal)
            {
                var settings = AppSettings.Load();
                settings.MiniWindowLeft = this.Left;
                settings.MiniWindowTop = this.Top;
                System.Diagnostics.Debug.WriteLine("saving settings in OnLocationChanged");
                settings.Save();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            var settings = AppSettings.Load();
            settings.MiniWindowLeft = this.Left;
            settings.MiniWindowTop = this.Top;
            settings.MiniWindowWidth = this.Width;
            settings.MiniWindowHeight = this.Height;
            System.Diagnostics.Debug.WriteLine("saving settings in OnClosed");
            settings.Save();
            base.OnClosed(e);
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            if (this.IsLoaded && this.WindowState == WindowState.Normal)
            {
                var settings = AppSettings.Load();
                settings.MiniWindowWidth = this.Width;
                settings.MiniWindowHeight = this.Height;
                System.Diagnostics.Debug.WriteLine("saving settings in OnRenderSizeChanged");
                settings.Save();
            }
        }

        public void SetTimer(TimeSpan time)
        {
            _timeLeft = time;
            var timerText = this.FindName("MiniTimerText") as TextBlock;
            if (timerText != null)
                timerText.Text = FormatTimeSpan(_timeLeft);
        }

        public void SetTimerRunning(bool running)
        {
            System.Diagnostics.Debug.WriteLine($"[MiniWindow] SetTimerRunning called. running={running}");
            var startStopBtn = this.FindName("MiniStartStopButton") as Button;
            if (startStopBtn != null)
                startStopBtn.Content = running ? "■" : "▶";
        }

        private void MiniStartStopButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[MiniWindow] MiniStartStopButton_Click fired");
            if (_pomodoroSessionManager.IsRunning)
            {
                _pomodoroSessionManager.Stop();
            }
            else
            {
                // Set the timer length to the current in-memory value before starting
                // This ensures rapid timer adjustments are used even if not yet saved to disk
                _pomodoroSessionManager.SetPomodoroLength(_settings.PomodoroTimerLength);

                var task = MiniTaskText;
                _pomodoroSessionManager.Start(task, "");
            }
            TimerStartStopClicked?.Invoke(this, EventArgs.Empty);
        }

        private void MiniTimerText_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            int delta = 0;

            if (e.ChangedButton == MouseButton.Left)
            {
                // Left-click: Increase timer
                delta = 5; // Default 5 minutes

                if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                {
                    delta = 30; // Alt key: 30 minutes
                }
                else if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                {
                    delta = 10; // Shift key: 10 minutes
                }
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                // Right-click: Decrease timer
                delta = -5; // Default -5 minutes

                if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                {
                    delta = -30; // Alt key: -30 minutes
                }
                else if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                {
                    delta = -10; // Shift key: -10 minutes
                }
            }

            if (delta != 0)
            {
                AdjustTimer(delta);
                e.Handled = true;
            }
        }

        private void AdjustTimer(int deltaMinutes)
        {
            // Use in-memory settings for fast updates
            int currentLength = _settings.PomodoroTimerLength;

            // Calculate new length
            int newLength = currentLength + deltaMinutes;

            // Clamp to bounds (min 5, max 2400)
            if (newLength < 5 || newLength > 2400)
            {
                return; // Do nothing if out of bounds
            }

            // Update settings in memory
            _settings.PomodoroTimerLength = newLength;
            System.Diagnostics.Debug.WriteLine($"[MiniWindow] Timer length adjusted to {newLength} minutes (delta: {deltaMinutes})");

            // If timer is running, adjust the current time left
            if (_pomodoroSessionManager.IsRunning)
            {
                _pomodoroSessionManager.AdjustTimerLength(deltaMinutes);
            }
            else
            {
                // If not running, just update the display
                SetTimer(TimeSpan.FromMinutes(newLength));
            }

            // Flash timer briefly for visual feedback
            FlashTimerBriefly();

            // Debounce the save operation - only save after user stops clicking
            _settingsSaveDebounceTimer.Stop();
            _settingsSaveDebounceTimer.Start();
        }

        private void MiniTimerText_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Use in-memory settings for fast updates
            int currentLength = _settings.PomodoroTimerLength;

            // Calculate new length (scroll up = +5min, scroll down = -5min)
            int delta = e.Delta > 0 ? 5 : -5;
            int newLength = currentLength + delta;

            // Clamp to bounds (min 5, max 2400)
            if (newLength < 5 || newLength > 2400)
            {
                return; // Do nothing if out of bounds
            }

            // Update settings in memory
            _settings.PomodoroTimerLength = newLength;
            System.Diagnostics.Debug.WriteLine($"[MiniWindow] Timer length adjusted to {newLength} minutes");

            // If timer is running, adjust the current time left
            if (_pomodoroSessionManager.IsRunning)
            {
                _pomodoroSessionManager.AdjustTimerLength(delta);
            }
            else
            {
                // If not running, just update the display
                SetTimer(TimeSpan.FromMinutes(newLength));
            }

            // Flash timer briefly for visual feedback
            FlashTimerBriefly();

            // Debounce the save operation
            _settingsSaveDebounceTimer.Stop();
            _settingsSaveDebounceTimer.Start();

            e.Handled = true;
        }

        private void SettingsSaveDebounceTimer_Tick(object sender, EventArgs e)
        {
            _settingsSaveDebounceTimer.Stop();
            _settings.Save();
            System.Diagnostics.Debug.WriteLine("[MiniWindow] Settings saved after timer adjustment");

            // Show notification with the new timer length
            string timeDisplay = FormatMinutesForNotification(_settings.PomodoroTimerLength);
            Utils.BubbleNotification($"Timer set to {timeDisplay}");
        }

        private string FormatMinutesForNotification(int totalMinutes)
        {
            if (totalMinutes < 60)
            {
                return $"{totalMinutes} minute{(totalMinutes != 1 ? "s" : "")}";
            }
            else if (totalMinutes % 60 == 0)
            {
                int hours = totalMinutes / 60;
                return $"{hours} hour{(hours != 1 ? "s" : "")}";
            }
            else
            {
                int hours = totalMinutes / 60;
                int minutes = totalMinutes % 60;
                return $"{hours}h {minutes}m";
            }
        }

        private void FlashTimerBriefly()
        {
            var timerText = this.FindName("MiniTimerText") as TextBlock;
            if (timerText != null)
            {
                var originalOpacity = timerText.Opacity;
                timerText.Opacity = 0.5;

                var flashTimer = new DispatcherTimer();
                flashTimer.Interval = TimeSpan.FromMilliseconds(100);
                flashTimer.Tick += (s, e) =>
                {
                    timerText.Opacity = originalOpacity;
                    ((DispatcherTimer)s).Stop();
                };
                flashTimer.Start();
            }
        }

        private void Border_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Only allow drag if not clicking on input or button
            if (!(e.OriginalSource is System.Windows.Controls.TextBox) && !(e.OriginalSource is System.Windows.Controls.Button))
            {
                this.DragMove();
            }
        }

        public string MiniTaskText
        {
            get { var tb = this.FindName("MiniTaskInput") as TextBox; return tb != null ? tb.Text : string.Empty; }
            set { var tb = this.FindName("MiniTaskInput") as TextBox; if (tb != null) tb.Text = value; }
        }

        public void SetTaskInput(string value)
        {
            var tb = this.FindName("MiniTaskInput") as TextBox;
            if (tb != null && tb.Text != value)
                tb.Text = value;
        }

        private void MiniTaskInput_LostFocus(object sender, RoutedEventArgs e)
        {
            var miniTaskInput = sender as TextBox;
            if (miniTaskInput != null)
            {
                var settings = AppSettings.Load(); // Always load the latest
                settings.CurrentSessionInputField = miniTaskInput.Text;
                System.Diagnostics.Debug.WriteLine("saving settings in MiniTaskInput_LostFocus");
                settings.Save();
            }
        }

        private void MiniTaskInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isWindowLoaded)
            {
                return;
            }

            // Check for "#" character to trigger tag picker
            var textBox = sender as TextBox;
            if (textBox != null)
            {
                var text = textBox.Text;
                var caretPos = textBox.CaretIndex;

                // Check if we're typing a "#" character
                if (caretPos > 0 && text[caretPos - 1] == '#' && !_isTagModeActive)
                {
                    // Activate tag mode
                    ActivateTagMode(textBox, caretPos - 1);
                    return;
                }
                // Check if we're in tag mode and the "#" was deleted
                else if (_isTagModeActive && caretPos <= _tagStartPosition && caretPos > 0 && text[caretPos - 1] != '#')
                {
                    // Deactivate tag mode
                    DeactivateTagMode();
                }
            }

            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private void ActivateTagMode(TextBox textBox, int tagStartPos)
        {
            _isTagModeActive = true;
            _tagStartPosition = tagStartPos;

            // Close any existing auto-complete popup
            AutoCompletePopup.IsOpen = false;

            // Show tag picker window
            ShowTagPicker(textBox, tagStartPos);
        }

        private void DeactivateTagMode()
        {
            _isTagModeActive = false;
            _tagStartPosition = -1;

            // Close tag picker if open
            if (_tagPickerWindow != null && _tagPickerWindow.IsVisible)
            {
                _tagPickerWindow.Close();
                _tagPickerWindow = null;
            }
        }

        private void ShowTagPicker(TextBox textBox, int tagStartPos)
        {
            // Create and show tag picker window
            _tagPickerWindow = new TagPickerWindow();
            _tagPickerWindow.TagSelected += TagPickerWindow_TagSelected;
            _tagPickerWindow.Closed += TagPickerWindow_Closed;

            // Position the tag picker near the text box
            var point = textBox.PointToScreen(new Point(0, textBox.ActualHeight));
            _tagPickerWindow.Left = point.X;
            _tagPickerWindow.Top = point.Y;
            _tagPickerWindow.Owner = this;
            _tagPickerWindow.Show();
        }

        private void TagPickerWindow_TagSelected(object sender, TagSelectedEventArgs e)
        {
            if (_isTagModeActive && _tagPickerWindow != null)
            {
                var textBox = this.FindName("MiniTaskInput") as TextBox;
                if (textBox != null)
                {
                    // Insert the selected tag
                    var text = textBox.Text;
                    var beforeTag = text.Substring(0, _tagStartPosition);
                    var afterTag = text.Substring(textBox.CaretIndex);
                    var newText = beforeTag + e.Tag + " " + afterTag;
                    textBox.Text = newText;
                    textBox.CaretIndex = _tagStartPosition + e.Tag.Length + 1; // After tag + space
                }
            }
            DeactivateTagMode();
        }

        private void TagPickerWindow_Closed(object sender, EventArgs e)
        {
            DeactivateTagMode();
        }

        private void MiniTaskInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            HandleAutoCompleteInteraction(sender, e);
        }


        private async void HandleAutoCompleteInteraction(object sender, KeyEventArgs e)
        {
            if (AutoCompletePopup.IsOpen)
            {
                switch (e.Key)
                {
                    case Key.Down:
                        AutoCompleteListBox.SelectedIndex = (AutoCompleteListBox.SelectedIndex + 1) % AutoCompleteListBox.Items.Count;
                        e.Handled = true;
                        break;
                    case Key.Up:
                        AutoCompleteListBox.SelectedIndex = (AutoCompleteListBox.SelectedIndex - 1 + AutoCompleteListBox.Items.Count) % AutoCompleteListBox.Items.Count;
                        e.Handled = true;
                        break;
                    case Key.Enter:
                        if (AutoCompleteListBox.SelectedItem != null)
                        {
                            MiniTaskInput.Text = GetCleanSuggestionText(AutoCompleteListBox.SelectedItem.ToString());
                            MiniTaskInput.CaretIndex = MiniTaskInput.Text.Length;
                            AutoCompletePopup.IsOpen = false;
                        }
                        e.Handled = true;
                        break;
                    case Key.Escape:
                        AutoCompletePopup.IsOpen = false;
                        e.Handled = true;
                        break;
                }
            }
            else
            {
                // Handle special key combinations first
                if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    List<string> suggestions = null;
                    bool handled = true;

                    switch (e.Key)
                    {
                        case Key.R:
                            suggestions = await _autoCompleteManager.GetRecentSuggestionsAsync();
                            break;
                        case Key.F:
                            suggestions = await _autoCompleteManager.GetFrequentSuggestionsAsync();
                            break;
                        case Key.Space:
                            suggestions = await _autoCompleteManager.GetSuggestionsAsync(MiniTaskInput.Text, 10);
                            break;
                        default:
                            handled = false;
                            break;
                    }

                    if (suggestions != null)
                    {
                        if (suggestions.Any())
                        {
                            AutoCompleteListBox.ItemsSource = suggestions;
                            AutoCompleteListBox.SelectedIndex = 0; // Select first item by default
                            AutoCompletePopup.IsOpen = true;
                        }
                        else
                        {
                            AutoCompletePopup.IsOpen = false;
                        }
                    }

                    if (handled)
                    {
                        e.Handled = true;
                        return;
                    }
                }
            }

            if (e.Key == Key.Enter)
            {
                bool startTimer = (Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.Shift;

                if (AutoCompletePopup.IsOpen && AutoCompleteListBox.SelectedItem != null)
                {
                    MiniTaskInput.Text = GetCleanSuggestionText(AutoCompleteListBox.SelectedItem.ToString());
                    MiniTaskInput.CaretIndex = MiniTaskInput.Text.Length;
                    AutoCompletePopup.IsOpen = false;
                }

                var miniTaskInput = sender as TextBox;
                if (miniTaskInput != null)
                {
                    var settings = AppSettings.Load();
                    settings.CurrentSessionInputField = miniTaskInput.Text;
                    settings.Save();
                }

                if (startTimer)
                {
                    MiniStartStopButton_Click(sender, new RoutedEventArgs());
                }

                e.Handled = true;
            }
        }

        private async void DebounceTimer_Tick(object sender, EventArgs e)
        {
            _debounceTimer.Stop();

            // Save the current text to settings so journal updates can pick it up
            var settings = AppSettings.Load();
            settings.CurrentSessionInputField = MiniTaskInput.Text;
            settings.Save();

            // Skip auto-complete if in tag mode or if we're typing a tag
            if (_isTagModeActive) return;

            var suggestions = await _autoCompleteManager.GetSuggestionsAsync(MiniTaskInput.Text);

            if (suggestions.Any())
            {
                AutoCompleteListBox.ItemsSource = suggestions;
                AutoCompleteListBox.SelectedIndex = 0; // Select first item by default
                AutoCompletePopup.IsOpen = true;
            }
            else
            {
                AutoCompletePopup.IsOpen = false;
            }
        }

        private async Task TriggerAutoCompleteSyncAsync()
        {
            if (_debugMode) System.Diagnostics.Debug.WriteLine("[MiniWindow] TriggerAutoCompleteSyncAsync called");
            // Skip auto-complete if in tag mode or if we're typing a tag
            if (_isTagModeActive) return;

            var suggestions = await _autoCompleteManager.GetSuggestionsAsync(MiniTaskInput.Text);
            if (_debugMode) System.Diagnostics.Debug.WriteLine($"[MiniWindow] Got {suggestions.Count} suggestions for text: '{MiniTaskInput.Text}'");

            if (suggestions.Any())
            {
                AutoCompleteListBox.ItemsSource = suggestions;
                AutoCompleteListBox.SelectedIndex = 0; // Select first item by default
                AutoCompletePopup.IsOpen = true;
                if (_debugMode) System.Diagnostics.Debug.WriteLine("[MiniWindow] Popup opened with suggestions");
            }
            else
            {
                if (_debugMode) System.Diagnostics.Debug.WriteLine("[MiniWindow] No suggestions for current text, trying recent suggestions");
                // If no suggestions for current text, show recent suggestions
                suggestions = await _autoCompleteManager.GetRecentSuggestionsAsync(5);
                if (_debugMode) System.Diagnostics.Debug.WriteLine($"[MiniWindow] Got {suggestions.Count} recent suggestions");

                if (suggestions.Any())
                {
                    AutoCompleteListBox.ItemsSource = suggestions;
                    AutoCompleteListBox.SelectedIndex = 0; // Select first item by default
                    AutoCompletePopup.IsOpen = true;
                    if (_debugMode) System.Diagnostics.Debug.WriteLine("[MiniWindow] Popup opened with recent suggestions");
                }
                else
                {
                    if (_debugMode) System.Diagnostics.Debug.WriteLine("[MiniWindow] No recent suggestions either, closing popup");
                    AutoCompletePopup.IsOpen = false;
                }
            }
        }

        private string GetCleanSuggestionText(string suggestion)
        {
            if (suggestion.StartsWith("🕒 ") || suggestion.StartsWith("⭐ "))
            {
                return suggestion.Substring(2);
            }
            return suggestion;
        }

        private void AutoCompleteListBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (AutoCompleteListBox.SelectedItem != null)
                {
                    MiniTaskInput.Text = GetCleanSuggestionText(AutoCompleteListBox.SelectedItem.ToString());
                    AutoCompletePopup.IsOpen = false;
                    MiniTaskInput.Focus();
                    MiniTaskInput.CaretIndex = MiniTaskInput.Text.Length;
                }
            }
        }

        private void AutoCompleteListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (AutoCompleteListBox.SelectedItem != null)
            {
                MiniTaskInput.Text = GetCleanSuggestionText(AutoCompleteListBox.SelectedItem.ToString());
                AutoCompletePopup.IsOpen = false;
                MiniTaskInput.Focus();
                MiniTaskInput.CaretIndex = MiniTaskInput.Text.Length;
            }
        }

        private void StartFlashing()
        {
            if (_flashTimer == null)
            {
                _flashTimer = new DispatcherTimer();
                _flashTimer.Interval = TimeSpan.FromMilliseconds(500);
                _flashTimer.Tick += (s, e) => ToggleFlash();
            }
            _isFlashing = true;
            _flashState = false;
            _flashTimer.Start();
        }

        private void StopFlashing()
        {
            if (_flashTimer != null)
                _flashTimer.Stop();
            _isFlashing = false;
            var timerText = this.FindName("MiniTimerText") as TextBlock;
            if (timerText != null)
                timerText.Opacity = 1.0;
        }

        private void ToggleFlash()
        {
            var timerText = this.FindName("MiniTimerText") as TextBlock;
            if (timerText != null)
            {
                _flashState = !_flashState;
                timerText.Opacity = _flashState ? 0.3 : 1.0;
            }
        }

        private void UpdateTimerText(TimeSpan t)
        {
            var timerText = this.FindName("MiniTimerText") as TextBlock;
            if (timerText != null)
            {
                if (_pomodoroSessionManager.IsReverseCountdown)
                {
                    timerText.Text = "-" + FormatTimeSpan(t.Negate());
                }
                else
                {
                    timerText.Text = FormatTimeSpan(t);
                }
            }
        }

        private void UpdateNegativeTimerText(TimeSpan t)
        {
            var timerText = this.FindName("MiniTimerText") as TextBlock;
            if (timerText != null)
            {
                timerText.Text = "-" + FormatTimeSpan(t);
            }
        }

        private string FormatTimeSpan(TimeSpan t)
        {
            // For times under 1 hour (60 minutes), use mm:ss format
            // For times 1 hour and over, use h:mm:ss format
            if (t.TotalMinutes < 60)
            {
                return t.ToString(@"mm\:ss");
            }
            else
            {
                return t.ToString(@"h\:mm\:ss");
            }
        }

        private void AttachResizeHandleEvents()
        {
            var left = this.FindName("ResizeHandleLeft") as Border;
            var right = this.FindName("ResizeHandleRight") as Border;

            if (left != null) left.MouseDown += ResizeLeft_MouseDown;
            if (right != null) right.MouseDown += ResizeRight_MouseDown;
        }

        private void MiniWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _isWindowLoaded = true;
        }

        // Resize handle events
        private void ResizeLeft_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                ResizeWindow(HTLEFT);
            }
        }

        private void ResizeRight_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                ResizeWindow(HTRIGHT);
            }
        }



        private void ResizeWindow(int direction)
        {
            // Must release capture before sending WM_NCLBUTTONDOWN message
            ReleaseCapture();
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            SendMessage(hwnd, WM_NCLBUTTONDOWN, (IntPtr)direction, IntPtr.Zero);
        }

        private void MiniTaskInput_MouseEnter(object sender, MouseEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null && string.IsNullOrEmpty(textBox.Text))
            {
                textBox.ToolTip = "What are you working on?";
            }
        }

        private void MiniTaskInput_MouseLeave(object sender, MouseEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null)
            {
                textBox.ToolTip = null;
            }
        }

        private void MiniTaskInput_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null && e.ClickCount == 3)
            {
                textBox.SelectAll();
                e.Handled = true;
            }
        }
    }
}
