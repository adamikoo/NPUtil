using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;
using NPUtil.Modules.UniversalOcr.Core;

namespace NPUtil.Modules.UniversalOcr.UI;

public partial class ScreenLensWindow : FluentWindow
{
    private readonly UniversalOcrEngine _engine;
    private readonly byte[] _imageBytes;
    private readonly Action? _onOpenInFullApp;
    private readonly Action? _onRetake;

    private string _ocrText = string.Empty;
    private string _lastAiResponse = string.Empty;
    private bool _showingRawOcr = false;

    public ScreenLensWindow(
        UniversalOcrEngine engine,
        byte[] imageBytes,
        Rectangle? cropBounds = null,
        Action? onOpenInFullApp = null,
        Action? onRetake = null)
    {
        InitializeComponent();
        _engine = engine;
        _imageBytes = imageBytes;
        _onOpenInFullApp = onOpenInFullApp;
        _onRetake = onRetake;

        Loaded += ScreenLensWindow_Loaded;
        KeyDown += (s, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        };

        PositionWindow(cropBounds);
    }

    private void PositionWindow(Rectangle? cropBounds)
    {
        var workArea = SystemParameters.WorkArea;
        double targetWidth = Width > 0 ? Width : 480;
        double targetHeight = Height > 0 ? Height : 580;

        if (cropBounds.HasValue)
        {
            var bounds = cropBounds.Value;
            // Place to the right of selection if space allows
            double left = bounds.Right + 16;
            double top = bounds.Top;

            if (left + targetWidth > workArea.Right)
            {
                // Place to the left
                left = bounds.Left - targetWidth - 16;
            }

            // Fallback center if selection spans whole screen
            if (left < workArea.Left || left + targetWidth > workArea.Right)
            {
                left = workArea.Right - targetWidth - 24;
            }

            if (top + targetHeight > workArea.Bottom)
            {
                top = workArea.Bottom - targetHeight - 24;
            }
            if (top < workArea.Top)
            {
                top = workArea.Top + 24;
            }

            Left = Math.Max(workArea.Left, Math.Min(left, workArea.Right - targetWidth));
            Top = Math.Max(workArea.Top, Math.Min(top, workArea.Bottom - targetHeight));
        }
        else
        {
            Left = (workArea.Width - targetWidth) / 2 + workArea.Left;
            Top = (workArea.Height - targetHeight) / 2 + workArea.Top;
        }
    }

    private async void ScreenLensWindow_Loaded(object sender, RoutedEventArgs e)
    {
        TxtPrompt.Focus();

        // 1. Load image thumbnail
        try
        {
            using var ms = new MemoryStream(_imageBytes);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            ImgThumbnail.Source = bmp;
            TxtSnipMeta.Text = $"{bmp.PixelWidth} × {bmp.PixelHeight} px";
        }
        catch
        {
            TxtSnipMeta.Text = "Selection Captured";
        }

        // 2. Perform OCR and initial analysis
        await RunOcrAndInitialAnalysisAsync();
    }

