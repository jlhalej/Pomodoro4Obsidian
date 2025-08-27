using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using System.Runtime.InteropServices;

namespace PomodoroForObsidian
{
    public class TrayIcon : IDisposable
    {
        private NotifyIcon _notifyIcon;
        private ToolStripMenuItem _miniWindowMenuItem;
        private ToolStripMenuItem _settingsMenuItem;
        
        // Fields for blinking icon functionality
        private DispatcherTimer _blinkTimer;
        private bool _isBlinking = false;
        private bool _isFirstColor = true;
        private Icon _defaultIcon;
        private Icon _primaryIcon;
        private Icon _secondaryIcon;

        // Colors based on the screenshot - purple theme with dark background
        private static readonly Color PrimaryColor = Color.FromArgb(169, 130, 210); // Purple color
        private static readonly Color SecondaryColor = Color.FromArgb(20, 20, 20);  // Dark background
        private static readonly Color PlayButtonColor = Color.FromArgb(169, 130, 210); // Purple for play button

        // For icon cleanup
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        extern static bool DestroyIcon(IntPtr handle);

        public TrayIcon()
        {
            Utils.LogDebug("TrayIcon", "Initializing TrayIcon");
            _notifyIcon = new NotifyIcon();
            
            // Use custom tray icon
            try
            {
                // Load icon from the same directory as the executable
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "timer16.ico");
                Utils.LogDebug("TrayIcon", $"Loading icon from: {iconPath}, File exists: {File.Exists(iconPath)}");
                _notifyIcon.Icon = new Icon(iconPath);
                _defaultIcon = new Icon(iconPath); // Create a separate instance to avoid disposal issues
                Utils.LogDebug("TrayIcon", "Default icon loaded successfully");
            }
            catch (Exception ex)
            {
                // Fallback to default icon if loading fails
                Utils.LogDebug("TrayIcon", $"Failed to load tray icon: {ex.Message}");
                _notifyIcon.Icon = SystemIcons.Application;
                _defaultIcon = SystemIcons.Application;
            }
            
            _notifyIcon.Visible = true;
            
            // Get the application version
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            var versionString = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "unknown";
            
            // Set tooltip with version
            _notifyIcon.Text = $"PomodoroForObsidian v{versionString}";
            Utils.LogDebug("TrayIcon", $"Set tray icon tooltip to: {_notifyIcon.Text}");
            
            _notifyIcon.ContextMenuStrip = new ContextMenuStrip();

            _miniWindowMenuItem = new ToolStripMenuItem("Mini Mode") { CheckOnClick = true };
            _settingsMenuItem = new ToolStripMenuItem("Settings");
            _miniWindowMenuItem.Click += (s, e) => ToggleMiniWindow();
            _settingsMenuItem.Click += (s, e) => ShowSettingsWindow();

            _notifyIcon.ContextMenuStrip.Items.Add(_miniWindowMenuItem);
            _notifyIcon.ContextMenuStrip.Items.Add(_settingsMenuItem);
            _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            _notifyIcon.ContextMenuStrip.Items.Add("Exit", null, (s, e) => ExitApp());
            _notifyIcon.ContextMenuStrip.Opening += (s, e) => UpdateMenuChecks();

            // Add left-click event to show mini window
            _notifyIcon.MouseClick += NotifyIcon_MouseClick;
            
            // Pre-create color icons
            try 
            {
                _primaryIcon = CreateColorIcon(PrimaryColor, true); // Purple with play icon
                _secondaryIcon = CreateColorIcon(SecondaryColor, false); // Dark background
                Utils.LogDebug("TrayIcon", "Pre-created color icons successfully");
            }
            catch (Exception ex) 
            {
                Utils.LogDebug("TrayIcon", $"Error creating color icons: {ex.Message}");
            }
            
