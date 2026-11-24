using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using LmenV2.Helpers;
using LmenV2.Models;
using LmenV2.Rendering;
using LmenV2.Rendering.Kinetic;
using LmenV2.Services;

namespace LmenV2.Views
{
    public partial class SecondaryLyricsWindow : Window
    {
        private static readonly ExtractedPalette FallbackPalette = new ExtractedPalette
        {
            DominantColor = Color.FromRgb(124, 92, 252),
            AccentColor = Color.FromRgb(217, 91, 244),
            SecondaryColor = Color.FromRgb(100, 244, 196),
            AmbientColor = Color.FromRgb(27, 21, 57),
            DarkVignetteColor = Color.FromRgb(7, 9, 16)
        };

        private readonly FullscreenVisualizerWindow _primaryWindow;
        private readonly MediaSessionService _mediaService;
        private readonly AudioReactiveService _audioService;
        private readonly SettingsService _settings = SettingsService.Instance;
        private readonly KineticStageRenderer _kineticRenderer;
        private readonly ChromaticSplitEffect _displayAdjustmentEffect = new ChromaticSplitEffect();
        private readonly LyricClockReconciler _lyricClock = new LyricClockReconciler();
        private TimeSpan _lastRenderingTime = TimeSpan.MinValue;
        private readonly EdgeBeam _beam;
        private System.Windows.Threading.DispatcherTimer? _idleTimer;

        private DisplayMonitor _monitor;
        private ExtractedPalette _currentPalette = FallbackPalette;
        private SecondaryGlowColorMode _glowColorMode;
        private string _glowCustomColor = "#7C5CFC";
        private double _glowBrightness;
        private double _glowSpread;
        private double _glowRotationSpeed;
        private double _glowBreathingAmount;
        private double _glowAudioReactivity;
        private double _currentGlowSpeed;
        private double _glowBreathingPhase;
        private TrackLyrics? _lastLyrics;
        private string _lastTitle = string.Empty;
        private string _lastArtist = string.Empty;
        private string _lastTransformStatus = string.Empty;
        private KineticTemplate _lastTemplate = KineticTemplate.Auto;
        private Color _lastDominant = Colors.Transparent;
        private Color _lastAccent = Colors.Transparent;
        private Color _lastSecondary = Colors.Transparent;
        private Color _lastAmbient = Colors.Transparent;
        private Color _lastVignette = Colors.Transparent;
        private BitmapSource? _lastThumbnail;
        private TimeSpan _displayedLyricPosition;
        private TrackLyrics? _lastVisibilityLyrics;
        private bool _hasClockPosition;
        private bool _hasAppliedLyricsVisibility;
        private bool _lastLyricsVisibilityToggle = true;
        private bool _lyricsVisible = true;
        private bool _isWindowed;
        private bool _isClosingFromOwner;
        private bool _isLanguagePickerInitialized;
        private bool _hasAppliedGlowSettings;
        private bool _appliedLowLatencyMode;
        private bool _hasMousePosition;
        private bool _suppressScreenSettingsUpdate;
        private bool _isDisplaySettingsPopupOpen;
        private Point _lastMousePosition;

        public event Action? ExitRequested;
        public bool IsWindowedPresentation => _isWindowed;

        public SecondaryLyricsWindow(
            FullscreenVisualizerWindow primaryWindow,
            MediaSessionService mediaService,
            AudioReactiveService audioService,
            DisplayMonitor monitor)
        {
            _primaryWindow = primaryWindow ?? throw new ArgumentNullException(nameof(primaryWindow));
            _mediaService = mediaService ?? throw new ArgumentNullException(nameof(mediaService));
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));

