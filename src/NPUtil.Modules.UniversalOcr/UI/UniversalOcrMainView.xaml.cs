using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NPUtil.Modules.UniversalOcr.Core;

namespace NPUtil.Modules.UniversalOcr.UI;

public partial class UniversalOcrMainView : UserControl
{
    private readonly UniversalOcrEngine _engine;
    private readonly Action<string>? _onSendToAssistant;
    private readonly Action<string>? _onSendToExtractor;
    private byte[]? _lastCapturedBytes;
    private OcrTranslationResult? _lastResult;

    public UniversalOcrMainView(
        UniversalOcrEngine engine,
        Action<string>? onSendToAssistant = null,
        Action<string>? onSendToExtractor = null)
    {
        InitializeComponent();
        _engine = engine;
        _onSendToAssistant = onSendToAssistant;
        _onSendToExtractor = onSendToExtractor;

        Loaded += (s, e) =>
        {
            TxtOriginalText.TextChanged += TxtOriginalText_TextChanged;
        };
    }

    private void UserControl_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private async void UserControl_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0 && File.Exists(files[0]))
            {
                var bytes = await File.ReadAllBytesAsync(files[0]);
                await ProcessBytesAsync(bytes);
            }
        }
    }

    private async void BtnLoadImage_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Title = "Select Image for OCR",
            Filter = "Image Files (*.png;*.jpg;*.jpeg;*.webp;*.bmp)|*.png;*.jpg;*.jpeg;*.webp;*.bmp|All Files (*.*)|*.*",
            Multiselect = true
        };

        if (ofd.ShowDialog() == true)
        {
            if (ofd.FileNames.Length == 1)
            {
                var bytes = await File.ReadAllBytesAsync(ofd.FileName);
                await ProcessBytesAsync(bytes);
            }
            else
            {
                // Batch OCR
                TxtSourceLangInfo.Text = $"Processing batch of {ofd.FileNames.Length} images...";
                var results = await _engine.ProcessBatchAsync(ofd.FileNames, GetSelectedTargetLang());

                var allOriginal = string.Join("\n\n--- Next Document ---\n\n", results.Select(r => r.OriginalText));
                var allTranslated = string.Join("\n\n--- Next Document ---\n\n", results.Select(r => r.TranslatedText));

                TxtOriginalText.Text = allOriginal;
                TxtTranslatedText.Text = allTranslated;
                TxtOcrStats.Text = $"Batch complete: {results.Count} images processed";
            }
        }
    }

    private void BtnSnipScreen_Click(object sender, RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this);
        parentWindow?.Hide();

        var overlay = new ScreenSnipOverlayWindow();
        overlay.RegionCaptured += async (bytes) =>
        {
            parentWindow?.Show();
            parentWindow?.Activate();
            await ProcessBytesAsync(bytes);
        };

        overlay.Closed += (s, ev) =>
        {
            if (parentWindow != null && !parentWindow.IsVisible)
            {
                parentWindow.Show();
                parentWindow.Activate();
            }
        };

        overlay.Show();
    }

    private string GetSelectedTargetLang()
    {
        var tag = ((ComboBoxItem)CmbTranslationDirection.SelectedItem)?.Tag?.ToString() ?? "auto";
        return tag switch
        {
            "cs-en" => "en",
            "en-cs" => "cs",
            _ => "auto"
        };
    }

    public async Task ProcessBytesAsync(byte[] bytes)
    {
        _lastCapturedBytes = bytes;
        TxtSourceLangInfo.Text = "Recognizing text via Windows Media OCR...";
        TxtTargetLangInfo.Text = "Translating...";

        var targetLang = GetSelectedTargetLang();
        var result = await _engine.ProcessImageBytesAsync(bytes, targetLang);
        _lastResult = result;

        if (result.Success)
        {
            TxtOriginalText.Text = result.OriginalText;
            TxtTranslatedText.Text = result.TranslatedText;

            TxtSourceLangInfo.Text = $"Detected Source: {result.DetectedSourceLang.ToUpperInvariant()}";
            TxtTargetLangInfo.Text = $"Target: {result.TargetLang.ToUpperInvariant()}";
            TxtOcrStats.Text = $"{result.Lines.Count} text lines recognized in {result.ElapsedTime.TotalMilliseconds:F0} ms";
            TxtTranslationStats.Text = "Translation synchronized with original text";
        }
        else
        {
            TxtSourceLangInfo.Text = "OCR Failed";
            MessageBox.Show($"Recognition error: {result.ErrorMessage}", "OCR Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void CmbTranslationDirection_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded && _lastCapturedBytes != null)
        {
            await ProcessBytesAsync(_lastCapturedBytes);
        }
    }

    private void TxtOriginalText_TextChanged(object? sender, TextChangedEventArgs? e)
    {
        if (TxtOcrStats != null && TxtOriginalText != null)
        {
            var lines = TxtOriginalText.Text?.Split('\n') ?? [];
            TxtOcrStats.Text = $"{lines.Length} lines, {TxtOriginalText.Text?.Length ?? 0} chars";
        }
    }

    private void BtnCopyOriginal_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(TxtOriginalText.Text))
        {
            Clipboard.SetText(TxtOriginalText.Text);
            TxtOcrStats.Text = "Original text copied to clipboard";
        }
    }

    private void BtnCopyTranslation_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(TxtTranslatedText.Text))
        {
            Clipboard.SetText(TxtTranslatedText.Text);
            TxtTranslationStats.Text = "Translation copied to clipboard";
        }
    }

    private void BtnSendToAssistant_Click(object sender, RoutedEventArgs e)
    {
        var text = !string.IsNullOrWhiteSpace(TxtTranslatedText.Text) ? TxtTranslatedText.Text : TxtOriginalText.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            MessageBox.Show("No text available to send to Context Assistant.", "Empty Text", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_onSendToAssistant != null)
        {
            _onSendToAssistant.Invoke(text);
        }
        else
        {
            Clipboard.SetText(text);
            MessageBox.Show("OCR text copied to clipboard! Switch to Context Assistant to act on it.", "Sent to Assistant", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnSendToExtractor_Click(object sender, RoutedEventArgs e)
    {
        var text = TxtOriginalText.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            MessageBox.Show("No text available to send to Smart Data Extractor.", "Empty Text", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_onSendToExtractor != null)
        {
            _onSendToExtractor.Invoke(text);
        }
        else
        {
            Clipboard.SetText(text);
            MessageBox.Show("OCR text copied to clipboard! Switch to Smart Data Extractor to parse it.", "Sent to Extractor", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
