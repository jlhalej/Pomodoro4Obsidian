using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using PomodoroForObsidian.Managers;

namespace PomodoroForObsidian
{
    public class TrayIcon : IDisposable
    {
        private NotifyIcon _notifyIcon = new NotifyIcon();
        private ToolStripMenuItem _settingsMenuItem = new ToolStripMenuItem();

        // Fields for blinking icon functionality
        private DispatcherTimer? _blinkTimer;
        private bool _isBlinking = false;
        private bool _isFirstColor = true;
        private Icon? _defaultIcon;
        private Icon? _primaryIcon;
        private Icon? _secondaryIcon;

        private PomodoroSessionManager _pomodoroSessionManager;
        private AppSettings _settings;
        private AutoCompleteManager _autoCompleteManager;

        private Color PrimaryColor = Color.Purple;
        private Color SecondaryColor = Color.FromArgb(40, 40, 40);

        // For icon cleanup
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        extern static bool DestroyIcon(IntPtr handle);

        public TrayIcon(PomodoroSessionManager pomodoroSessionManager, AppSettings settings, AutoCompleteManager autoCompleteManager)
        {
            _pomodoroSessionManager = pomodoroSessionManager;
            _settings = settings;
            _autoCompleteManager = autoCompleteManager;

            Utils.LogDebug("TrayIcon", "Initializing TrayIcon");

            // Use custom tray icon
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "timer16.ico");
                Utils.LogDebug("TrayIcon", $"Loading icon from: {iconPath}, File exists: {File.Exists(iconPath)}");
                _notifyIcon.Icon = new Icon(iconPath);
                _defaultIcon = new Icon(iconPath);
                Utils.LogDebug("TrayIcon", "Default icon loaded successfully");
            }
            catch (Exception ex)
            {
                Utils.LogDebug("TrayIcon", $"Failed to load tray icon: {ex.Message}");
                _notifyIcon.Icon = SystemIcons.Application;
                _defaultIcon = SystemIcons.Application;
            }

            _notifyIcon.Visible = true;

            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            var versionString = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "unknown";

            _notifyIcon.Text = $"PomodoroForObsidian v{versionString}";
            Utils.LogDebug("TrayIcon", $"Set tray icon tooltip to: {_notifyIcon.Text}");

            _notifyIcon.ContextMenuStrip = new ContextMenuStrip();

            _settingsMenuItem = new ToolStripMenuItem("Settings");
            _settingsMenuItem.Click += (s, e) => ShowSettingsWindow();

            _notifyIcon.ContextMenuStrip.Items.Add(_settingsMenuItem);
            _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            _notifyIcon.ContextMenuStrip.Items.Add("Exit", null, (s, e) => ExitApp());

            _notifyIcon.MouseClick += NotifyIcon_MouseClick;