            InitializeComponent();
            _kineticRenderer = new KineticStageRenderer(KineticLyricsCanvas);
            _appliedLowLatencyMode = _settings.LowLatencyMode;
            _kineticRenderer.SetLowLatencyMode(_appliedLowLatencyMode);
            _beam = new EdgeBeam(EdgeBeamHost);
            SecondaryVisualsRoot.Effect = _displayAdjustmentEffect;
            BackdropImage.Source = BackdropBlur.GetFallbackBackdrop();
        }

        public void SetMonitor(DisplayMonitor monitor)
        {
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            if (!IsLoaded) return;

            if (_isWindowed)
            {
                PlaceWindowed();
            }
            else
            {
                Win32Helper.PositionWindowOnMonitor(this, _monitor, topmost: true);
            }
        }

        public void CloseFromOwner()
        {
            _isClosingFromOwner = true;
            Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _lastRenderingTime = TimeSpan.MinValue;
            Win32Helper.PositionWindowOnMonitor(this, _monitor, topmost: true);
            PopulateLanguagePicker();
            ApplySecondaryGlowSettings();
            ApplySecondaryScreenSettings();
            UpdateSecondaryScreenSettingsPopup();
            _settings.SettingsChanged += OnSettingsChanged;
            OffsetValueText.Text = FormatOffset(_settings.LyricsOffsetMs);
            CompositionTarget.Rendering += OnRenderFrame;
            SyncPrimaryState();
            RevealActionBar();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _beam.SetGeometry(ActualWidth, ActualHeight, _glowSpread > 0.0 ? _glowSpread : 110.0);
        }

        private void OnRenderFrame(object? sender, EventArgs e)
        {
            if (!IsVisible || WindowState == WindowState.Minimized) return;
            if (!RenderFrameClock.TryGetDeltaTime(e, ref _lastRenderingTime, out double dt)) return;

            SyncPrimaryState();

            bool isPlaying = _primaryWindow.IsPlaybackPlaying;
            TimeSpan reportedPosition = _primaryWindow.CurrentLyricPosition;
            if (!_hasClockPosition)
            {
                _displayedLyricPosition = reportedPosition;
                _lyricClock.Reset(reportedPosition, isPlaying);
                _hasClockPosition = true;
            }
            else
            {
                if (isPlaying)
                {
                    _displayedLyricPosition += TimeSpan.FromSeconds(dt);
                }
                _displayedLyricPosition = _lyricClock.Reconcile(
                    _displayedLyricPosition,
                    reportedPosition,
                    isPlaying);
            }

            if (_lyricsVisible && KineticLyricsCanvas.Visibility == Visibility.Visible)
            {
                _kineticRenderer.UpdateFrame(_displayedLyricPosition, dt);
            }

            float energy = _primaryWindow.IsAudioReactivityEnabled
                ? _audioService.CurrentEnergySnapshot
                : 0f;
            float bass = _primaryWindow.IsAudioReactivityEnabled
                ? _audioService.CurrentBassEnergy
                : 0f;
            float pulse = _primaryWindow.IsAudioReactivityEnabled
                ? _audioService.CurrentBeatPulse
                : 0f;
            double targetSpeed = _glowRotationSpeed <= 0.0
                ? 0.0
                : (80.0 + pulse * 720.0) * _glowRotationSpeed;
            double speedDamping = targetSpeed > _currentGlowSpeed ? 14.0 : 5.0;
            _currentGlowSpeed += (targetSpeed - _currentGlowSpeed) *
                (1.0 - Math.Exp(-speedDamping * dt));
            _beam.AdvancePhase(_currentGlowSpeed * dt);

            _glowBreathingPhase += dt * GlowProfile.BreathOmega;
            if (_glowBreathingPhase >= Math.PI * 2.0) _glowBreathingPhase -= Math.PI * 2.0;
            double breath = GlowProfile.Breathing(_glowBreathingPhase, _glowBreathingAmount);
            double reactiveEnergy = Math.Clamp(Math.Max(energy, bass) * _glowAudioReactivity, 0.0, 1.0);
            double reactiveBrightness = _glowBrightness * (1.0 + Math.Clamp(pulse * _glowAudioReactivity, 0.0, 1.0) * 0.35);
            _beam.Render(reactiveBrightness, breath, reactiveEnergy);
            _beam.EaseColor(dt);
        }

        private void SyncPrimaryState()
        {
            MediaTrackInfo? track = _primaryWindow.CurrentTrack;
            TrackLyrics? lyrics = _primaryWindow.CurrentLyrics;
            string title = track?.Title ?? "Ready to Play";
            string artist = track?.Artist ?? "Play music to begin";
            BitmapSource? thumbnail = track?.Thumbnail;
            bool hasArtwork = thumbnail != null;
            Color dominant = hasArtwork ? track!.DominantColor : FallbackPalette.DominantColor;
            Color accent = hasArtwork ? track!.AccentColor : FallbackPalette.AccentColor;
            Color secondary = hasArtwork ? track!.SecondaryColor : FallbackPalette.SecondaryColor;
            Color ambient = hasArtwork ? track!.AmbientColor : FallbackPalette.AmbientColor;
            Color vignette = hasArtwork ? track!.DarkVignetteColor : FallbackPalette.DarkVignetteColor;
            bool paletteChanged = !ReferenceEquals(_lastThumbnail, thumbnail);
            if (paletteChanged)
            {
                _lastThumbnail = thumbnail;
                _currentPalette = thumbnail != null
                    ? ColorExtractionService.ExtractPalette(thumbnail)
                    : FallbackPalette;
                BackdropImage.Source = thumbnail != null
                    ? CreateBlurredBackdrop(thumbnail) ?? BackdropBlur.GetFallbackBackdrop()
                    : BackdropBlur.GetFallbackBackdrop();
                ApplyButtonAccent(_currentPalette.AccentColor);
            }

            KineticTemplate template = _primaryWindow.CurrentLyricsTemplate;

            bool lyricChanged = !ReferenceEquals(_lastLyrics, lyrics);
            bool titleChanged = !string.Equals(_lastTitle, title, StringComparison.Ordinal);
            bool visualChanged = lyricChanged ||
                titleChanged ||
                !string.Equals(_lastArtist, artist, StringComparison.Ordinal) ||
                dominant != _lastDominant ||
                accent != _lastAccent ||
                secondary != _lastSecondary ||
                ambient != _lastAmbient ||
                vignette != _lastVignette ||
                paletteChanged ||
                template != _lastTemplate;

            if (visualChanged)
            {
                _kineticRenderer.SetLyrics(lyrics, title, artist, dominant, accent);
                _kineticRenderer.SetTemplateChoice(template);
                _lastLyrics = lyrics;
                _lastTitle = title;
                _lastArtist = artist;
                _lastDominant = dominant;
                _lastAccent = accent;
                _lastSecondary = secondary;
                _lastAmbient = ambient;
                _lastVignette = vignette;
                _lastTemplate = template;
                SongTitleText.Text = title;
                ArtistText.Text = artist;
                StyleButton.Content = _kineticRenderer.GetCurrentTemplateDisplayName().ToUpperInvariant();
                PaletteTint.Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1),
                    GradientStops =
                    {
                        new GradientStop(Color.FromArgb(0x55, _currentPalette.DominantColor.R, _currentPalette.DominantColor.G, _currentPalette.DominantColor.B), 0.0),
                        new GradientStop(Color.FromArgb(0x45, _currentPalette.SecondaryColor.R, _currentPalette.SecondaryColor.G, _currentPalette.SecondaryColor.B), 0.35),
                        new GradientStop(Color.FromArgb(0xA8, _currentPalette.AmbientColor.R, _currentPalette.AmbientColor.G, _currentPalette.AmbientColor.B), 0.68),
                        new GradientStop(Color.FromArgb(0xE8, _currentPalette.DarkVignetteColor.R, _currentPalette.DarkVignetteColor.G, _currentPalette.DarkVignetteColor.B), 1.0)
                    }
                };

                if (lyricChanged || titleChanged)
                {
                    _displayedLyricPosition = _primaryWindow.CurrentLyricPosition;
                    _lyricClock.Reset(_displayedLyricPosition, _primaryWindow.IsPlaybackPlaying);
                    _hasClockPosition = true;
                }
            }

            if (paletteChanged) UpdateSecondaryGlowTargetColor();
            PlayPauseTrackIcon.Text = track?.IsPlaying == true ? "\uE769" : "\uE768";

            string transformStatus = _primaryWindow.IsTranslationActive
                ? "TRANSLATED"
                : _primaryWindow.IsRomanizationActive ? "ROMANIZED" : string.Empty;
            if (!string.Equals(_lastTransformStatus, transformStatus, StringComparison.Ordinal))
            {
                _lastTransformStatus = transformStatus;
                TransformStatusText.Text = transformStatus;
                TransformStatusText.Visibility = string.IsNullOrEmpty(transformStatus)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                TranslateButton.Content = _primaryWindow.IsTranslationActive ? "ORIGINAL" : "TRANSLATE";
                RomanizeButton.Content = _primaryWindow.IsRomanizationActive ? "ORIGINAL" : "ROMANIZE";
            }

            RefreshLyricsVisibility();
        }

        private void PopulateLanguagePicker()
        {
            LanguageComboBox.Items.Clear();
            foreach (TranslationLanguage language in LyricsTranslationLanguages.All)
            {
                LanguageComboBox.Items.Add(new ComboBoxItem
                {
                    Content = LyricsTranslationLanguages.DisplayName(language.Code),
                    Tag = language.Code
                });
            }
            _isLanguagePickerInitialized = true;
            SelectLanguage(_settings.LyricsTranslationLanguage);
        }

        private void SelectLanguage(string languageCode)
        {
            foreach (ComboBoxItem item in LanguageComboBox.Items)
            {
                if (item.Tag is string code &&
                    string.Equals(code, languageCode, StringComparison.OrdinalIgnoreCase))
                {
                    LanguageComboBox.SelectedItem = item;
                    return;
                }
            }
        }

        private void OnSettingsChanged()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ApplySecondaryGlowSettings();
                ApplySecondaryScreenSettings();
                ApplyLowLatencyMode();
                UpdateSecondaryScreenSettingsPopup();
                OffsetValueText.Text = FormatOffset(_settings.LyricsOffsetMs);
                if (_isLanguagePickerInitialized)
                {
                    SelectLanguage(_settings.LyricsTranslationLanguage);
                }
            }));
        }

        private void ApplyLowLatencyMode()
        {
            bool enabled = _settings.LowLatencyMode;
            if (_appliedLowLatencyMode == enabled) return;

            _appliedLowLatencyMode = enabled;
            _kineticRenderer.SetLowLatencyMode(enabled);
            BackdropImage.Source = _lastThumbnail != null
                ? CreateBlurredBackdrop(_lastThumbnail) ?? BackdropBlur.GetFallbackBackdrop()
                : BackdropBlur.GetFallbackBackdrop();
        }

        private BitmapSource? CreateBlurredBackdrop(BitmapSource? source)
        {
            return _settings.LowLatencyMode
                ? BackdropBlur.CreateBlurredBackdrop(source, targetSize: 144, blurRadius: 4, passes: 1)
                : BackdropBlur.CreateBlurredBackdrop(source);
        }

        private void ApplySecondaryGlowSettings()
        {
            bool enabled = _settings.SecondaryGlowEnabled;
            double brightness = _settings.SecondaryGlowBrightness;
            double spread = _settings.SecondaryGlowSpread;
            double rotation = _settings.SecondaryGlowRotationSpeed;
            double breathing = _settings.SecondaryGlowBreathingAmount;
            double audioReactivity = _settings.SecondaryGlowAudioReactivity;
            SecondaryGlowColorMode colorMode = _settings.SecondaryGlowColorMode;
            string customColor = _settings.SecondaryGlowCustomColor;

            bool geometryChanged = !_hasAppliedGlowSettings || _glowSpread != spread;
            bool colorChanged = !_hasAppliedGlowSettings ||
                _glowColorMode != colorMode ||
                !string.Equals(_glowCustomColor, customColor, StringComparison.Ordinal);
            _glowBrightness = brightness;
            _glowSpread = spread;
            _glowRotationSpeed = rotation;
            _glowBreathingAmount = breathing;
            _glowAudioReactivity = audioReactivity;
            _glowColorMode = colorMode;
            _glowCustomColor = customColor;
            _hasAppliedGlowSettings = true;

            if (geometryChanged)
            {
                _beam.SetGeometry(ActualWidth, ActualHeight, spread);
            }
            _beam.SetEnabled(enabled);
            if (colorChanged)
            {
                UpdateSecondaryGlowTargetColor();
            }
        }

        private void ApplySecondaryScreenSettings()
        {
            _displayAdjustmentEffect.DisplayBrightness = _settings.SecondaryBrightness;
            _displayAdjustmentEffect.DisplayContrast = _settings.SecondaryContrast;
            BackdropDimmingOverlay.Opacity = _settings.SecondaryBackdropDimming * 0.8;
        }

        private void UpdateSecondaryScreenSettingsPopup()
        {
            if (SecondaryBrightnessSlider == null) return;
            _suppressScreenSettingsUpdate = true;
            try
            {
                SecondaryBrightnessSlider.Value = _settings.SecondaryBrightness * 100.0;
                SecondaryContrastSlider.Value = _settings.SecondaryContrast * 100.0;
                SecondaryDimmingSlider.Value = _settings.SecondaryBackdropDimming * 100.0;
                SecondaryGlowBrightnessSlider.Value = _settings.SecondaryGlowBrightness * 100.0;
                SecondaryGlowSpreadSlider.Value = _settings.SecondaryGlowSpread;
                SecondaryAudioReactivitySlider.Value = _settings.SecondaryAudioReactivity * 100.0;
                SecondaryGlowEnabledCheckBox.IsChecked = _settings.SecondaryGlowEnabled;
                UpdateSecondaryScreenSettingLabels();
            }
            finally
            {
                _suppressScreenSettingsUpdate = false;
            }
        }

        private void UpdateSecondaryScreenSettingLabels()
        {
            SecondaryBrightnessValue.Text = $"Brightness  {SecondaryBrightnessSlider.Value:0}%";
            SecondaryContrastValue.Text = $"Contrast  {SecondaryContrastSlider.Value:0}%";
            SecondaryDimmingValue.Text = $"Backdrop dimming  {SecondaryDimmingSlider.Value:0}%";
            SecondaryGlowBrightnessValue.Text = $"Glow brightness  {SecondaryGlowBrightnessSlider.Value:0}%";
            SecondaryGlowSpreadValue.Text = $"Glow spread  {SecondaryGlowSpreadSlider.Value:0} px";
            SecondaryAudioReactivityValue.Text = $"Audio reactivity  {SecondaryAudioReactivitySlider.Value / 100.0:0.00}x";
        }

        private void UpdateSecondaryGlowTargetColor()
        {
            Color target = _glowColorMode switch
            {
                SecondaryGlowColorMode.SecondaryAccent => _currentPalette.SecondaryColor,
                SecondaryGlowColorMode.Custom => ParseCustomGlowColor(_glowCustomColor),
                _ => _currentPalette.DominantColor
            };
            _beam.SetTargetColor(target);
        }

        private static Color ParseCustomGlowColor(string value)
        {
            try
            {
                if (System.Windows.Media.ColorConverter.ConvertFromString(value) is Color color) return color;
            }
            catch (FormatException)
            {
            }
            return Color.FromRgb(124, 92, 252);
        }

        private void ApplyButtonAccent(Color accent)
        {
            Resources["NyxAccentColor"] = accent;
            Resources["NyxAccentDarkColor"] = Color.FromRgb(
                (byte)(accent.R * 0.48),
                (byte)(accent.G * 0.48),
                (byte)(accent.B * 0.48));
            Resources["NyxAccentSoftColor"] = Color.FromArgb(0x48, accent.R, accent.G, accent.B);
        }

        private void RevealActionBar()
        {
            if (HeaderActionsBar == null) return;
            HeaderActionsBar.IsHitTestVisible = true;
            double opacity = HeaderActionsBar.Opacity;
            HeaderActionsBar.BeginAnimation(
                UIElement.OpacityProperty,
                new DoubleAnimation(opacity, 1.0, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });

            if (_idleTimer == null)
            {
                _idleTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(3)
                };
                _idleTimer.Tick += IdleTimer_Tick;
            }
            _idleTimer.Stop();
            _idleTimer.Start();
        }

        private void IdleTimer_Tick(object? sender, EventArgs e)
        {
            if (HeaderActionsBar.IsMouseOver || LanguageComboBox.IsDropDownOpen || _isDisplaySettingsPopupOpen)
            {
                _idleTimer?.Stop();
                _idleTimer?.Start();
                return;
            }

            _idleTimer?.Stop();
            double opacity = HeaderActionsBar.Opacity;
            var fade = new DoubleAnimation(opacity, 0.0, TimeSpan.FromMilliseconds(260))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            fade.Completed += (_, _) =>
            {
                if (HeaderActionsBar.Opacity <= 0.01)
                {
                    HeaderActionsBar.IsHitTestVisible = false;
                }
            };
            HeaderActionsBar.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        private void HandlePointerActivity()
        {
            Point position = Mouse.GetPosition(this);
            if (_hasMousePosition &&
                Math.Abs(position.X - _lastMousePosition.X) < 0.5 &&
                Math.Abs(position.Y - _lastMousePosition.Y) < 0.5)
            {
                return;
            }

            _lastMousePosition = position;
            _hasMousePosition = true;
            RevealActionBar();
        }

        private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e) => HandlePointerActivity();

        private void Window_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e) => HandlePointerActivity();

        private void Window_Activated(object? sender, EventArgs e)
        {
            _hasMousePosition = false;
            RevealActionBar();
        }

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                _hasMousePosition = false;
                RevealActionBar();
            }
            else
            {
                _idleTimer?.Stop();
            }
        }

        private void RefreshLyricsVisibility()
        {
            TrackLyrics? lyrics = _primaryWindow.CurrentLyrics;
            if (_hasAppliedLyricsVisibility &&
                ReferenceEquals(_lastVisibilityLyrics, lyrics) &&
                _lastLyricsVisibilityToggle == _lyricsVisible)
            {
                return;
            }

            UpdateLyricsVisibility();
        }

        private void UpdateLyricsVisibility()
        {
            TrackLyrics? lyrics = _primaryWindow.CurrentLyrics;
            bool hasSyncedLyrics = lyrics?.IsSynced == true &&
                lyrics.Lines != null &&
                lyrics.Lines.Count > 0;
            bool hasPlainLyrics = !string.IsNullOrWhiteSpace(lyrics?.PlainLyrics);

            KineticLyricsCanvas.Visibility = _lyricsVisible && hasSyncedLyrics
                ? Visibility.Visible
                : Visibility.Collapsed;
            PlainLyricsViewer.Visibility = _lyricsVisible && !hasSyncedLyrics && hasPlainLyrics
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (hasPlainLyrics && !hasSyncedLyrics && PlainLyricsText.Text != lyrics!.PlainLyrics)
            {
                PlainLyricsText.Text = lyrics.PlainLyrics;
                PlainLyricsViewer.ScrollToVerticalOffset(0);
            }

            bool showStatus = !_lyricsVisible || (!hasSyncedLyrics && !hasPlainLyrics);
            StageStatusPanel.Visibility = showStatus ? Visibility.Visible : Visibility.Collapsed;
            string statusText = !_lyricsVisible
                ? "Lyrics hidden · press L to show"
                : lyrics?.IsInstrumental == true
                    ? "Instrumental"
                    : "Waiting for synchronized lyrics";
            if (!string.Equals(StageStatusText.Text, statusText, StringComparison.Ordinal))
            {
                StageStatusText.Text = statusText;
            }

            _lastVisibilityLyrics = lyrics;
            _lastLyricsVisibilityToggle = _lyricsVisible;
            _hasAppliedLyricsVisibility = true;
        }

        private async void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && SecondaryDisplaySettingsPopup.IsOpen)
            {
                SecondaryDisplaySettingsPopup.IsOpen = false;
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape && LanguageComboBox.IsDropDownOpen)
            {
                RevealActionBar();
                return;
            }

            if (await _primaryWindow.HandleSharedShortcutFromSecondaryAsync(e.Key))
            {
                e.Handled = true;
            }
        }

        private void ToggleLyricsVisibility()
        {
            _lyricsVisible = !_lyricsVisible;
            UpdateLyricsVisibility();
        }

        public void ToggleLyricsVisibilityFromPrimary() => ToggleLyricsVisibility();

        public void ToggleFullscreenFromPrimary() => ToggleFullscreen();

        private void ToggleFullscreen()
        {
            if (!_isWindowed)
            {
                _isWindowed = true;
                Topmost = false;
                WindowStyle = WindowStyle.SingleBorderWindow;
                ResizeMode = ResizeMode.CanResize;
                PlaceWindowed();
                return;
            }

            _isWindowed = false;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Win32Helper.PositionWindowOnMonitor(this, _monitor, topmost: true);
        }

        private void PlaceWindowed()
        {
            Topmost = false;
            double monitorWidth = _monitor.Bounds.Width / _monitor.ScaleX;
            double monitorHeight = _monitor.Bounds.Height / _monitor.ScaleY;
            Width = Math.Min(1200.0, monitorWidth * 0.82);
            Height = Math.Min(820.0, monitorHeight * 0.82);
            Left = (_monitor.Bounds.Left / _monitor.ScaleX) + ((monitorWidth - Width) / 2.0);
            Top = (_monitor.Bounds.Top / _monitor.ScaleY) + ((monitorHeight - Height) / 2.0);
        }

        private void TranslateButton_Click(object sender, RoutedEventArgs e) =>
            _primaryWindow.ToggleTranslationFromSecondary();

        private void RomanizeButton_Click(object sender, RoutedEventArgs e) =>
            _primaryWindow.ToggleRomanizationFromSecondary();

        private void StyleButton_Click(object sender, RoutedEventArgs e)
        {
            _primaryWindow.CycleLyricsTemplateFromSecondary();
            SyncPrimaryState();
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLanguagePickerInitialized ||
                LanguageComboBox.SelectedItem is not ComboBoxItem item ||
                item.Tag is not string code)
            {
                return;
            }

            _settings.LyricsTranslationLanguage = code;
        }

        private void DecreaseOffsetButton_Click(object sender, RoutedEventArgs e)
        {
            _settings.LyricsOffsetMs -= 50;
            OffsetValueText.Text = FormatOffset(_settings.LyricsOffsetMs);
        }

        private void IncreaseOffsetButton_Click(object sender, RoutedEventArgs e)
        {
            _settings.LyricsOffsetMs += 50;
            OffsetValueText.Text = FormatOffset(_settings.LyricsOffsetMs);
        }

        private static string FormatOffset(int offsetMs) =>
            offsetMs == 0 ? "0 ms" : $"{offsetMs:+#;-#;0} ms";

        private void SwapDisplaysButton_Click(object sender, RoutedEventArgs e)
        {
            _settings.SwapDualDisplays = !_settings.SwapDualDisplays;
        }

        private async void PreviousTrackButton_Click(object sender, RoutedEventArgs e) =>
            await _mediaService.SkipPreviousAsync();

        private async void PlayPauseTrackButton_Click(object sender, RoutedEventArgs e) =>
            await _mediaService.TogglePlayPauseAsync();

        private async void NextTrackButton_Click(object sender, RoutedEventArgs e) =>
            await _mediaService.SkipNextAsync();

        private void ExitButton_Click(object sender, RoutedEventArgs e) =>
            ExitRequested?.Invoke();

        private void SecondaryDisplaySettingsButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateSecondaryScreenSettingsPopup();
            SecondaryDisplaySettingsPopup.IsOpen = !SecondaryDisplaySettingsPopup.IsOpen;
            if (SecondaryDisplaySettingsPopup.IsOpen) RevealActionBar();
        }

        private void SecondaryDisplaySettingsPopup_Opened(object? sender, EventArgs e)
        {
            _isDisplaySettingsPopupOpen = true;
            _idleTimer?.Stop();
            HeaderActionsBar.IsHitTestVisible = true;
            HeaderActionsBar.BeginAnimation(UIElement.OpacityProperty, null);
            HeaderActionsBar.Opacity = 1.0;
        }

        private void SecondaryDisplaySettingsPopup_Closed(object? sender, EventArgs e)
        {
            _isDisplaySettingsPopupOpen = false;
            RevealActionBar();
        }

        private void DisplaySettingsPopup_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                SecondaryDisplaySettingsPopup.IsOpen = false;
                e.Handled = true;
            }
        }

        private void SecondaryScreenSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressScreenSettingsUpdate || !IsLoaded) return;

            if (ReferenceEquals(sender, SecondaryBrightnessSlider))
                _settings.SecondaryBrightness = e.NewValue / 100.0;
            else if (ReferenceEquals(sender, SecondaryContrastSlider))
                _settings.SecondaryContrast = e.NewValue / 100.0;
            else if (ReferenceEquals(sender, SecondaryDimmingSlider))
                _settings.SecondaryBackdropDimming = e.NewValue / 100.0;
            else if (ReferenceEquals(sender, SecondaryGlowBrightnessSlider))
                _settings.SecondaryGlowBrightness = e.NewValue / 100.0;
            else if (ReferenceEquals(sender, SecondaryGlowSpreadSlider))
                _settings.SecondaryGlowSpread = e.NewValue;
            else if (ReferenceEquals(sender, SecondaryAudioReactivitySlider))
                _settings.SecondaryAudioReactivity = e.NewValue / 100.0;

            UpdateSecondaryScreenSettingLabels();
        }

        private void SecondaryGlowEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (!_suppressScreenSettingsUpdate && SecondaryGlowEnabledCheckBox.IsChecked is bool enabled)
                _settings.SecondaryGlowEnabled = enabled;
        }

        private void CopyPrimaryDisplaySettings_Click(object sender, RoutedEventArgs e)
        {
            _settings.CopyPrimaryScreenSettingsToSecondary();
            UpdateSecondaryScreenSettingsPopup();
        }

        private void ResetSecondaryDisplaySettings_Click(object sender, RoutedEventArgs e)
        {
            _settings.ResetSecondaryScreenSettings();
            UpdateSecondaryScreenSettingsPopup();
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            _idleTimer?.Stop();
            SecondaryDisplaySettingsPopup.IsOpen = false;
            if (!_isClosingFromOwner)
            {
                ExitRequested?.Invoke();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            SecondaryDisplaySettingsPopup.IsOpen = false;
            CompositionTarget.Rendering -= OnRenderFrame;
            _settings.SettingsChanged -= OnSettingsChanged;
            if (_idleTimer != null)
            {
                _idleTimer.Stop();
                _idleTimer.Tick -= IdleTimer_Tick;
                _idleTimer = null;
            }
            HeaderActionsBar.BeginAnimation(UIElement.OpacityProperty, null);
            ExitRequested = null;
            base.OnClosed(e);
        }
    }
}
