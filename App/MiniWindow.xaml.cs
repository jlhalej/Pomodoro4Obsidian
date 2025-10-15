using System;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices; // For Win32 interop
using PomodoroForObsidian.Managers;

namespace PomodoroForObsidian
{
    public partial class MiniWindow : Window
    {
        public event EventHandler? TimerStartStopClicked;
        public event EventHandler? TimerResetRequested;
        private TimeSpan _timeLeft;
        private AppSettings _settings;
        private DispatcherTimer _flashTimer;
        private bool _isFlashing = false;
        private bool _flashState = false;
        private AutoCompleteManager _autoCompleteManager;
        private DispatcherTimer _debounceTimer;
        private PomodoroSessionManager _pomodoroSessionManager;

        // Win32 interop for resizing
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern IntPtr ReleaseCapture();

        private const int WM_NCLBUTTONDOWN = 0x00A1;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        public MiniWindow(AppSettings settings, AutoCompleteManager autoCompleteManager, PomodoroSessionManager pomodoroSessionManager)
        {
            InitializeComponent();
            this.Topmost = true;
            _settings = settings;
            _autoCompleteManager = autoCompleteManager;
            _pomodoroSessionManager = pomodoroSessionManager;

            _debounceTimer = new DispatcherTimer();
            _debounceTimer.Interval = TimeSpan.FromMilliseconds(300);
            _debounceTimer.Tick += DebounceTimer_Tick;

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
                miniTaskInput.MouseDoubleClick += MiniTaskInput_MouseDoubleClick;
                miniTaskInput.KeyDown += MiniTaskInput_KeyDown;
                // Load saved value
                if (!string.IsNullOrEmpty(_settings.CurrentSessionInputField))
                    miniTaskInput.Text = _settings.CurrentSessionInputField;
            }

            _pomodoroSessionManager.ReverseCountdownStarted += (s, e) => StartFlashing();
            _pomodoroSessionManager.Tick += (s, t) => UpdateTimerText(t);
            _pomodoroSessionManager.Stopped += (s, e) => {
                StopFlashing();
                SetTimerRunning(false);
            };
            _pomodoroSessionManager.NegativeTimerTick += (s, t) => UpdateNegativeTimerText(t);

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
                timerText.Text = _timeLeft.ToString(@"mm\:ss");
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
                var task = MiniTaskText;
                _pomodoroSessionManager.Start(task, "");
            }
            TimerStartStopClicked?.Invoke(this, EventArgs.Empty);
        }

        private void MiniTimerText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                if (_pomodoroSessionManager.IsRunning)
                {
                    _pomodoroSessionManager.Pause();
                }
                else
                {
                    // Resume if paused (session timestamp exists)
                    var settings = AppSettings.Load();
                    if (!_pomodoroSessionManager.IsRunning && !string.IsNullOrEmpty(settings.CurrentSessionTimestamp))
                    {
                        var task = MiniTaskText;
                        _pomodoroSessionManager.Start(task, "");
                    }
                }
            }
            else if (!_pomodoroSessionManager.IsRunning && e.ClickCount == 2)
            {
                TimerResetRequested?.Invoke(this, EventArgs.Empty);
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
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private void MiniTaskInput_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var tb = sender as TextBox;
            if (tb != null)
            {
                tb.SelectAll();
                e.Handled = true;
            }
        }

        private async void MiniTaskInput_KeyDown(object sender, KeyEventArgs e)
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

            if (AutoCompletePopup.IsOpen)
            {
                if (e.Key == Key.Down)
                {
                    AutoCompleteListBox.Focus();
                    AutoCompleteListBox.SelectedIndex = (AutoCompleteListBox.SelectedIndex + 1) % AutoCompleteListBox.Items.Count;
                    e.Handled = true;
                }
                else if (e.Key == Key.Up)
                {
                    AutoCompleteListBox.Focus();
                    AutoCompleteListBox.SelectedIndex = (AutoCompleteListBox.SelectedIndex - 1 + AutoCompleteListBox.Items.Count) % AutoCompleteListBox.Items.Count;
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    AutoCompletePopup.IsOpen = false;
                    e.Handled = true;
                }
                else if (e.Key == Key.Tab)
                {
                    if (AutoCompleteListBox.SelectedItem != null)
                    {
                        MiniTaskInput.Text = GetCleanSuggestionText(AutoCompleteListBox.SelectedItem.ToString());
                        MiniTaskInput.CaretIndex = MiniTaskInput.Text.Length;
                    }
                    // Keep popup open for refinement
                    e.Handled = true;
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

            var suggestions = await _autoCompleteManager.GetSuggestionsAsync(MiniTaskInput.Text);

            if (suggestions.Any())
            {
                AutoCompleteListBox.ItemsSource = suggestions;
                AutoCompletePopup.IsOpen = true;
            }
            else
            {
                AutoCompletePopup.IsOpen = false;
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
                _flashTimer.Interval = TimeSpan.FromMilliseconds(600);
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
                    timerText.Text = "-" + t.Negate().ToString(@"mm\:ss");
                }
                else
                {
                    timerText.Text = t.ToString(@"mm\:ss");
                }
            }
        }

        private void UpdateNegativeTimerText(TimeSpan t)
        {
            var timerText = this.FindName("MiniTimerText") as TextBlock;
            if (timerText != null)
            {
                timerText.Text = "-" + t.ToString(@"mm\:ss");
            }
        }

        private void AttachResizeHandleEvents()
        {
            var left = this.FindName("ResizeHandleLeft") as Border;
            var right = this.FindName("ResizeHandleRight") as Border;
            var top = this.FindName("ResizeHandleTop") as Border;
            var bottom = this.FindName("ResizeHandleBottom") as Border;
            var topLeft = this.FindName("ResizeHandleTopLeft") as Border;
            var topRight = this.FindName("ResizeHandleTopRight") as Border;
            var bottomLeft = this.FindName("ResizeHandleBottomLeft") as Border;
            var bottomRight = this.FindName("ResizeHandleBottomRight") as Border;

            if (left != null) left.MouseDown += ResizeLeft_MouseDown;
            if (right != null) right.MouseDown += ResizeRight_MouseDown;
            if (top != null) top.MouseDown += ResizeTop_MouseDown;
            if (bottom != null) bottom.MouseDown += ResizeBottom_MouseDown;
            if (topLeft != null) topLeft.MouseDown += ResizeTopLeft_MouseDown;
            if (topRight != null) topRight.MouseDown += ResizeTopRight_MouseDown;
            if (bottomLeft != null) bottomLeft.MouseDown += ResizeBottomLeft_MouseDown;
            if (bottomRight != null) bottomRight.MouseDown += ResizeBottomRight_MouseDown;
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

        private void ResizeTop_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                ResizeWindow(HTTOP);
            }
        }

        private void ResizeBottom_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                ResizeWindow(HTBOTTOM);
            }
        }

        private void ResizeTopLeft_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                ResizeWindow(HTTOPLEFT);
            }
        }

        private void ResizeTopRight_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                ResizeWindow(HTTOPRIGHT);
            }
        }

        private void ResizeBottomLeft_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                ResizeWindow(HTBOTTOMLEFT);
            }
        }

        private void ResizeBottomRight_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                ResizeWindow(HTBOTTOMRIGHT);
            }
        }

        private void ResizeWindow(int direction)
        {
            // Must release capture before sending WM_NCLBUTTONDOWN message
            ReleaseCapture();
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            SendMessage(hwnd, WM_NCLBUTTONDOWN, (IntPtr)direction, IntPtr.Zero);
        }
    }
}
