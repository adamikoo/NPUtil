using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;
using NPUtil.Core.Context;
using NPUtil.Modules.ContextAssistant.Core;

namespace NPUtil.Modules.ContextAssistant.UI;

public partial class ContextAssistantPopupWindow : FluentWindow
{
    private readonly ContextAssistantEngine _engine;
    private readonly Action? _onOpenFullView;
    private CapturedContext? _currentContext;

    public ContextAssistantPopupWindow(ContextAssistantEngine engine, Action? onOpenFullView = null)
    {
        InitializeComponent();
        _engine = engine;
        _onOpenFullView = onOpenFullView;

        // Position window near cursor / bottom-right
        Loaded += async (s, e) =>
        {
            PositionNearCursor();
            await CaptureContextAsync();
        };
    }

    private void PositionNearCursor()
    {
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;

        Left = screenWidth - Width - 40;
        Top = screenHeight - Height - 80;
    }

    public async Task CaptureContextAsync()
    {
        TxtStatus.Text = "Capturing context...";
        _currentContext = await _engine.CaptureContextAsync();

        var app = !string.IsNullOrEmpty(_currentContext.ActiveProcessName)
            ? $"{_currentContext.ActiveProcessName} ({_currentContext.ActiveWindowTitle})"
            : "Current Active Window";

        TxtContextSource.Text = $"Source: {app}";

        if (!string.IsNullOrWhiteSpace(_currentContext.SelectedText))
        {
            var snippet = _currentContext.SelectedText.Trim();
            if (snippet.Length > 120) snippet = snippet[..120] + "...";
            TxtOutput.Text = $"Captured Content:\n\"{snippet}\"\n\nChoose an action above or enter a prompt below.";
        }
        else
        {
            TxtOutput.Text = "No text selected in active application. You can paste text or type a question below.";
        }

        TxtStatus.Text = "Ready";
    }

    private async void Action_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Wpf.Ui.Controls.Button btn && Enum.TryParse<ContextActionType>(btn.Tag?.ToString(), out var action))
        {
            await RunActionAsync(action);
        }
    }

    private async Task RunActionAsync(ContextActionType action, string? customPrompt = null)
    {
        var text = _currentContext?.SelectedText;
        if (string.IsNullOrWhiteSpace(text))
        {
            text = TxtCustomPrompt.Text.Trim();
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            TxtOutput.Text = "Please select some text or provide input first.";
            return;
        }

        TxtStatus.Text = "Analyzing with local AI...";
        TxtOutput.Text = "Processing...";

        var result = await _engine.ExecuteActionAsync(
            action,
            text,
            customPrompt,
            _currentContext?.ActiveProcessName);

        if (result.Success)
        {
            TxtOutput.Text = result.OutputText;
            TxtStatus.Text = $"Completed in {result.ElapsedTime.TotalMilliseconds:F0} ms";
        }
        else
        {
            TxtOutput.Text = $"Error: {result.ErrorMessage}";
            TxtStatus.Text = "Failed";
        }
    }

    private async void BtnSendPrompt_Click(object sender, RoutedEventArgs e)
    {
        var prompt = TxtCustomPrompt.Text.Trim();
        if (string.IsNullOrEmpty(prompt)) return;
        await RunActionAsync(ContextActionType.Custom, prompt);
    }

    private void TxtCustomPrompt_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            BtnSendPrompt_Click(sender, e);
        }
    }

    private async void BtnRefreshContext_Click(object sender, RoutedEventArgs e)
    {
        await CaptureContextAsync();
    }

    private void BtnCopyResult_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(TxtOutput.Text))
        {
            Clipboard.SetText(TxtOutput.Text);
            TxtStatus.Text = "Result copied to clipboard";
        }
    }

    private void BtnOpenFull_Click(object sender, RoutedEventArgs e)
    {
        Close();
        _onOpenFullView?.Invoke();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
