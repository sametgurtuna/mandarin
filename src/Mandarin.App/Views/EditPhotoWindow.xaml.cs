using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ImageMagick;
using Mandarin.Core.AdvancedTools;
using Mandarin.Core.AdvancedTools.Images;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;

namespace Mandarin.App.Views;

public partial class EditPhotoWindow : Window
{
    private readonly string _sourcePath;
    private MagickImage? _previewBase;

    public ImageEditOptions? Result { get; private set; }

    public EditPhotoWindow(string sourcePath)
    {
        InitializeComponent();
        DialogChrome.Apply(this, HeaderBar, CardRoot);
        OutputPreview.Show(OutputPreviewText, sourcePath, AdvancedToolKind.EditPhoto);
        _sourcePath = sourcePath;

        try
        {
            // Load a lightweight preview copy (max 600px) for super fast 60fps real-time preview adjustments
            var orig = new MagickImage(sourcePath);
            var preview = (MagickImage)orig.Clone();
            if (preview.Width > 640 || preview.Height > 640)
            {
                preview.Resize(new MagickGeometry(640, 640) { Greater = true });
            }
            _previewBase = preview;

            UpdatePreview();
        }
        catch
        {
        }
    }

    private ImageEditOptions GetCurrentOptions()
    {
        return new ImageEditOptions(
            Exposure: ExposureSlider.Value,
            Brightness: BrightnessSlider.Value,
            Contrast: ContrastSlider.Value,
            Gamma: GammaSlider.Value,
            Saturation: SaturationSlider.Value,
            Hue: HueSlider.Value,
            Clarity: ClaritySlider.Value,
            NoiseReduction: NoiseSlider.Value
        );
    }

    private void UpdatePreview()
    {
        if (_previewBase == null) return;

        try
        {
            var opt = GetCurrentOptions();

            // Update UI text values
            ExposureVal.Text = $"{opt.Exposure:+0.00;-0.00;0.00} EV";
            BrightnessVal.Text = $"{opt.Brightness:+0;-0;0}";
            ContrastVal.Text = $"{opt.Contrast:+0;-0;0}";
            GammaVal.Text = $"{opt.Gamma:F2}";
            SaturationVal.Text = $"{opt.Saturation:+0;-0;0}";
            HueVal.Text = $"{opt.Hue:+0;-0;0}°";
            ClarityVal.Text = $"{(int)opt.Clarity}";
            NoiseVal.Text = $"{(int)opt.NoiseReduction}";

            using var clone = (MagickImage)_previewBase.Clone();
            ImageEditTool.ApplyAdjustments(clone, opt);

            using var ms = new MemoryStream();
            clone.Write(ms, MagickFormat.Bmp);
            ms.Seek(0, SeekOrigin.Begin);

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();

            PreviewImage.Source = bmp;
        }
        catch
        {
        }
    }

    private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdatePreview();
    }

    private void ResetExposure_Click(object sender, RoutedEventArgs e) { ExposureSlider.Value = 0; }
    private void ResetBrightness_Click(object sender, RoutedEventArgs e) { BrightnessSlider.Value = 0; }
    private void ResetContrast_Click(object sender, RoutedEventArgs e) { ContrastSlider.Value = 0; }
    private void ResetGamma_Click(object sender, RoutedEventArgs e) { GammaSlider.Value = 1.0; }
    private void ResetSaturation_Click(object sender, RoutedEventArgs e) { SaturationSlider.Value = 0; }
    private void ResetHue_Click(object sender, RoutedEventArgs e) { HueSlider.Value = 0; }
    private void ResetClarity_Click(object sender, RoutedEventArgs e) { ClaritySlider.Value = 0; }
    private void ResetNoise_Click(object sender, RoutedEventArgs e) { NoiseSlider.Value = 0; }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        Result = GetCurrentOptions();
        DialogResult = true;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _previewBase?.Dispose();
        base.OnClosed(e);
    }
}