    private async Task RunOcrAndInitialAnalysisAsync()
    {
        SetBusy(true, "Recognizing text on screen...");

        var ocrResult = await _engine.ProcessImageBytesAsync(_imageBytes, "auto");
        _ocrText = ocrResult.OriginalText;

        if (string.IsNullOrWhiteSpace(_ocrText))
        {
            SetBusy(false);
            TxtOcrStats.Text = "No clear text detected in selection.";
            TxtResponse.Text = "Could not identify clear text in the selected area. You can try asking a question or retaking the selection.";
            return;
        }

        int words = _ocrText.Split([' ', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Length;
        TxtOcrStats.Text = $"{ocrResult.Lines.Count} lines ({words} words)";

        // If an exception/crash or code was snipped, highlight the Explain chip
        if (_ocrText.Contains("Exception") || _ocrText.Contains("StackTrace") || _ocrText.Contains("at System."))
        {
            BtnChipExplainFix.Appearance = ControlAppearance.Primary;
        }

        // Run initial intelligent "What can you see" breakdown
        SetBusy(true, "Analyzing with Qualcomm Hexagon NPU...");

        if (_engine.InferenceEngine != null)
        {
            _lastAiResponse = await _engine.InferenceEngine.CustomInstructAsync("what can you see", _ocrText);
        }
        else
        {
            _lastAiResponse = _ocrText;
        }

        TxtResponse.Text = _lastAiResponse;
        SetBusy(false);
        TxtPrompt.Focus();
    }

    private async Task ExecuteInstructionAsync(string instruction)
    {
        if (string.IsNullOrWhiteSpace(_ocrText))
        {
            TxtResponse.Text = "No captured text to analyze. Please retake the snip.";
            return;
        }

        SetBusy(true, $"Processing \"{instruction}\"...");

        if (_engine.InferenceEngine != null)
        {
            _lastAiResponse = await _engine.InferenceEngine.CustomInstructAsync(instruction, _ocrText);
        }
        else
        {
            _lastAiResponse = $"AI Engine offline. Text captured:\n{_ocrText}";
        }

        _showingRawOcr = false;
        BtnToggleRawOcr.Content = "Raw OCR";
        TxtResponse.Text = _lastAiResponse;
        SetBusy(false);
        TxtPrompt.Focus();
    }

    private void SetBusy(bool busy, string? message = null)
    {
        PnlLoading.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        TxtResponse.Visibility = busy ? Visibility.Collapsed : Visibility.Visible;
        if (message != null)
        {
            TxtLoadingStatus.Text = message;
        }
    }

    private async void BtnSendPrompt_Click(object sender, RoutedEventArgs e)
    {
        var prompt = TxtPrompt.Text.Trim();
        if (string.IsNullOrWhiteSpace(prompt)) return;

        TxtPrompt.Text = string.Empty;
        await ExecuteInstructionAsync(prompt);
    }

    private async void TxtPrompt_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var prompt = TxtPrompt.Text.Trim();
            if (string.IsNullOrWhiteSpace(prompt)) return;

            TxtPrompt.Text = string.Empty;
            await ExecuteInstructionAsync(prompt);
        }
    }

    private async void Chip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.Tag is string instruction)
        {
            await ExecuteInstructionAsync(instruction);
        }
    }

    private void BtnCopyText_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_ocrText))
        {
            Clipboard.SetText(_ocrText);
            ShowFeedback("Clean text copied!");
        }
    }

    private void BtnCopyResponse_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(TxtResponse.Text))
        {
            Clipboard.SetText(TxtResponse.Text);
            ShowFeedback("Response copied!");
        }
    }

    private void BtnCopyImage_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using var ms = new MemoryStream(_imageBytes);
            using var bmp = new Bitmap(ms);
            Clipboard.SetImage(Imaging.CreateBitmapSourceFromHBitmap(
                bmp.GetHbitmap(),
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions()));
            ShowFeedback("Image copied!");
        }
        catch
        {
            ShowFeedback("Failed to copy image");
        }
    }

    private void BtnToggleRawOcr_Click(object sender, RoutedEventArgs e)
    {
        _showingRawOcr = !_showingRawOcr;
        if (_showingRawOcr)
        {
            TxtResponse.Text = _ocrText;
            BtnToggleRawOcr.Content = "Show AI";
        }
        else
        {
            TxtResponse.Text = _lastAiResponse;
            BtnToggleRawOcr.Content = "Raw OCR";
        }
    }

    private void BtnRetakeSnip_Click(object sender, RoutedEventArgs e)
    {
        Close();
        _onRetake?.Invoke();
    }

    private void BtnOpenFull_Click(object sender, RoutedEventArgs e)
    {
        Close();
        _onOpenInFullApp?.Invoke();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void ShowFeedback(string msg)
    {
        TxtFeedback.Text = msg;
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2.5)
        };
        timer.Tick += (s, e) =>
        {
            TxtFeedback.Text = string.Empty;
            timer.Stop();
        };
        timer.Start();
    }
}