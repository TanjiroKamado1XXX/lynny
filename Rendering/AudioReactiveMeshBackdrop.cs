using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using LmenV2.Models;
using LmenV2.Services;

namespace LmenV2.Rendering
{
    public sealed class AudioReactiveMeshBackdrop : IDisposable
    {
        private readonly struct NodeLayout
        {
            public NodeLayout(
                double centerX,
                double centerY,
                double width,
                double height,
                double frequency,
                double phase,
                double driftX,
                double driftY,
                double baseScale,
                double pulseScale,
                double baseOpacity,
                int paletteIndex)
            {
                CenterX = centerX;
                CenterY = centerY;
                Width = width;
                Height = height;
                Frequency = frequency;
                Phase = phase;
                DriftX = driftX;
                DriftY = driftY;
                BaseScale = baseScale;
                PulseScale = pulseScale;
                BaseOpacity = baseOpacity;
                PaletteIndex = paletteIndex;
            }

            public double CenterX { get; }
            public double CenterY { get; }
            public double Width { get; }
            public double Height { get; }
            public double Frequency { get; }
            public double Phase { get; }
            public double DriftX { get; }
            public double DriftY { get; }
            public double BaseScale { get; }
            public double PulseScale { get; }
            public double BaseOpacity { get; }
            public int PaletteIndex { get; }
        }

        private sealed class LightNode
        {
            public LightNode(NodeLayout layout, Color color, byte centerAlpha)
            {
                Layout = layout;
                CenterColor = new GradientStop(ToAlpha(color, centerAlpha), 0.0);
                MidColor = new GradientStop(ToAlpha(color, (byte)(centerAlpha * 0.34)), 0.48);
                var gradient = new RadialGradientBrush
                {
                    Center = new Point(0.5, 0.5),
                    GradientOrigin = new Point(0.5, 0.5),
                    RadiusX = 0.72,
                    RadiusY = 0.72,
                    MappingMode = BrushMappingMode.RelativeToBoundingBox
                };
                gradient.GradientStops.Add(CenterColor);
                gradient.GradientStops.Add(MidColor);
                gradient.GradientStops.Add(new GradientStop(ToAlpha(color, 0), 1.0));

                Scale = new ScaleTransform(1.0, 1.0);
                Translate = new TranslateTransform();
                var transform = new TransformGroup();
                transform.Children.Add(Scale);
                transform.Children.Add(Translate);

                Shape = new Ellipse
                {
                    Fill = gradient,
                    IsHitTestVisible = false,
                    Opacity = layout.BaseOpacity,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    RenderTransform = transform
                };
            }

            public NodeLayout Layout { get; }
            public Ellipse Shape { get; }
            public GradientStop CenterColor { get; }
            public GradientStop MidColor { get; }
            public ScaleTransform Scale { get; }
            public TranslateTransform Translate { get; }
        }

        private readonly struct Palette
        {
            public Palette(Color dominant, Color accent, Color secondary, Color ambient, Color vignette)
            {
                Dominant = dominant;
                Accent = accent;
                Secondary = secondary;
                Ambient = ambient;
                Vignette = vignette;
            }

            public Color Dominant { get; }
            public Color Accent { get; }
            public Color Secondary { get; }
            public Color Ambient { get; }
            public Color Vignette { get; }

            public Color GetColor(int index) => index switch
            {
                0 => Dominant,
                1 => Accent,
                2 => Secondary,
                3 => Ambient,
                _ => Vignette
            };
        }

        private static readonly NodeLayout[] Layouts =
        {
            new NodeLayout(0.10, 0.20, 0.76, 0.92, 0.34, 0.10, 0.12, 0.10, 0.92, 0.22, 0.38, 0),
            new NodeLayout(0.48, 0.04, 0.82, 0.82, 0.27, 1.85, 0.10, 0.12, 0.90, 0.26, 0.35, 1),
            new NodeLayout(0.91, 0.34, 0.73, 1.00, 0.39, 3.40, 0.13, 0.09, 0.94, 0.20, 0.32, 2),
            new NodeLayout(0.70, 0.91, 0.90, 0.80, 0.23, 4.65, 0.11, 0.12, 0.88, 0.24, 0.34, 3),
            new NodeLayout(0.20, 0.78, 0.70, 0.86, 0.31, 5.55, 0.12, 0.11, 0.91, 0.18, 0.25, 4)
        };

