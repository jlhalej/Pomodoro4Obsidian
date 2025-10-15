using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Drawing;
using Point = System.Windows.Point;

namespace PomodoroForObsidian
{
    public class TaskbarManager
    {
        #region Win32 API

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRectRgnIndirect([In] ref RECT lprc);

        [DllImport("gdi32.dll")]
        private static extern int CombineRgn(IntPtr hrgnDest, IntPtr hrgnSrc1, IntPtr hrgnSrc2, int fnCombineMode);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;

            public RECT(int left, int top, int right, int bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }

            public static implicit operator System.Drawing.Rectangle(RECT rect)
            {
                return new System.Drawing.Rectangle(rect.Left, rect.Top, rect.Width, rect.Height);
            }
        }

        // Window positioning flags
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOREDRAW = 0x0008;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOOWNERZORDER = 0x0200;
        private const uint SWP_ASYNCWINDOWPOS = 0x4000;

        // Region combining modes
        private const int RGN_AND = 1;
        private const int RGN_OR = 2;
        private const int RGN_XOR = 3;
        private const int RGN_DIFF = 4;
        private const int RGN_COPY = 5;

        // For SetWindowPos
        private static readonly IntPtr HWND_TOP = new IntPtr(0);
        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        #endregion

        // Class properties
        private IntPtr _taskbarHandle;
        private RECT _originalTaskbarRect;
        private bool _isModified = false;

        // Settings for taskbar modification
        public bool TaskbarModificationEnabled { get; set; } = false;
        public int TaskbarNotchWidth { get; set; } = 340;
        public int TaskbarNotchHeight { get; set; } = 36;
        public int TaskbarNotchPosition { get; set; } = 0; // 0 = center, negative = left offset, positive = right offset
        public int CornerRadius { get; set; } = 6;

        public TaskbarManager()
        {
            // Find the taskbar window
            _taskbarHandle = FindWindow("Shell_TrayWnd", string.Empty);
            
            if (_taskbarHandle != IntPtr.Zero)
            {
                // Store the original taskbar rectangle
                if (GetWindowRect(_taskbarHandle, out _originalTaskbarRect))
                {
                    Utils.LogDebug("TaskbarManager", $"Found taskbar at: {_originalTaskbarRect.Left}, {_originalTaskbarRect.Top}, {_originalTaskbarRect.Right}, {_originalTaskbarRect.Bottom}");
                }
                else
                {
                    Utils.LogDebug("TaskbarManager", "Found taskbar but could not get window rect");
                }
            }
            else
            {
                Utils.LogDebug("TaskbarManager", "Could not find taskbar window");
            }
        }