            // Initialize timer on UI thread
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                // Create the timer on the UI thread
                _blinkTimer = new DispatcherTimer(DispatcherPriority.Normal, System.Windows.Application.Current.Dispatcher);
                _blinkTimer.Interval = TimeSpan.FromMilliseconds(500); // Blink every 500ms
                _blinkTimer.Tick += BlinkTimer_Tick;
                Utils.LogDebug("TrayIcon", "Blinking timer initialized on UI thread");
            });

            // Subscribe to Pomodoro session events to control blinking
            PomodoroSessionManager.Instance.ReverseCountdownStarted += (s, e) => {
                Utils.LogDebug("TrayIcon", "ReverseCountdownStarted event triggered, starting blinking");
                StartBlinking();
            };
            PomodoroSessionManager.Instance.Stopped += (s, e) => {
                Utils.LogDebug("TrayIcon", "Stopped event triggered, stopping blinking");
                StopBlinking();
            };
            PomodoroSessionManager.Instance.Reset += (s, e) => {
                Utils.LogDebug("TrayIcon", "Reset event triggered, stopping blinking");
                StopBlinking();
            };
            
            // Start blinking immediately for debugging (on UI thread)
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                Utils.LogDebug("TrayIcon", "Starting blinking immediately for debugging");
                StartBlinking();
            }));
        }

        private void BlinkTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                if (_isBlinking)
                {
                    _isFirstColor = !_isFirstColor;
                    // Removed excessive logging here
                    
                    // Force update on UI thread to be safe
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
                // Create a bitmap with the specified color
                using (Bitmap bitmap = new Bitmap(16, 16))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        g.Clear(backgroundColor);
                        
                        // Draw a rounded rectangle for the icon background
                        using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath())
                        {
                            path.AddRectangle(new Rectangle(0, 0, 15, 15));
                            using (Pen borderPen = new Pen(Color.FromArgb(40, 40, 40), 1))
                            {
                                g.DrawPath(borderPen, path);
                            }
                        }
                        
                        // Draw a play triangle icon if requested
                        if (includePlayIcon)
                        {
                            // Create a triangular play icon - use explicit System.Drawing.Point
                            System.Drawing.Point[] playTriangle = new System.Drawing.Point[]
                            {
                                new System.Drawing.Point(5, 3),     // Top point
                                new System.Drawing.Point(5, 13),    // Bottom point
                                new System.Drawing.Point(12, 8)     // Right point
                            };
                            
                            // Fill with a contrasting color
                            using (SolidBrush brush = new SolidBrush(Color.White))
                            {
                                g.FillPolygon(brush, playTriangle);
                            }
                        }
                    }
                    
                    // Create icon from bitmap
                    IntPtr hIcon = bitmap.GetHicon();
                    Icon icon = Icon.FromHandle(hIcon);
                    
                    // We need to clone the icon since FromHandle doesn't create a copy
                    // and the handle would be destroyed when this method exits
                    Icon clonedIcon = (Icon)icon.Clone();
                    
                    // Clean up the handle
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
        
        /// <summary>
        /// Starts blinking the tray icon between primary and secondary colors
        /// </summary>
        public void StartBlinking()
        {
            Utils.LogDebug("TrayIcon", "StartBlinking called");
            
            try
            {
                // Ensure we're on the UI thread
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
        
        /// <summary>
        /// Stops the blinking and restores the default icon
        /// </summary>
        public void StopBlinking()
        {
            Utils.LogDebug("TrayIcon", "StopBlinking called");
            
            try
            {
                // Ensure we're on the UI thread
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
                        
                        // Reset to the default icon
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
                
                // Stop blinking when user acknowledges by clicking on the icon
                if (_isBlinking && PomodoroSessionManager.Instance.IsReverseCountdown)
                {
                    Utils.LogDebug("TrayIcon", "Stopping blinking due to mouse click during reverse countdown");
                    StopBlinking();
                }
            }
        }

        private void UpdateMenuChecks()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var app = System.Windows.Application.Current as PomodoroForObsidian.App;
                if (app == null) { _miniWindowMenuItem.Checked = false; return; }
                var miniWindow = app.GetType().GetField("_miniWindow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(app) as PomodoroForObsidian.MiniWindow;
                _miniWindowMenuItem.Checked = miniWindow != null && miniWindow.IsVisible;
            });
        }

        private void ToggleMiniWindow()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var app = System.Windows.Application.Current as PomodoroForObsidian.App;
                if (app == null) return;
                var miniWindow = app.GetType().GetField("_miniWindow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(app) as PomodoroForObsidian.MiniWindow;
                var settings = app.GetType().GetField("_settings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(app) as PomodoroForObsidian.AppSettings;
                if (miniWindow != null && miniWindow.IsVisible)
                {
                    miniWindow.Hide();
                    if (settings != null)
                    {
                        settings.MiniModeActive = false;
                        settings.Save();
                    }
                }
                else
                {
                    if (miniWindow == null && settings != null)
                    {
                        miniWindow = new PomodoroForObsidian.MiniWindow(settings);
                        app.GetType().GetField("_miniWindow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(app, miniWindow);
                        var wireMethod = app.GetType().GetMethod("WireMiniWindowEvents", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        wireMethod?.Invoke(app, null);
                    }
                    miniWindow?.Show();
                    if (settings != null)
                    {
                        settings.MiniModeActive = true;
                        settings.Save();
                    }
                }
            });
        }

        private void ShowMiniWindow()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var app = System.Windows.Application.Current as PomodoroForObsidian.App;
                if (app == null) return;
                var miniWindow = app.GetType().GetField("_miniWindow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(app) as PomodoroForObsidian.MiniWindow;
                var settings = app.GetType().GetField("_settings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(app) as PomodoroForObsidian.AppSettings;
                if (miniWindow == null && settings != null)
                {
                    miniWindow = new PomodoroForObsidian.MiniWindow(settings);
                    app.GetType().GetField("_miniWindow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(app, miniWindow);
                    var wireMethod = app.GetType().GetMethod("WireMiniWindowEvents", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    wireMethod?.Invoke(app, null);
                }
                miniWindow?.Show();
                if (settings != null)
                {
                    settings.MiniModeActive = true;
                    settings.Save();
                }
            });
        }

        private void HideMiniWindow()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var app = System.Windows.Application.Current as PomodoroForObsidian.App;
                if (app == null) return;
                var miniWindow = app.GetType().GetField("_miniWindow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(app) as PomodoroForObsidian.MiniWindow;
                var settings = app.GetType().GetField("_settings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(app) as PomodoroForObsidian.AppSettings;
                if (miniWindow != null)
                {
                    miniWindow.Hide();
                    if (settings != null)
                    {
                        settings.MiniModeActive = false;
                        settings.Save();
                    }
                }
            });
        }

        private void ShowSettingsWindow()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var settingsWindow = new SettingsWindow();
                settingsWindow.Closed += (s, e) =>
                {
                    var app = System.Windows.Application.Current as PomodoroForObsidian.App;
                    var miniWindow = app?.GetType().GetField("_miniWindow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(app) as PomodoroForObsidian.MiniWindow;
                    var settings = PomodoroForObsidian.AppSettings.Load();
                    if (miniWindow != null && !PomodoroSessionManager.Instance.IsRunning && !PomodoroSessionManager.Instance.IsReverseCountdown)
                    {
                        miniWindow.SetTimer(TimeSpan.FromMinutes(settings.PomodoroTimerLength));
                    }
                };
                settingsWindow.Show(); // Not owned by any window
            });
        }

        private void ExitApp()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _notifyIcon.Visible = false;
                var app = System.Windows.Application.Current as PomodoroForObsidian.App;
                app?.ExitAppExplicit();
            });
        }

        public void ShowBalloon(string message, string title = "PomodoroForObsidian", ToolTipIcon icon = ToolTipIcon.Info)
        {
            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText = message;
            _notifyIcon.BalloonTipIcon = icon;
            _notifyIcon.ShowBalloonTip(4000); // Show for 4 seconds
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
