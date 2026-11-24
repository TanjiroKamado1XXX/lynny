using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace LmenV2.Rendering.Kinetic
{
    public sealed class ChromaticSplitEffect : ShaderEffect
    {
        private static readonly PixelShader Shader = LoadShader();

        public static readonly DependencyProperty InputProperty = RegisterPixelShaderSamplerProperty(
            nameof(Input), typeof(ChromaticSplitEffect), 0);

        public static readonly DependencyProperty SplitAmountProperty = DependencyProperty.Register(
            nameof(SplitAmount), typeof(double), typeof(ChromaticSplitEffect),
            new UIPropertyMetadata(0.0, PixelShaderConstantCallback(0)));

        public static readonly DependencyProperty RadialBiasProperty = DependencyProperty.Register(
            nameof(RadialBias), typeof(double), typeof(ChromaticSplitEffect),
            new UIPropertyMetadata(0.85, PixelShaderConstantCallback(1)));

        public static readonly DependencyProperty PulseStrengthProperty = DependencyProperty.Register(
            nameof(PulseStrength), typeof(double), typeof(ChromaticSplitEffect),
            new UIPropertyMetadata(0.0, PixelShaderConstantCallback(2)));

        public static readonly DependencyProperty DisplayBrightnessProperty = DependencyProperty.Register(
            nameof(DisplayBrightness), typeof(double), typeof(ChromaticSplitEffect),
            new UIPropertyMetadata(1.0, PixelShaderConstantCallback(3)));

        public static readonly DependencyProperty DisplayContrastProperty = DependencyProperty.Register(
            nameof(DisplayContrast), typeof(double), typeof(ChromaticSplitEffect),
            new UIPropertyMetadata(1.0, PixelShaderConstantCallback(4)));

        public ChromaticSplitEffect()
        {
            PixelShader = Shader;
            UpdateShaderValue(InputProperty);
            UpdateShaderValue(SplitAmountProperty);
            UpdateShaderValue(RadialBiasProperty);
            UpdateShaderValue(PulseStrengthProperty);
            UpdateShaderValue(DisplayBrightnessProperty);
            UpdateShaderValue(DisplayContrastProperty);
        }

        public System.Windows.Media.Brush? Input
        {
            get => (System.Windows.Media.Brush?)GetValue(InputProperty);
            set => SetValue(InputProperty, value);
        }

        public double SplitAmount
        {
            get => (double)GetValue(SplitAmountProperty);
            set => SetValue(SplitAmountProperty, Math.Clamp(value, 0.0, 0.012));
        }

        public double RadialBias
        {
            get => (double)GetValue(RadialBiasProperty);
            set => SetValue(RadialBiasProperty, Math.Clamp(value, 0.0, 1.0));
        }

        public double PulseStrength
        {
            get => (double)GetValue(PulseStrengthProperty);
            set => SetValue(PulseStrengthProperty, Math.Clamp(value, 0.0, 1.0));
        }

        public double DisplayBrightness
        {
            get => (double)GetValue(DisplayBrightnessProperty);
            set => SetValue(DisplayBrightnessProperty, Math.Clamp(value, 0.5, 1.5));
        }

        public double DisplayContrast
        {
            get => (double)GetValue(DisplayContrastProperty);
            set => SetValue(DisplayContrastProperty, Math.Clamp(value, 0.5, 1.5));
        }

        private static PixelShader LoadShader()
        {
            var shader = new PixelShader
            {
                UriSource = new Uri(
                    "pack://application:,,,/LmenV2;component/Rendering/Kinetic/Shaders/ChromaticSplit.ps",
                    UriKind.Absolute)
            };
            shader.Freeze();
            return shader;
        }
    }
}
