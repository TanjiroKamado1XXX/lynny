using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using LmenV2.Helpers;
using LmenV2.Services;
using LmenV2.Tray;
using LmenV2.Views;

namespace LmenV2
{
    public partial class App : Application
    {
        private static Mutex? _singleInstanceMutex;
        private static EventWaitHandle? _showWindowEvent;
        private MediaSessionService? _mediaService;
        private AudioReactiveService? _audioService;
        private TrayIconManager? _trayManager;

        private FullscreenVisualizerWindow? _fullscreenWindow;
        private SecondaryLyricsWindow? _secondaryLyricsWindow;
        private DesktopOverlayWindow? _overlayWindow;
        private bool _dualDisplayActive;
        private bool _lastDualDisplaySetting;
        private bool _lastSwapDualDisplaysSetting;
        private bool _settingsEventsSubscribed;
        private bool _displayEventsSubscribed;

        public static void Log(string msg)
        {
            try
            {
                string dir = AppContext.BaseDirectory;
                try
                {
                    string? procPath = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(procPath))
                    {
                        string? pDir = Path.GetDirectoryName(procPath);
                        if (!string.IsNullOrEmpty(pDir) && Directory.Exists(pDir))
                        {
                            dir = pDir;
                        }
                    }
                }
                catch { }

                string logPath = Path.Combine(dir, "app_debug.log");
                File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");
            }
            catch { }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            Log("App OnStartup begins");

            // Enforce Discrete / Standalone High-Performance GPU priority (NVIDIA RTX, AMD Radeon, etc.)
            if (GpuPreferenceService.EnsureHighPerformanceGpu(e.Args))
            {
                Shutdown();
                Environment.Exit(0);
                return;
            }

            // Ensure WPF hardware acceleration pipeline is active
            System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.Default;

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                Log($"[AppDomain UnhandledException] {args.ExceptionObject}");
            };

            DispatcherUnhandledException += (s, args) =>
            {
                Log($"[Dispatcher UnhandledException] {args.Exception}");
                args.Handled = true;
            };

            const string appGuid = "LmenV2_Ambient_Music_Companion_Unique";
            const string eventName = "LmenV2_Show_Window_Event";
            try
            {
                _singleInstanceMutex = new Mutex(true, appGuid, out bool isNewInstance);
                Log($"Mutex created. isNewInstance={isNewInstance}");
                if (!isNewInstance)
                {
                    Log("Another instance is running. Signaling existing instance and exiting.");
                    try
                    {
                        if (EventWaitHandle.TryOpenExisting(eventName, out var existingEvent))
                        {
                            existingEvent.Set();
                            existingEvent.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"Failed signaling existing instance: {ex.Message}");
                    }

                    _singleInstanceMutex.Dispose();
                    _singleInstanceMutex = null;
                    Shutdown();
                    return;
                }

                // Setup listener event for secondary launches
                _showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
                var listenerThread = new Thread(() =>
                {
                    while (_showWindowEvent.WaitOne())
                    {
                        if (_hasExited) break;
                        Dispatcher.BeginInvoke(() =>
                        {
                            Log("Show window event signaled from another instance.");
                            ShowFullscreenVisualizer();
                            if (_fullscreenWindow != null)
                            {
                                if (_fullscreenWindow.WindowState == WindowState.Minimized)
                                    _fullscreenWindow.WindowState = WindowState.Normal;
                                _fullscreenWindow.Activate();
                                _fullscreenWindow.Focus();
                            }
                        });
                    }
                })
                {
                    IsBackground = true,
                    Name = "LmenV2_SingleInstanceListener"
                };
                listenerThread.Start();
            }
            catch (Exception ex)
            {
                Log($"Mutex exception: {ex.Message}");
            }

            base.OnStartup(e);