            try
            {
                _primaryIcon = CreateColorIcon(PrimaryColor, true);
                _secondaryIcon = CreateColorIcon(SecondaryColor, false);
                Utils.LogDebug("TrayIcon", "Pre-created color icons successfully");
            }
            catch (Exception ex)
            {
                Utils.LogDebug("TrayIcon", $"Error creating color icons: {ex.Message}");
            }

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _blinkTimer = new DispatcherTimer(DispatcherPriority.Normal, System.Windows.Application.Current.Dispatcher);
                _blinkTimer.Interval = TimeSpan.FromMilliseconds(500);
                _blinkTimer.Tick += BlinkTimer_Tick;
                Utils.LogDebug("TrayIcon", "Blinking timer initialized on UI thread");
            });

            _pomodoroSessionManager.ReverseCountdownStarted += (s, e) =>
            {
                Utils.LogDebug("TrayIcon", "ReverseCountdownStarted event triggered, starting blinking");
                StartBlinking();
            };
            _pomodoroSessionManager.Stopped += (s, e) =>
            {
                Utils.LogDebug("TrayIcon", "Stopped event triggered, stopping blinking");
                StopBlinking();
            };
            _pomodoroSessionManager.Reset += (s, e) =>
            {
                Utils.LogDebug("TrayIcon", "Reset event triggered, stopping blinking");
                StopBlinking();
            };
        }

        private void BlinkTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                if (_isBlinking)
                {
                    _isFirstColor = !_isFirstColor;
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        _notifyIcon.Icon = _isFirstColor ? _primaryIcon : _secondaryIcon;
                    });
                }
            }
            catch (Exception ex)
            {
                Utils.LogDebug("TrayIcon", $"Error during timer tick: {ex.Message}");
            }
        }

        private Icon CreateColorIcon(Color backgroundColor, bool includePlayIcon)
        {
            Utils.LogDebug("TrayIcon", $"Creating {backgroundColor.Name} icon, includePlayIcon: {includePlayIcon}");
            try
            {
                using (Bitmap bitmap = new Bitmap(16, 16))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        g.Clear(backgroundColor);
                        using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath())
                        {
                            path.AddRectangle(new Rectangle(0, 0, 15, 15));
                            using (Pen borderPen = new Pen(Color.FromArgb(40, 40, 40), 1))
                            {
                                g.DrawPath(borderPen, path);
                            }
                        }
                        if (includePlayIcon)
                        {
                            System.Drawing.Point[] playTriangle = new System.Drawing.Point[]
                            {
                                new System.Drawing.Point(5, 3),
                                new System.Drawing.Point(5, 13),
                                new System.Drawing.Point(12, 8)
                            };
                            using (SolidBrush brush = new SolidBrush(Color.White))
                            {
                                g.FillPolygon(brush, playTriangle);
                            }
                        }
                    }
                    IntPtr hIcon = bitmap.GetHicon();
                    Icon icon = Icon.FromHandle(hIcon);
                    Icon clonedIcon = (Icon)icon.Clone();
                    DestroyIcon(hIcon);
                    Utils.LogDebug("TrayIcon", "Created icon successfully");
                    return clonedIcon;
                }
            }
            catch (Exception ex)
            {
                Utils.LogDebug("TrayIcon", $"Error creating icon: {ex.Message}");
                throw;
            }
        }

        public void StartBlinking()
        {
            Utils.LogDebug("TrayIcon", "StartBlinking called");
            try
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    _isBlinking = true;
                    _isFirstColor = true;
                    _notifyIcon.Icon = _primaryIcon;
                    if (_blinkTimer != null)
                    {
                        _blinkTimer.Start();
                        Utils.LogDebug("TrayIcon", $"Blinking timer started, IsEnabled: {_blinkTimer.IsEnabled}");
                    }
                    else
                    {
                        Utils.LogDebug("TrayIcon", "ERROR: Blinking timer is null!");
                    }
                });
            }
            catch (Exception ex)
            {
                Utils.LogDebug("TrayIcon", $"Error starting blinking: {ex.Message}");
            }
        }

        public void StopBlinking()
        {
            Utils.LogDebug("TrayIcon", "StopBlinking called");
            try
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (_isBlinking)
                    {
                        _isBlinking = false;
                        if (_blinkTimer != null)
                        {
                            _blinkTimer.Stop();
                            Utils.LogDebug("TrayIcon", "Blinking timer stopped");
                        }
                        _notifyIcon.Icon = _defaultIcon;
                        Utils.LogDebug("TrayIcon", "Restored default icon");
                    }
                });
            }
            catch (Exception ex)
            {
                Utils.LogDebug("TrayIcon", $"Error stopping blinking: {ex.Message}");
            }
        }

        private void NotifyIcon_MouseClick(object? sender, MouseEventArgs e)
        {
            Utils.LogDebug("TrayIcon", $"MouseClick detected: {e.Button}");
            if (e.Button == MouseButtons.Left)
            {
                ShowMiniWindow();
                if (_isBlinking && _pomodoroSessionManager.IsReverseCountdown)
                {
                    Utils.LogDebug("TrayIcon", "Stopping blinking due to mouse click during reverse countdown");
                    StopBlinking();
                }
            }
        }

        private void ShowMiniWindow()
        {
            var app = (App)System.Windows.Application.Current;
            var miniWindow = app.GetMiniWindow();
            if (miniWindow == null)
            {
                miniWindow = new MiniWindow(_settings, _autoCompleteManager, _pomodoroSessionManager);
                app.SetMiniWindow(miniWindow);
            }
            miniWindow.Show();
            miniWindow.Activate();
            miniWindow.Focus();
        }

        private void ShowSettingsWindow()
        {
            var settingsWindow = new SettingsWindow();
            settingsWindow.Closed += (s, e) =>
            {
                var settings = AppSettings.Load();
                if (!_pomodoroSessionManager.IsRunning && !_pomodoroSessionManager.IsReverseCountdown)
                {
                    var app = (App)System.Windows.Application.Current;
                    var miniWindow = app.GetMiniWindow();
                    miniWindow?.SetTimer(TimeSpan.FromMinutes(settings.PomodoroTimerLength));
                }
            };
            settingsWindow.Show();
        }

        private void ExitApp()
        {
            var app = (App)System.Windows.Application.Current;
            app?.ExitAppExplicit();
        }

        public void ShowBalloon(string message, string title = "PomodoroForObsidian", ToolTipIcon icon = ToolTipIcon.Info)
        {
            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText = message;
            _notifyIcon.BalloonTipIcon = icon;
            _notifyIcon.ShowBalloonTip(4000);
        }

        public NotifyIcon NotifyIcon => _notifyIcon;

        public void Dispose()
        {
            Utils.LogDebug("TrayIcon", "Disposing TrayIcon");
            try
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    _blinkTimer?.Stop();
                });
            }
            catch (Exception ex)
            {
                Utils.LogDebug("TrayIcon", $"Error stopping timer during disposal: {ex.Message}");
            }
            _primaryIcon?.Dispose();
            _secondaryIcon?.Dispose();
            _defaultIcon?.Dispose();
            _notifyIcon?.Dispose();
        }
    }
}
