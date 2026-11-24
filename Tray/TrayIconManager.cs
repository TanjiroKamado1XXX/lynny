using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using LmenV2.Helpers;
using LmenV2.Services;
using DrawingColor = System.Drawing.Color;

namespace LmenV2.Tray
{
    public class TrayIconManager : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly ContextMenuStrip _menu;
        private readonly ToolStripMenuItem _dualDisplayItem;
        private readonly ToolStripMenuItem _swapDisplaysItem;
        private readonly SettingsService _settings = SettingsService.Instance;
        private bool _hasMultipleDisplays;

        // Icon.FromHandle does NOT take ownership of the HICON, so the handle returned by
        // Bitmap.GetHicon has to be destroyed manually or it leaks for the process lifetime.
        private IntPtr _hIcon = IntPtr.Zero;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public event Action? OpenFullscreenRequested;
        public event Action? OpenOverlayRequested;
        public event Action? OpenSettingsRequested;
        public event Action? ExitRequested;

        public TrayIconManager()
        {
            _menu = new ContextMenuStrip();
            _menu.Items.Add("📺 Fullscreen Visualizer", null, (s, e) => OpenFullscreenRequested?.Invoke());
            _menu.Items.Add("🗗 Desktop Edge Overlay", null, (s, e) => OpenOverlayRequested?.Invoke());
            _dualDisplayItem = new ToolStripMenuItem("Dual Display Lyrics")
            {
                CheckOnClick = true
            };
            _dualDisplayItem.Click += (s, e) => _settings.DualDisplayModeEnabled = _dualDisplayItem.Checked;
            _swapDisplaysItem = new ToolStripMenuItem("Swap Cover / Lyrics Displays");
            _swapDisplaysItem.Click += (s, e) => _settings.SwapDualDisplays = !_settings.SwapDualDisplays;
            _menu.Items.Add(_dualDisplayItem);
            _menu.Items.Add(_swapDisplaysItem);
            _menu.Items.Add("⚙ Settings", null, (s, e) => OpenSettingsRequested?.Invoke());
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add("❌ Exit Nyx", null, (s, e) => ExitRequested?.Invoke());
            _menu.Opening += (s, e) => RefreshDualDisplayItems();
            _settings.SettingsChanged += OnSettingsChanged;
            RefreshDualDisplayItems();

            _notifyIcon = new NotifyIcon
            {
                Icon = CreateGlowIcon(),
                Text = "Nyx - Ambient Music Glow",
                Visible = true,
                ContextMenuStrip = _menu
            };

            _notifyIcon.DoubleClick += (s, e) => OpenFullscreenRequested?.Invoke();
        }

        private void OnSettingsChanged() => UpdateDualDisplaySettings();

        private void RefreshDualDisplayItems()
        {
            _hasMultipleDisplays = Win32Helper.GetMonitors().Count >= 2;
            UpdateDualDisplaySettings();
        }

        private void UpdateDualDisplaySettings()
        {
            _dualDisplayItem.Enabled = _hasMultipleDisplays || _settings.DualDisplayModeEnabled;
            _dualDisplayItem.Checked = _settings.DualDisplayModeEnabled;
            _dualDisplayItem.ToolTipText = _hasMultipleDisplays
                ? "Show album artwork and synchronized lyrics on separate displays"
                : "Connect a second display to enable dual display mode";
            _swapDisplaysItem.Enabled = _hasMultipleDisplays;
            _swapDisplaysItem.Checked = _settings.SwapDualDisplays;
        }

        private Icon CreateGlowIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(DrawingColor.Transparent);

                // Outer ambient Nyx glow
                using (var glowBrush = new SolidBrush(DrawingColor.FromArgb(130, 101, 72, 245)))
                {
                    g.FillEllipse(glowBrush, 2, 2, 28, 28);
                }

                // Inner bright cyan core
                using (var coreBrush = new SolidBrush(DrawingColor.FromArgb(255, 89, 205, 255)))
                {
                    g.FillEllipse(coreBrush, 8, 8, 16, 16);
                }

                // Center highlight
                using (var centerBrush = new SolidBrush(DrawingColor.White))
                {
                    g.FillEllipse(centerBrush, 12, 12, 8, 8);
                }
            }

            _hIcon = bmp.GetHicon();
            return Icon.FromHandle(_hIcon);
        }

        public void Dispose()
        {
            _settings.SettingsChanged -= OnSettingsChanged;

            // Visible=false first: removing the icon from the notification area before the
            // handle goes away is what prevents the "ghost tray icon" that lingers on the
            // taskbar until the next explorer refresh.
            try { _notifyIcon.Visible = false; } catch { }

            try { _notifyIcon.Dispose(); } catch { }
            try { _menu.Dispose(); } catch { }

            if (_hIcon != IntPtr.Zero)
            {
                try { DestroyIcon(_hIcon); } catch { }
                _hIcon = IntPtr.Zero;
            }
        }
    }
}