            try
            {
                _lastDualDisplaySetting = SettingsService.Instance.DualDisplayModeEnabled;
                _lastSwapDualDisplaysSetting = SettingsService.Instance.SwapDualDisplays;
                SettingsService.Instance.SettingsChanged += OnSettingsChanged;
                _settingsEventsSubscribed = true;
                SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
                _displayEventsSubscribed = true;

                Log("Initializing MediaSessionService...");
                _mediaService = new MediaSessionService();

                Log("Initializing AudioReactiveService...");
                _audioService = new AudioReactiveService();

                Log("Initializing TrayIconManager...");
                _trayManager = new TrayIconManager();

                _trayManager.OpenFullscreenRequested += ShowFullscreenVisualizer;
                _trayManager.OpenOverlayRequested += ShowDesktopOverlay;
                _trayManager.OpenSettingsRequested += ShowSettings;
                _trayManager.ExitRequested += ExitApp;

                Log("Showing FullscreenVisualizerWindow...");
                ShowFullscreenVisualizer();

                Log("Starting AudioReactiveService...");
                _audioService.Start();

                Log("Initializing MediaSessionService async...");
                _ = _mediaService.InitializeAsync();

                Log("Startup completed successfully!");
            }
            catch (Exception ex)
            {
                Log($"[Startup Init Error] {ex}");
            }
        }

        public void ShowFullscreenVisualizer()
        {
            Dispatcher.Invoke(() =>
            {
                Log("ShowFullscreenVisualizer called");
                if (_overlayWindow != null)
                {
                    _overlayWindow.Hide();
                }

                if (_fullscreenWindow == null)
                {
                    _fullscreenWindow = new FullscreenVisualizerWindow(_mediaService!, _audioService!);
                    AttachPresentationWindow(_fullscreenWindow);
                    _fullscreenWindow.SecondaryLyricsToggleRequested += ToggleSecondaryLyrics;
                    _fullscreenWindow.SecondaryFullscreenToggleRequested += ToggleSecondaryFullscreen;

                    // The window can disappear without the app exiting (Alt+F4 while it is
                    // focused). Without clearing the reference, the next tray click would
                    // call Show() on a closed Window and throw
                    // "Cannot set Visibility or call Show/Activate after window has closed".
                    _fullscreenWindow.Closed += (s, e) =>
                    {
                        if (s is FullscreenVisualizerWindow window)
                        {
                            DetachPresentationWindow(window);
                            window.SecondaryLyricsToggleRequested -= ToggleSecondaryLyrics;
                            window.SecondaryFullscreenToggleRequested -= ToggleSecondaryFullscreen;
                        }
                        _fullscreenWindow = null;
                        _dualDisplayActive = false;
                        CloseSecondaryLyricsWindow();
                        UpdateOverlayRendering();
                    };
                }

                _fullscreenWindow.Show();
                _fullscreenWindow.Activate();
                Dispatcher.BeginInvoke(new Action(ApplyDualDisplayMode));
                UpdateOverlayRendering();
            });
        }

        public void ShowDesktopOverlay()
        {
            Dispatcher.Invoke(() =>
            {
                Log("ShowDesktopOverlay called");
                if (_fullscreenWindow != null)
                {
                    _fullscreenWindow.RestoreTaskbarToTopmost();
                    _fullscreenWindow.Hide();
                }

                if (_overlayWindow == null)
                {
                    _overlayWindow = new DesktopOverlayWindow(_mediaService!, _audioService!);
                    _overlayWindow.Closed += (s, e) => _overlayWindow = null;
                }

                _overlayWindow.Show();
                _overlayWindow.EnsureTaskbarAboveOverlay();
                ApplyDualDisplayMode();
                UpdateOverlayRendering();
            });
        }

        private void AttachPresentationWindow(Window window)
        {
            window.IsVisibleChanged += OnPresentationWindowVisibilityChanged;
            window.LocationChanged += OnPresentationWindowChanged;
            window.SizeChanged += OnPresentationWindowSizeChanged;
            window.StateChanged += OnPresentationWindowChanged;
        }

        private void DetachPresentationWindow(Window window)
        {
            window.IsVisibleChanged -= OnPresentationWindowVisibilityChanged;
            window.LocationChanged -= OnPresentationWindowChanged;
            window.SizeChanged -= OnPresentationWindowSizeChanged;
            window.StateChanged -= OnPresentationWindowChanged;
        }

        private void OnPresentationWindowVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) =>
            UpdateOverlayRendering();

        private void OnPresentationWindowChanged(object? sender, EventArgs e) =>
            UpdateOverlayRendering();

        private void OnPresentationWindowSizeChanged(object sender, SizeChangedEventArgs e) =>
            UpdateOverlayRendering();

        private void UpdateOverlayRendering()
        {
            if (_overlayWindow == null) return;

            var primaryMonitor = Win32Helper.GetMonitors().FirstOrDefault(monitor => monitor.IsPrimary);
            bool coveredByVisualizer = primaryMonitor != null &&
                _fullscreenWindow != null &&
                _fullscreenWindow.IsVisible &&
                _fullscreenWindow.WindowState != WindowState.Minimized &&
                !_fullscreenWindow.IsRestoredMode &&
                Win32Helper.IsWindowOnMonitor(_fullscreenWindow, primaryMonitor);
            bool coveredBySecondaryLyrics = primaryMonitor != null &&
                _secondaryLyricsWindow != null &&
                _secondaryLyricsWindow.IsVisible &&
                _secondaryLyricsWindow.WindowState != WindowState.Minimized &&
                !_secondaryLyricsWindow.IsWindowedPresentation &&
                Win32Helper.IsWindowOnMonitor(_secondaryLyricsWindow, primaryMonitor);

            _overlayWindow.SetRenderingPaused(coveredByVisualizer || coveredBySecondaryLyrics);
        }

        private void OnSettingsChanged()
        {
            if (_hasExited || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                bool dualDisplayEnabled = SettingsService.Instance.DualDisplayModeEnabled;
                bool swapDualDisplays = SettingsService.Instance.SwapDualDisplays;
                if (_lastDualDisplaySetting == dualDisplayEnabled &&
                    _lastSwapDualDisplaysSetting == swapDualDisplays)
                {
                    return;
                }

                _lastDualDisplaySetting = dualDisplayEnabled;
                _lastSwapDualDisplaysSetting = swapDualDisplays;
                ApplyDualDisplayMode();
            }));
        }

        private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            if (!_hasExited && !Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
            {
                Dispatcher.BeginInvoke(new Action(ApplyDualDisplayMode));
            }
        }

        private void ApplyDualDisplayMode()
        {
            if (_fullscreenWindow == null)
            {
                CloseSecondaryLyricsWindow();
                _dualDisplayActive = false;
                UpdateOverlayRendering();
                return;
            }

            var monitors = Win32Helper.GetMonitors();
            bool visualizerVisible = _fullscreenWindow.IsVisible &&
                (_overlayWindow == null || !_overlayWindow.IsVisible);

            if (!visualizerVisible || !SettingsService.Instance.DualDisplayModeEnabled || monitors.Count < 2)
            {
                bool wasActive = _dualDisplayActive;
                _dualDisplayActive = false;
                CloseSecondaryLyricsWindow();
                _fullscreenWindow.SetDualDisplayPresentation(false);
                if (wasActive && monitors.Count > 0 && _fullscreenWindow.IsVisible)
                {
                    var primary = monitors.FirstOrDefault(monitor => monitor.IsPrimary) ?? monitors[0];
                    TryPositionVisualizerOnMonitor(_fullscreenWindow, primary, "single display restore");
                }
                UpdateOverlayRendering();
                return;
            }

            var primaryMonitor = monitors.FirstOrDefault(monitor => monitor.IsPrimary) ?? monitors[0];
            var secondaryMonitor = monitors.First(monitor => monitor.DeviceName != primaryMonitor.DeviceName);
            DisplayMonitor coverMonitor = SettingsService.Instance.SwapDualDisplays
                ? secondaryMonitor
                : primaryMonitor;
            DisplayMonitor lyricsMonitor = SettingsService.Instance.SwapDualDisplays
                ? primaryMonitor
                : secondaryMonitor;

            try
            {
                _fullscreenWindow.SetDualDisplayPresentation(true, coverMonitor);
                if (_secondaryLyricsWindow == null)
                {
                    _secondaryLyricsWindow = new SecondaryLyricsWindow(_fullscreenWindow, _mediaService!, _audioService!, lyricsMonitor);
                    AttachPresentationWindow(_secondaryLyricsWindow);
                    _secondaryLyricsWindow.ExitRequested += OnSecondaryLyricsExitRequested;
                    _secondaryLyricsWindow.Closed += OnSecondaryLyricsClosed;
                }

                _secondaryLyricsWindow.SetMonitor(lyricsMonitor);
                if (!_secondaryLyricsWindow.IsVisible)
                {
                    _secondaryLyricsWindow.Show();
                }

                _dualDisplayActive = true;
                UpdateOverlayRendering();
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Log($"[Dual display placement] {ex.Message}");
                _dualDisplayActive = false;
                CloseSecondaryLyricsWindow();
                _fullscreenWindow.SetDualDisplayPresentation(false);
                var fallbackMonitor = Win32Helper.GetMonitors().FirstOrDefault(monitor => monitor.IsPrimary);
                if (fallbackMonitor != null && _fullscreenWindow.IsVisible)
                {
                    TryPositionVisualizerOnMonitor(_fullscreenWindow, fallbackMonitor, "dual display fallback placement");
                }
                UpdateOverlayRendering();
            }
        }

        private static void TryPositionVisualizerOnMonitor(
            FullscreenVisualizerWindow window,
            DisplayMonitor monitor,
            string operation)
        {
            try
            {
                window.MoveToMonitor(monitor);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Log($"[{operation}] {ex.Message}");
            }
        }

        private void OnSecondaryLyricsExitRequested()
        {
            SettingsService.Instance.DualDisplayModeEnabled = false;
        }

        private void OnSecondaryLyricsClosed(object? sender, EventArgs e)
        {
            if (sender is SecondaryLyricsWindow window)
            {
                DetachPresentationWindow(window);
                window.ExitRequested -= OnSecondaryLyricsExitRequested;
            }
            if (ReferenceEquals(sender, _secondaryLyricsWindow))
            {
                _secondaryLyricsWindow = null;
            }
            UpdateOverlayRendering();
        }

        private void CloseSecondaryLyricsWindow()
        {
            if (_secondaryLyricsWindow == null) return;
            var window = _secondaryLyricsWindow;
            _secondaryLyricsWindow = null;
            window.ExitRequested -= OnSecondaryLyricsExitRequested;
            window.Closed -= OnSecondaryLyricsClosed;
            window.CloseFromOwner();
        }

        private void ToggleSecondaryLyrics()
        {
            _secondaryLyricsWindow?.ToggleLyricsVisibilityFromPrimary();
        }

        private void ToggleSecondaryFullscreen()
        {
            _secondaryLyricsWindow?.ToggleFullscreenFromPrimary();
        }

        public bool IsOverlayModeActive =>
            _overlayWindow != null && _overlayWindow.IsVisible &&
            (_fullscreenWindow == null || !_fullscreenWindow.IsVisible);

        public void ShowSettings()
        {
            Dispatcher.Invoke(() =>
            {
                Log("ShowSettings called");
                var settings = new SettingsWindow();
                if (_fullscreenWindow != null && _fullscreenWindow.IsVisible)
                {
                    settings.Owner = _fullscreenWindow;
                }
                else if (_overlayWindow != null && _overlayWindow.IsVisible)
                {
                    settings.Owner = _overlayWindow;
                }
                settings.Topmost = true;
                settings.ShowDialog();
            });
        }

        private volatile bool _hasExited;

        public void ExitApp()
        {
            // ExitApp runs from the tray menu and again from OnExit (Shutdown() re-enters
            // it). Doing the teardown twice means releasing an unowned mutex and disposing
            // objects that are already gone.
            if (_hasExited) return;
            _hasExited = true;

            Log("ExitApp called");
            if (_settingsEventsSubscribed)
            {
                SettingsService.Instance.SettingsChanged -= OnSettingsChanged;
                _settingsEventsSubscribed = false;
            }
            if (_displayEventsSubscribed)
            {
                SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
                _displayEventsSubscribed = false;
            }

            // Windows first: their OnClosed handlers unsubscribe from the render loop and
            // from service events, which must happen before the services go away.
            try
            {
                _fullscreenWindow?.Close();
                CloseSecondaryLyricsWindow();
                _overlayWindow?.Close();
            }
            catch (Exception ex)
            {
                Log($"[ExitApp window close] {ex.Message}");
            }

            try { _audioService?.Dispose(); } catch (Exception ex) { Log($"[ExitApp audio] {ex.Message}"); }
            try { _mediaService?.Dispose(); } catch (Exception ex) { Log($"[ExitApp media] {ex.Message}"); }
            try { _trayManager?.Dispose(); } catch (Exception ex) { Log($"[ExitApp tray] {ex.Message}"); }
            try { Services.SystemVolumeService.Instance.Dispose(); } catch (Exception ex) { Log($"[ExitApp volume] {ex.Message}"); }
            try
            {
                _showWindowEvent?.Set();
                _showWindowEvent?.Dispose();
            }
            catch { }

            try
            {
                _singleInstanceMutex?.ReleaseMutex();
            }
            catch (Exception ex)
            {
                Log($"[ExitApp mutex] {ex.Message}");
            }
            try { _singleInstanceMutex?.Dispose(); } catch { }

            Log("ExitApp teardown complete - shutting down");
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log("OnExit called");
            ExitApp();
            base.OnExit(e);
        }
    }
}
