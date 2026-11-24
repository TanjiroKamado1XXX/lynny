using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace LmenV2.Services
{
    public enum ChromaticSplitScope
    {
        LyricsOnly,
        BackdropArtwork,
        FullVisualizerStage
    }

    public enum AmbientBackdropMode
    {
        ArtworkBlurOnly,
        AudioReactiveMesh,
        Blended
    }

    public enum SecondaryGlowColorMode
    {
        DominantPalette,
        SecondaryAccent,
        Custom
    }

    public class SettingsModel
    {
        public double GlowBrightness { get; set; } = 0.85;      // 0.2 to 1.5
        public double GlowSpread { get; set; } = 125.0;          // 50 to 260 px
        public double RotationSpeed { get; set; } = 1.0;        // 0.0 (off) to 2.5 (fast)
        public double AudioReactivity { get; set; } = 1.0;      // 0.0 (off) to 2.0 (hyper)
        public double BreathingAmount { get; set; } = 0.7;      // 0.0 (steady) to 1.0 (deep)
        public double BackdropBrightness { get; set; } = 0.70;  // 0.0 (off) to 1.0 (vivid)
        public bool EnableLyricsBeatEffects { get; set; } = true;
        public string PaxSenixApiKey { get; set; } = string.Empty;
        public int LyricsOffsetMs { get; set; } = 0;             // manual lyric sync offset
        public string LyricsTranslationLanguage { get; set; } = "en";
        public bool EnableMotionArtwork { get; set; } = true;
        public bool PrioritizeSpotifyCanvas { get; set; } = false;
        public string SpotifySpDcCookie { get; set; } = string.Empty;
        public bool EnableChromaticSplit { get; set; }
        public bool LowLatencyMode { get; set; }
        public double MaxChromaticSplit { get; set; } = 0.006;
        public ChromaticSplitScope ChromaticSplitTarget { get; set; } = ChromaticSplitScope.LyricsOnly;
        public AmbientBackdropMode BackdropMode { get; set; } = AmbientBackdropMode.ArtworkBlurOnly;
        public double MeshMotionSpeed { get; set; } = 1.0;
        public double MeshAudioReactivity { get; set; } = 1.0;
        public double BackdropVignetteDarkness { get; set; } = 0.70;
        public bool DualDisplayModeEnabled { get; set; }
        public bool SwapDualDisplays { get; set; }
        public double PrimaryBrightness { get; set; } = 1.0;
        public double PrimaryContrast { get; set; } = 1.0;
        public double SecondaryBrightness { get; set; } = 1.0;
        public double SecondaryContrast { get; set; } = 1.0;
        public double SecondaryBackdropDimming { get; set; } = 0.3;
        public bool SecondaryGlowEnabled { get; set; } = true;
        public double SecondaryGlowBrightness { get; set; } = 0.85;
        public double SecondaryGlowSpread { get; set; } = 110.0;
        public double SecondaryGlowRotationSpeed { get; set; } = 1.0;
        public double SecondaryGlowBreathingAmount { get; set; } = 0.7;
        public double SecondaryGlowAudioReactivity { get; set; } = 1.0;
        public SecondaryGlowColorMode SecondaryGlowColorMode { get; set; } = SecondaryGlowColorMode.DominantPalette;
        public string SecondaryGlowCustomColor { get; set; } = "#7C5CFC";

        [JsonIgnore]
        public double PrimaryBackdropDimming
        {
            get => 1.0 - BackdropBrightness;
            set
            {
                double dimming = double.IsFinite(value) ? Math.Clamp(value, 0.0, 1.0) : 0.3;
                BackdropBrightness = 1.0 - dimming;
            }
        }

        [JsonIgnore]
        public double PrimaryGlowBrightness
        {
            get => GlowBrightness;
            set => GlowBrightness = value;
        }

        [JsonIgnore]
        public double PrimaryGlowSpread
        {
            get => GlowSpread;
            set => GlowSpread = value;
        }

        [JsonIgnore]
        public double PrimaryAudioReactivity
        {
            get => AudioReactivity;
            set => AudioReactivity = value;
        }

        [JsonIgnore]
        public double SecondaryAudioReactivity
        {
            get => SecondaryGlowAudioReactivity;
            set => SecondaryGlowAudioReactivity = value;
        }
    }

    public class SettingsService
    {
        private static SettingsService? _instance;
        public static SettingsService Instance => _instance ??= new SettingsService();
        private static readonly JsonSerializerOptions SettingsJsonOptions = new JsonSerializerOptions { WriteIndented = true };

        private readonly string _settingsFilePath;
        private SettingsModel _model;

        public event Action? SettingsChanged;

        private SettingsService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "LmenV2");
            Directory.CreateDirectory(folder);
            _settingsFilePath = Path.Combine(folder, "settings.json");

            _model = LoadSettings();
        }

        public double BackdropBrightness
        {
            get => _model.BackdropBrightness;
            set
            {
                _model.BackdropBrightness = Math.Clamp(value, 0.0, 1.0);
                NotifyAndSave();
            }
        }

        public double GlowBrightness
        {
            get => _model.GlowBrightness;
            set
            {
                _model.GlowBrightness = Math.Clamp(value, 0.1, 1.8);
                NotifyAndSave();
            }
        }

        public double GlowSpread
        {
            get => _model.GlowSpread;
            set
            {
                _model.GlowSpread = Math.Clamp(value, 40.0, 300.0);
                NotifyAndSave();
            }
        }

        public double RotationSpeed
        {
            get => _model.RotationSpeed;
            set
            {
                _model.RotationSpeed = Math.Clamp(value, 0.0, 3.0);
                NotifyAndSave();
            }
        }

        public double AudioReactivity
        {
            get => _model.AudioReactivity;
            set
            {
                _model.AudioReactivity = Math.Clamp(value, 0.0, 2.5);
                NotifyAndSave();
            }
        }

        public double BreathingAmount
        {
            get => _model.BreathingAmount;
            set
            {
                _model.BreathingAmount = Math.Clamp(value, 0.0, 1.0);
                NotifyAndSave();
            }
        }

        public bool EnableLyricsBeatEffects
        {
            get => _model.EnableLyricsBeatEffects;
            set
            {
                _model.EnableLyricsBeatEffects = value;
                NotifyAndSave();
            }
        }

        public string PaxSenixApiKey
        {
            get => _model.PaxSenixApiKey;
            set
            {
                _model.PaxSenixApiKey = value?.Trim() ?? string.Empty;
                NotifyAndSave();
            }
        }

        public int LyricsOffsetMs
        {
            get => _model.LyricsOffsetMs;
            set
            {
                _model.LyricsOffsetMs = Math.Clamp(value, -10000, 10000);
                NotifyAndSave();
            }
        }

        public string LyricsTranslationLanguage
        {
            get => _model.LyricsTranslationLanguage;
            set
            {
                _model.LyricsTranslationLanguage = string.IsNullOrWhiteSpace(value) ? "en" : value;
                NotifyAndSave();
            }
        }

        public bool EnableMotionArtwork
        {
            get => _model.EnableMotionArtwork;
            set
            {
                _model.EnableMotionArtwork = value;
                NotifyAndSave();
            }
        }

        public bool PrioritizeSpotifyCanvas
        {
            get => _model.PrioritizeSpotifyCanvas;
            set
            {
                _model.PrioritizeSpotifyCanvas = value;
                NotifyAndSave();
            }
        }

        public string SpotifySpDcCookie
        {
            get => _model.SpotifySpDcCookie;
            set
            {
                _model.SpotifySpDcCookie = value?.Trim() ?? string.Empty;
                NotifyAndSave();
            }
        }

        public bool EnableChromaticSplit
        {
            get => _model.EnableChromaticSplit;
            set
            {
                _model.EnableChromaticSplit = value;
                NotifyAndSave();
            }
        }

        public bool LowLatencyMode
        {
            get => _model.LowLatencyMode;
            set
            {
                _model.LowLatencyMode = value;
                NotifyAndSave();
            }
        }

        public double MaxChromaticSplit
        {
            get => _model.MaxChromaticSplit;
            set
            {
                _model.MaxChromaticSplit = double.IsFinite(value)
                    ? Math.Clamp(value, 0.0, 0.012)
                    : 0.006;
                NotifyAndSave();
            }
        }

        public ChromaticSplitScope ChromaticSplitTarget
        {
            get => _model.ChromaticSplitTarget;
            set
            {
                _model.ChromaticSplitTarget = Enum.IsDefined(typeof(ChromaticSplitScope), value)
                    ? value
                    : ChromaticSplitScope.LyricsOnly;
                NotifyAndSave();
            }
        }

        public AmbientBackdropMode BackdropMode
        {
            get => _model.BackdropMode;
            set
            {
                _model.BackdropMode = Enum.IsDefined(typeof(AmbientBackdropMode), value)
                    ? value
                    : AmbientBackdropMode.ArtworkBlurOnly;
                NotifyAndSave();
            }
        }

        public double MeshMotionSpeed
        {
            get => _model.MeshMotionSpeed;
            set
            {
                _model.MeshMotionSpeed = double.IsFinite(value) ? Math.Clamp(value, 0.0, 3.0) : 1.0;
                NotifyAndSave();
            }
        }

        public double MeshAudioReactivity
        {
            get => _model.MeshAudioReactivity;
            set
            {
                _model.MeshAudioReactivity = double.IsFinite(value) ? Math.Clamp(value, 0.0, 2.5) : 1.0;
                NotifyAndSave();
            }
        }

        public double BackdropVignetteDarkness
        {
            get => _model.BackdropVignetteDarkness;
            set
            {
                _model.BackdropVignetteDarkness = double.IsFinite(value) ? Math.Clamp(value, 0.0, 1.0) : 0.70;
                NotifyAndSave();
            }
        }

        public bool DualDisplayModeEnabled
        {
            get => _model.DualDisplayModeEnabled;
            set
            {
                _model.DualDisplayModeEnabled = value;
                NotifyAndSave();
            }
        }

        public bool SwapDualDisplays
        {
            get => _model.SwapDualDisplays;
            set
            {
                _model.SwapDualDisplays = value;
                NotifyAndSave();
            }
        }

        public double PrimaryBrightness
        {
            get => _model.PrimaryBrightness;
            set
            {
                _model.PrimaryBrightness = double.IsFinite(value) ? Math.Clamp(value, 0.5, 1.5) : 1.0;
                NotifyAndSave();
            }
        }

        public double PrimaryContrast
        {
            get => _model.PrimaryContrast;
            set
            {
                _model.PrimaryContrast = double.IsFinite(value) ? Math.Clamp(value, 0.5, 1.5) : 1.0;
                NotifyAndSave();
            }
        }

        public double PrimaryBackdropDimming
        {
            get => _model.PrimaryBackdropDimming;
            set
            {
                _model.PrimaryBackdropDimming = value;
                NotifyAndSave();
            }
        }

        public double PrimaryGlowBrightness
        {
            get => GlowBrightness;
            set => GlowBrightness = value;
        }

        public double PrimaryGlowSpread
        {
            get => GlowSpread;
            set => GlowSpread = value;
        }

        public double PrimaryAudioReactivity
        {
            get => AudioReactivity;
            set => AudioReactivity = value;
        }

        public double SecondaryBrightness
        {
            get => _model.SecondaryBrightness;
            set
            {
                _model.SecondaryBrightness = double.IsFinite(value) ? Math.Clamp(value, 0.5, 1.5) : 1.0;
                NotifyAndSave();
            }
        }

        public double SecondaryContrast
        {
            get => _model.SecondaryContrast;
            set
            {
                _model.SecondaryContrast = double.IsFinite(value) ? Math.Clamp(value, 0.5, 1.5) : 1.0;
                NotifyAndSave();
            }
        }

        public double SecondaryBackdropDimming
        {
            get => _model.SecondaryBackdropDimming;
            set
            {
                _model.SecondaryBackdropDimming = double.IsFinite(value) ? Math.Clamp(value, 0.0, 1.0) : 0.3;
                NotifyAndSave();
            }
        }

        public double SecondaryAudioReactivity
        {
            get => SecondaryGlowAudioReactivity;
            set => SecondaryGlowAudioReactivity = value;
        }

        public bool SecondaryGlowEnabled
        {
            get => _model.SecondaryGlowEnabled;
            set
            {
                _model.SecondaryGlowEnabled = value;
                NotifyAndSave();
            }
        }

        public double SecondaryGlowBrightness
        {
            get => _model.SecondaryGlowBrightness;
            set
            {
                _model.SecondaryGlowBrightness = double.IsFinite(value) ? Math.Clamp(value, 0.0, 1.5) : 0.85;
                NotifyAndSave();
            }
        }

        public double SecondaryGlowSpread
        {
            get => _model.SecondaryGlowSpread;
            set
            {
                _model.SecondaryGlowSpread = double.IsFinite(value) ? Math.Clamp(value, 20.0, 300.0) : 110.0;
                NotifyAndSave();
            }
        }

        public double SecondaryGlowRotationSpeed
        {
            get => _model.SecondaryGlowRotationSpeed;
            set
            {
                _model.SecondaryGlowRotationSpeed = double.IsFinite(value) ? Math.Clamp(value, 0.0, 3.0) : 1.0;
                NotifyAndSave();
            }
        }

        public double SecondaryGlowBreathingAmount
        {
            get => _model.SecondaryGlowBreathingAmount;
            set
            {
                _model.SecondaryGlowBreathingAmount = double.IsFinite(value) ? Math.Clamp(value, 0.0, 1.0) : 0.7;
                NotifyAndSave();
            }
        }

        public double SecondaryGlowAudioReactivity
        {
            get => _model.SecondaryGlowAudioReactivity;
            set
            {
                _model.SecondaryGlowAudioReactivity = double.IsFinite(value) ? Math.Clamp(value, 0.0, 2.5) : 1.0;
                NotifyAndSave();
            }
        }

        public SecondaryGlowColorMode SecondaryGlowColorMode
        {
            get => _model.SecondaryGlowColorMode;
            set
            {
                _model.SecondaryGlowColorMode = Enum.IsDefined(typeof(SecondaryGlowColorMode), value)
                    ? value
                    : SecondaryGlowColorMode.DominantPalette;
                NotifyAndSave();
            }
        }

        public string SecondaryGlowCustomColor
        {
            get => _model.SecondaryGlowCustomColor;
            set
            {
                _model.SecondaryGlowCustomColor = NormalizeColor(value);
                NotifyAndSave();
            }
        }

        public void CopyPrimaryGlowToSecondary()
        {
            _model.SecondaryGlowBrightness = Math.Clamp(GlowBrightness, 0.0, 1.5);
            _model.SecondaryGlowSpread = Math.Clamp(GlowSpread, 20.0, 300.0);
            _model.SecondaryGlowRotationSpeed = RotationSpeed;
            _model.SecondaryGlowBreathingAmount = BreathingAmount;
            _model.SecondaryGlowAudioReactivity = AudioReactivity;
            _model.SecondaryGlowColorMode = SecondaryGlowColorMode.DominantPalette;
            NotifyAndSave();
        }

        public void CopyPrimaryScreenSettingsToSecondary()
        {
            _model.SecondaryBrightness = PrimaryBrightness;
            _model.SecondaryContrast = PrimaryContrast;
            _model.SecondaryBackdropDimming = PrimaryBackdropDimming;
            _model.SecondaryGlowEnabled = true;
            _model.SecondaryGlowBrightness = GlowBrightness;
            _model.SecondaryGlowSpread = GlowSpread;
            _model.SecondaryGlowRotationSpeed = RotationSpeed;
            _model.SecondaryGlowBreathingAmount = BreathingAmount;
            _model.SecondaryGlowAudioReactivity = AudioReactivity;
            _model.SecondaryGlowColorMode = SecondaryGlowColorMode.DominantPalette;
            NotifyAndSave();
        }

        public void ResetPrimaryScreenSettings()
        {
            _model.PrimaryBrightness = 1.0;
            _model.PrimaryContrast = 1.0;
            _model.BackdropBrightness = 0.70;
            _model.GlowBrightness = 0.85;
            _model.GlowSpread = 125.0;
            _model.RotationSpeed = 1.0;
            _model.AudioReactivity = 1.0;
            _model.BreathingAmount = 0.7;
            NotifyAndSave();
        }

        public void ResetSecondaryScreenSettings()
        {
            _model.SecondaryBrightness = 1.0;
            _model.SecondaryContrast = 1.0;
            _model.SecondaryBackdropDimming = 0.3;
            _model.SecondaryGlowEnabled = true;
            _model.SecondaryGlowBrightness = 0.85;
            _model.SecondaryGlowSpread = 110.0;
            _model.SecondaryGlowRotationSpeed = 1.0;
            _model.SecondaryGlowBreathingAmount = 0.7;
            _model.SecondaryGlowAudioReactivity = 1.0;
            _model.SecondaryGlowColorMode = SecondaryGlowColorMode.DominantPalette;
            _model.SecondaryGlowCustomColor = "#7C5CFC";
            NotifyAndSave();
        }

        private static string NormalizeColor(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "#7C5CFC";
            try
            {
                if (System.Windows.Media.ColorConverter.ConvertFromString(value) is Color color)
                {
                    return color.ToString();
                }
            }
            catch (FormatException)
            {
            }
            return "#7C5CFC";
        }

        private void NotifyAndSave()
        {
            SettingsChanged?.Invoke();
            SaveSettings();
        }

        private SettingsModel LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    var loaded = JsonSerializer.Deserialize<SettingsModel>(json);
                    if (loaded != null)
                    {
                        loaded.MaxChromaticSplit = double.IsFinite(loaded.MaxChromaticSplit)
                            ? Math.Clamp(loaded.MaxChromaticSplit, 0.0, 0.012)
                            : 0.006;
                        if (!Enum.IsDefined(typeof(ChromaticSplitScope), loaded.ChromaticSplitTarget))
                        {
                            loaded.ChromaticSplitTarget = ChromaticSplitScope.LyricsOnly;
                        }
                        if (!Enum.IsDefined(typeof(AmbientBackdropMode), loaded.BackdropMode))
                        {
                            loaded.BackdropMode = AmbientBackdropMode.ArtworkBlurOnly;
                        }
                        loaded.MeshMotionSpeed = double.IsFinite(loaded.MeshMotionSpeed)
                            ? Math.Clamp(loaded.MeshMotionSpeed, 0.0, 3.0)
                            : 1.0;
                        loaded.MeshAudioReactivity = double.IsFinite(loaded.MeshAudioReactivity)
                            ? Math.Clamp(loaded.MeshAudioReactivity, 0.0, 2.5)
                            : 1.0;
                        loaded.BackdropVignetteDarkness = double.IsFinite(loaded.BackdropVignetteDarkness)
                            ? Math.Clamp(loaded.BackdropVignetteDarkness, 0.0, 1.0)
                            : 0.70;
                        loaded.SecondaryGlowBrightness = double.IsFinite(loaded.SecondaryGlowBrightness)
                            ? Math.Clamp(loaded.SecondaryGlowBrightness, 0.0, 1.5)
                            : 0.85;
                        loaded.SecondaryGlowSpread = double.IsFinite(loaded.SecondaryGlowSpread)
                            ? Math.Clamp(loaded.SecondaryGlowSpread, 20.0, 300.0)
                            : 110.0;
                        loaded.SecondaryGlowRotationSpeed = double.IsFinite(loaded.SecondaryGlowRotationSpeed)
                            ? Math.Clamp(loaded.SecondaryGlowRotationSpeed, 0.0, 3.0)
                            : 1.0;
                        loaded.SecondaryGlowBreathingAmount = double.IsFinite(loaded.SecondaryGlowBreathingAmount)
                            ? Math.Clamp(loaded.SecondaryGlowBreathingAmount, 0.0, 1.0)
                            : 0.7;
                        loaded.SecondaryGlowAudioReactivity = double.IsFinite(loaded.SecondaryGlowAudioReactivity)
                            ? Math.Clamp(loaded.SecondaryGlowAudioReactivity, 0.0, 2.5)
                            : 1.0;
                        loaded.PrimaryBrightness = double.IsFinite(loaded.PrimaryBrightness)
                            ? Math.Clamp(loaded.PrimaryBrightness, 0.5, 1.5)
                            : 1.0;
                        loaded.PrimaryContrast = double.IsFinite(loaded.PrimaryContrast)
                            ? Math.Clamp(loaded.PrimaryContrast, 0.5, 1.5)
                            : 1.0;
                        loaded.SecondaryBrightness = double.IsFinite(loaded.SecondaryBrightness)
                            ? Math.Clamp(loaded.SecondaryBrightness, 0.5, 1.5)
                            : 1.0;
                        loaded.SecondaryContrast = double.IsFinite(loaded.SecondaryContrast)
                            ? Math.Clamp(loaded.SecondaryContrast, 0.5, 1.5)
                            : 1.0;
                        loaded.SecondaryBackdropDimming = double.IsFinite(loaded.SecondaryBackdropDimming)
                            ? Math.Clamp(loaded.SecondaryBackdropDimming, 0.0, 1.0)
                            : 0.3;
                        if (!Enum.IsDefined(typeof(SecondaryGlowColorMode), loaded.SecondaryGlowColorMode))
                        {
                            loaded.SecondaryGlowColorMode = SecondaryGlowColorMode.DominantPalette;
                        }
                        loaded.SecondaryGlowCustomColor = NormalizeColor(loaded.SecondaryGlowCustomColor);
                        return loaded;
                    }
                }
            }
            catch { }
            return new SettingsModel();
        }

        private void SaveSettings()
        {
            try
            {
                string json = JsonSerializer.Serialize(_model, SettingsJsonOptions);
                File.WriteAllText(_settingsFilePath, json);
            }
            catch { }
        }
    }
}