        private static readonly Palette FallbackPalette = new Palette(
            Color.FromRgb(124, 92, 252),
            Color.FromRgb(57, 208, 255),
            Color.FromRgb(100, 244, 196),
            Color.FromRgb(23, 26, 37),
            Color.FromRgb(9, 11, 16));

        private readonly Canvas _canvas;
        private readonly LightNode[] _nodes = new LightNode[Layouts.Length];
        private Palette _palette;
        private bool _hasPalette;
        private bool _disposed;
        private double _width;
        private double _height;
        private double _phase;
        private double _audioEnvelope;

        public AudioReactiveMeshBackdrop(Canvas canvas)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            _canvas.ClipToBounds = true;
            _canvas.IsHitTestVisible = false;
            _canvas.Background = Brushes.Transparent;

            for (int i = 0; i < _nodes.Length; i++)
            {
                var node = new LightNode(Layouts[i], FallbackPalette.GetColor(Layouts[i].PaletteIndex), GetCenterAlpha(i));
                _nodes[i] = node;
                _canvas.Children.Add(node.Shape);
            }

            SetPalette(null);
        }

        public void SetPalette(MediaTrackInfo? track)
        {
            if (_disposed) return;

            Palette next;
            if (track?.Thumbnail == null)
            {
                next = FallbackPalette;
            }
            else
            {
                next = new Palette(
                    track.DominantColor,
                    track.AccentColor,
                    track.SecondaryColor,
                    track.AmbientColor,
                    track.DarkVignetteColor);

                if (IsDefaultTrackPalette(next) ||
                    (!HasUsableColor(next.Dominant) &&
                     !HasUsableColor(next.Accent) &&
                     !HasUsableColor(next.Secondary)))
                {
                    var extracted = ColorExtractionService.ExtractPalette(track.Thumbnail);
                    next = new Palette(
                        extracted.DominantColor,
                        extracted.AccentColor,
                        extracted.SecondaryColor,
                        extracted.AmbientColor,
                        extracted.DarkVignetteColor);
                }

                if ((!HasUsableColor(next.Dominant) &&
                    !HasUsableColor(next.Accent) &&
                    !HasUsableColor(next.Secondary)) ||
                    IsExtractionFallback(next))
                {
                    next = FallbackPalette;
                }
            }

            if (_hasPalette && SamePalette(_palette, next)) return;
            _palette = next;
            _hasPalette = true;

            for (int i = 0; i < _nodes.Length; i++)
            {
                Color color = next.GetColor(_nodes[i].Layout.PaletteIndex);
                var animation = new ColorAnimation
                {
                    To = ToAlpha(color, GetCenterAlpha(i)),
                    Duration = TimeSpan.FromMilliseconds(900),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
                };
                _nodes[i].CenterColor.BeginAnimation(GradientStop.ColorProperty, animation);

                var midAnimation = new ColorAnimation
                {
                    To = ToAlpha(color, (byte)(GetCenterAlpha(i) * 0.34)),
                    Duration = TimeSpan.FromMilliseconds(900),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
                };
                _nodes[i].MidColor.BeginAnimation(GradientStop.ColorProperty, midAnimation);
            }
        }

