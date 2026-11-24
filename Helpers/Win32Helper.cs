using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;

namespace LmenV2.Helpers
{
    public sealed class DisplayMonitor
    {
        public string DeviceName { get; }
        public System.Drawing.Rectangle Bounds { get; }
        public bool IsPrimary { get; }
        public uint DpiX { get; }
        public uint DpiY { get; }
        public double ScaleX => DpiX / 96.0;
        public double ScaleY => DpiY / 96.0;

        internal DisplayMonitor(string deviceName, System.Drawing.Rectangle bounds, bool isPrimary, uint dpiX, uint dpiY)
        {
            DeviceName = deviceName;
            Bounds = bounds;
            IsPrimary = isPrimary;
            DpiX = dpiX;
            DpiY = dpiY;
        }
    }

    public static class Win32Helper
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int MDT_EFFECTIVE_DPI = 0;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_NOZORDER = 0x0004;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;

            public NativePoint(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;

        public static IReadOnlyList<DisplayMonitor> GetMonitors()
        {
            return System.Windows.Forms.Screen.AllScreens
                .Select(screen =>
                {
                    var bounds = screen.Bounds;
                    var center = new NativePoint(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
                    uint dpiX = 96;
                    uint dpiY = 96;
                    try
                    {
                        IntPtr monitor = MonitorFromPoint(center, MONITOR_DEFAULTTONEAREST);
                        if (monitor != IntPtr.Zero &&
                            GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out uint detectedX, out uint detectedY) == 0)
                        {
                            dpiX = detectedX > 0 ? detectedX : 96;
                            dpiY = detectedY > 0 ? detectedY : 96;
                        }
                    }
                    catch (DllNotFoundException) { }
                    catch (EntryPointNotFoundException) { }

                    return new DisplayMonitor(screen.DeviceName, bounds, screen.Primary, dpiX, dpiY);
                })
                .OrderByDescending(monitor => monitor.IsPrimary)
                .ThenBy(monitor => monitor.Bounds.Top)
                .ThenBy(monitor => monitor.Bounds.Left)
                .ThenBy(monitor => monitor.DeviceName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static bool IsWindowOnMonitor(Window window, DisplayMonitor monitor)
        {
            ArgumentNullException.ThrowIfNull(window);
            ArgumentNullException.ThrowIfNull(monitor);

            IntPtr handle = new WindowInteropHelper(window).Handle;
            return handle != IntPtr.Zero &&
                string.Equals(Screen.FromHandle(handle).DeviceName, monitor.DeviceName, StringComparison.OrdinalIgnoreCase);
        }

        public static void PositionWindowOnMonitor(Window window, DisplayMonitor monitor, bool topmost = false)
        {
            ArgumentNullException.ThrowIfNull(window);
            ArgumentNullException.ThrowIfNull(monitor);

            window.WindowState = WindowState.Normal;
            window.Topmost = topmost;
            window.Width = Math.Max(1.0, monitor.Bounds.Width / monitor.ScaleX);
            window.Height = Math.Max(1.0, monitor.Bounds.Height / monitor.ScaleY);

            IntPtr handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
            {
                throw new InvalidOperationException("The window handle must exist before monitor placement.");
            }

            uint flags = SWP_NOACTIVATE | SWP_FRAMECHANGED;
            IntPtr insertAfter = IntPtr.Zero;
            if (topmost)
            {
                insertAfter = HWND_TOPMOST;
            }
            else
            {
                flags |= SWP_NOZORDER;
            }

            if (!SetWindowPos(
                    handle,
                    insertAfter,
                    monitor.Bounds.Left,
                    monitor.Bounds.Top,
                    monitor.Bounds.Width,
                    monitor.Bounds.Height,
                    flags))
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        /// <summary>
        /// Makes a WPF window completely click-through (clicks pass to windows underneath).
        /// </summary>
        public static void SetClickThrough(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW);

            // SWP_NOACTIVATE (not SWP_SHOWWINDOW): this runs from Window_Loaded while the
            // overlay is being revealed. Forcing activation yanks keyboard focus away from
            // whatever the user was doing (Spotify, a game, a IDE) the moment the overlay
            // appears, which is exactly what an ambient always-on-top layer must never do.
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        /// <summary>
        /// Restores interactive click handling for a window.
        /// </summary>
        public static void RemoveClickThrough(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);
        }
    }
}
