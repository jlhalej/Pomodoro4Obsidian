using System;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Controls;
using System.Linq;
using System.Runtime.InteropServices; // For Win32 interop

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

        public MiniWindow(AppSettings settings)
        {
            InitializeComponent();
            this.Topmost = true;
            _settings = settings;
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
            SetTimerRunning(PomodoroSessionManager.Instance.IsRunning);

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

            PomodoroSessionManager.Instance.ReverseCountdownStarted += (s, e) => StartFlashing();
            PomodoroSessionManager.Instance.Tick += (s, t) => UpdateTimerText(t);
            PomodoroSessionManager.Instance.Stopped += (s, e) => {
                StopFlashing();
                SetTimerRunning(false);
            };
            PomodoroSessionManager.Instance.NegativeTimerTick += (s, t) => UpdateNegativeTimerText(t);

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
            if (PomodoroSessionManager.Instance.IsRunning)
            {
                PomodoroSessionManager.Instance.Stop();
            }
            else
            {
                var task = MiniTaskText;
                PomodoroSessionManager.Instance.Start(task, "");
            }
            TimerStartStopClicked?.Invoke(this, EventArgs.Empty);
        }

        private void MiniTimerText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                if (PomodoroSessionManager.Instance.IsRunning)
                {
                    PomodoroSessionManager.Instance.Pause();
                }
                else
                {
                    // Resume if paused (session timestamp exists)
                    var settings = AppSettings.Load();
                    if (!PomodoroSessionManager.Instance.IsRunning && !string.IsNullOrEmpty(settings.CurrentSessionTimestamp))
                    {
                        var task = MiniTaskText;
                        PomodoroSessionManager.Instance.Start(task, "");
                    }
                }
            }
            else if (!PomodoroSessionManager.Instance.IsRunning && e.ClickCount == 2)
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
            var miniTaskInput = sender as TextBox;
            if (miniTaskInput == null) return;
            if (e.Changes.Any(change =>
                change.AddedLength == 1 &&
                miniTaskInput.Text.Length > change.Offset &&
                miniTaskInput.Text[change.Offset] == '#'))
            {
                int hashIndex = miniTaskInput.Text.IndexOf('#');
                if (hashIndex >= 0)
                {
                    string originalText = miniTaskInput.Text;
                    var picker = new TagPickerWindow();
                    if (this.IsVisible)
                        picker.Owner = this;
                    if (picker.ShowDialog() == true && !string.IsNullOrEmpty(picker.SelectedTag))
                    {
                        string before = originalText.Substring(0, hashIndex);
                        string after = originalText.Substring(hashIndex + 1);
                        miniTaskInput.Text = before + picker.SelectedTag + after;
                        miniTaskInput.CaretIndex = (before + picker.SelectedTag).Length;
                    }
                }
            }
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

        private void MiniTaskInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var miniTaskInput = sender as TextBox;
                if (miniTaskInput != null)
                {
                    var settings = AppSettings.Load();
                    settings.CurrentSessionInputField = miniTaskInput.Text;
                    settings.Save();
                }
                MiniStartStopButton_Click(sender, new RoutedEventArgs());
                e.Handled = true;
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
                if (PomodoroSessionManager.Instance.IsReverseCountdown)
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