        /// <summary>
        /// Create a notch in the taskbar
        /// </summary>
        public bool CreateNotch()
        {
            if (_taskbarHandle == IntPtr.Zero || !TaskbarModificationEnabled)
                return false;

            try
            {
                // Get current taskbar rectangle
                if (!GetWindowRect(_taskbarHandle, out RECT taskbarRect))
                {
                    Utils.LogDebug("TaskbarManager", "Failed to get taskbar rect");
                    return false;
                }

                // Calculate position for the notch
                int taskbarWidth = taskbarRect.Width;
                int taskbarHeight = taskbarRect.Height;
                
                // Calculate center position and adjust by the TaskbarNotchPosition
                int notchX;
                if (TaskbarNotchPosition == 0)
                {
                    // Center
                    notchX = taskbarRect.Left + (taskbarWidth - TaskbarNotchWidth) / 2;
                }
                else if (TaskbarNotchPosition < 0)
                {
                    // Left offset (from center)
                    notchX = taskbarRect.Left + (taskbarWidth - TaskbarNotchWidth) / 2 + TaskbarNotchPosition;
                }
                else
                {
                    // Right offset (from center)
                    notchX = taskbarRect.Left + (taskbarWidth - TaskbarNotchWidth) / 2 + TaskbarNotchPosition;
                }

                // Create a region for the entire taskbar
                IntPtr taskbarRegion = CreateRectRgn(0, 0, taskbarWidth, taskbarHeight);

                // Create a region for the notch area
                // Note: coordinates are relative to the window, not the screen
                IntPtr notchRegion = CreateRectRgn(
                    notchX - taskbarRect.Left,
                    0,
                    notchX - taskbarRect.Left + TaskbarNotchWidth,
                    TaskbarNotchHeight);

                // Subtract the notch from the taskbar region
                IntPtr combinedRegion = CreateRectRgn(0, 0, 0, 0);
                CombineRgn(combinedRegion, taskbarRegion, notchRegion, RGN_DIFF);

                // Apply the new region to the taskbar
                _isModified = SetWindowRgn(_taskbarHandle, combinedRegion, true) != 0;

                // Clean up the region objects
                DeleteObject(taskbarRegion);
                DeleteObject(notchRegion);
                
                // Don't delete combinedRegion as it's now owned by the window

                Utils.LogDebug("TaskbarManager", $"Created notch at: {notchX}, width: {TaskbarNotchWidth}, height: {TaskbarNotchHeight}, success: {_isModified}");

                return _isModified;
            }
            catch (Exception ex)
            {
                Utils.LogDebug("TaskbarManager", $"Error creating notch: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Remove the notch and restore the original taskbar
        /// </summary>
        public bool RestoreTaskbar()
        {
            if (_taskbarHandle == IntPtr.Zero || !_isModified)
                return false;

            try
            {
                // Reset the window region
                SetWindowRgn(_taskbarHandle, IntPtr.Zero, true);
                
                // Make the taskbar redraw itself
                SetWindowPos(
                    _taskbarHandle, 
                    IntPtr.Zero, 
                    0, 0, 0, 0, 
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
                
                _isModified = false;
                Utils.LogDebug("TaskbarManager", "Restored taskbar");
                
                return true;
            }
            catch (Exception ex)
            {
                Utils.LogDebug("TaskbarManager", $"Error restoring taskbar: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the ideal position for the mini window inside the taskbar notch
        /// </summary>
        /// <returns>Point with coordinates for positioning the window</returns>
        public Point GetIdealMiniWindowPosition(double windowWidth, double windowHeight)
        {
            Utils.LogDebug("TaskbarManager", $"GetIdealMiniWindowPosition called - Window size: {windowWidth}x{windowHeight}");
            
            if (_taskbarHandle == IntPtr.Zero)
            {
                Utils.LogDebug("TaskbarManager", "Taskbar handle is null, returning (0,0)");
                return new Point(0, 0);
            }

            GetWindowRect(_taskbarHandle, out RECT taskbarRect);
            Utils.LogDebug("TaskbarManager", $"Taskbar rect: Left={taskbarRect.Left}, Top={taskbarRect.Top}, Right={taskbarRect.Right}, Bottom={taskbarRect.Bottom}, Width={taskbarRect.Width}, Height={taskbarRect.Height}");
            
            // Calculate position for the mini window
            int taskbarWidth = taskbarRect.Width;
            
            // Calculate center position and adjust by the TaskbarNotchPosition
            int notchX;
            if (TaskbarNotchPosition == 0)
            {
                // Center
                notchX = taskbarRect.Left + (taskbarWidth - TaskbarNotchWidth) / 2;
                Utils.LogDebug("TaskbarManager", $"Centered notch X position: {notchX}");
            }
            else if (TaskbarNotchPosition < 0)
            {
                // Left offset (from center)
                notchX = taskbarRect.Left + (taskbarWidth - TaskbarNotchWidth) / 2 + TaskbarNotchPosition;
                Utils.LogDebug("TaskbarManager", $"Left-offset notch X position: {notchX}, offset: {TaskbarNotchPosition}");
            }
            else
            {
                // Right offset (from center)
                notchX = taskbarRect.Left + (taskbarWidth - TaskbarNotchWidth) / 2 + TaskbarNotchPosition;
                Utils.LogDebug("TaskbarManager", $"Right-offset notch X position: {notchX}, offset: {TaskbarNotchPosition}");
            }
            
            // Center the window in the notch
            int windowX = notchX + (TaskbarNotchWidth - (int)windowWidth) / 2;
            
            // Position the window inside the notch
            // For Windows taskbar at the bottom of the screen, the notch creates an empty space at the top of the taskbar
            // So we position the window at the taskbar's top position
            int windowY = taskbarRect.Top;

            // Log the positioning details
            Utils.LogDebug("TaskbarManager", $"Positioning mini window at: X={windowX}, Y={windowY}");
            Utils.LogDebug("TaskbarManager", $"Notch details: Width={TaskbarNotchWidth}, Height={TaskbarNotchHeight}, Position={TaskbarNotchPosition}");
            
            return new Point(windowX, windowY);
        }

        /// <summary>
        /// Get the screen coordinates of the notch in the taskbar
        /// </summary>
        public Rect GetNotchRect()
        {
            if (_taskbarHandle == IntPtr.Zero)
                return new Rect();

            GetWindowRect(_taskbarHandle, out RECT taskbarRect);
            
            int taskbarWidth = taskbarRect.Width;
            
            int notchX;
            if (TaskbarNotchPosition == 0)
            {
                // Center
                notchX = taskbarRect.Left + (taskbarWidth - TaskbarNotchWidth) / 2;
            }
            else if (TaskbarNotchPosition < 0)
            {
                // Left offset
                notchX = taskbarRect.Left + (taskbarWidth - TaskbarNotchWidth) / 2 + TaskbarNotchPosition;
            }
            else
            {
                // Right offset
                notchX = taskbarRect.Left + (taskbarWidth - TaskbarNotchWidth) / 2 + TaskbarNotchPosition;
            }
            
            return new Rect(notchX, taskbarRect.Top, TaskbarNotchWidth, TaskbarNotchHeight);
        }
    }
}