        public void SetSize(double width, double height)
        {
            if (_disposed || width <= 0.0 || height <= 0.0 ||
                (Math.Abs(width - _width) < 1.0 && Math.Abs(height - _height) < 1.0))
            {
                return;
            }

            _width = width;
            _height = height;
            for (int i = 0; i < _nodes.Length; i++)
            {
                var node = _nodes[i];
                double nodeWidth = Math.Max(1.0, width * node.Layout.Width);
                double nodeHeight = Math.Max(1.0, height * node.Layout.Height);
                node.Shape.Width = nodeWidth;
                node.Shape.Height = nodeHeight;
                Canvas.SetLeft(node.Shape, (width * node.Layout.CenterX) - (nodeWidth * 0.5));
                Canvas.SetTop(node.Shape, (height * node.Layout.CenterY) - (nodeHeight * 0.5));
            }
        }

        public void Update(
            double elapsedSeconds,
            float energy,
            float bassEnergy,
            bool isPlaying,
            double motionSpeed,
            double audioReactivity)
        {
            if (_disposed || _width <= 0.0 || _height <= 0.0) return;

            double dt = Math.Clamp(elapsedSeconds, 0.0, 0.1);
            double target = isPlaying
                ? Math.Clamp(Math.Max(energy * 0.55, bassEnergy * 1.15) * audioReactivity, 0.0, 1.0)
                : 0.0;
            double timeConstant = target > _audioEnvelope ? 0.055 : 0.42;
            double blend = 1.0 - Math.Exp(-dt / timeConstant);
            _audioEnvelope += (target - _audioEnvelope) * blend;
            _phase += dt * motionSpeed * (0.55 + (_audioEnvelope * 1.9));

            for (int i = 0; i < _nodes.Length; i++)
            {
                var node = _nodes[i];
                var layout = node.Layout;
                double phase = (_phase * layout.Frequency) + layout.Phase;
                double drift = 0.42 + (_audioEnvelope * 1.35);
                double x = (Math.Sin(phase) + (0.24 * Math.Sin((phase * 0.53) + 1.1)))
                    * _width * layout.DriftX * drift;
                double y = (Math.Cos((phase * 0.81) + 0.6) + (0.20 * Math.Sin((phase * 0.47) + 2.0)))
                    * _height * layout.DriftY * drift;

                node.Translate.X = x;
                node.Translate.Y = y;

                double scale = layout.BaseScale + (_audioEnvelope * layout.PulseScale);
                node.Scale.ScaleX = scale;
                node.Scale.ScaleY = scale;
                node.Shape.Opacity = Math.Min(0.68, layout.BaseOpacity + (_audioEnvelope * 0.22));
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            for (int i = 0; i < _nodes.Length; i++)
            {
                _nodes[i].CenterColor.BeginAnimation(GradientStop.ColorProperty, null);
                _nodes[i].MidColor.BeginAnimation(GradientStop.ColorProperty, null);
            }

            _canvas.Children.Clear();
        }

        private static byte GetCenterAlpha(int index) => index == 4 ? (byte)92 : (byte)172;

        private static Color ToAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);

        private static bool HasUsableColor(Color color) =>
            color.A > 0 && (color.R > 18 || color.G > 18 || color.B > 18);

        private static bool IsExtractionFallback(Palette palette) =>
            palette.Dominant == Color.FromRgb(235, 25, 60) &&
            palette.Accent == Color.FromRgb(255, 45, 80) &&
            palette.Secondary == Color.FromRgb(255, 60, 150) &&
            palette.Ambient == Color.FromRgb(85, 12, 28) &&
            palette.Vignette == Color.FromRgb(14, 5, 8);

        private static bool IsDefaultTrackPalette(Palette palette) =>
            palette.Dominant == Color.FromRgb(220, 20, 60) &&
            palette.Accent == Color.FromRgb(255, 50, 80) &&
            palette.Secondary == Color.FromRgb(255, 60, 150) &&
            palette.Ambient == Color.FromRgb(80, 10, 25) &&
            palette.Vignette == Color.FromRgb(15, 5, 8);

        private static bool SamePalette(Palette left, Palette right) =>
            left.Dominant == right.Dominant &&
            left.Accent == right.Accent &&
            left.Secondary == right.Secondary &&
            left.Ambient == right.Ambient &&
            left.Vignette == right.Vignette;
    }
}
