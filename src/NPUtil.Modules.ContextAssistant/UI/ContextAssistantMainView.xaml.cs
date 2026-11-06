using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using NPUtil.Modules.ContextAssistant.Core;

namespace NPUtil.Modules.ContextAssistant.UI;

public partial class ContextAssistantMainView : UserControl
{
    private readonly ContextAssistantEngine _engine;
    private string? _currentActiveApp;

    public ContextAssistantMainView(ContextAssistantEngine engine)
    {
        InitializeComponent();
        _engine = engine;

        TxtContextInput.TextChanged += (s, e) =>
        {
            if (TxtContextLength != null && TxtContextInput != null)
            {
                TxtContextLength.Text = $"{TxtContextInput.Text.Length} characters";
            }
        };

        Loaded += async (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(TxtContextInput.Text))
            {
                await RefreshCapturedContextAsync();
            }
        };
    }

    public void SetContextText(string text)
    {
        TxtContextInput.Text = text;
    }

    public async Task RefreshCapturedContextAsync()
    {
        var context = await _engine.CaptureContextAsync();
        _currentActiveApp = context.ActiveProcessName;

        var appTitle = !string.IsNullOrEmpty(context.ActiveProcessName)
            ? $"{context.ActiveProcessName} ({context.ActiveWindowTitle})"
            : "Desktop / Explorer";

        TxtAppSource.Text = $"Active Window: {appTitle}";

        if (!string.IsNullOrWhiteSpace(context.SelectedText) && string.IsNullOrWhiteSpace(TxtContextInput.Text))
        {
            TxtContextInput.Text = context.SelectedText;
        }
    }

    private async void BtnCaptureWindow_Click(object sender, RoutedEventArgs e)
    {
        await RefreshCapturedContextAsync();
    }

    private async void BtnAttachFile_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Title = "Attach Document as Context",
            Filter = "Supported Documents (*.pdf;*.docx;*.xlsx;*.pptx;*.csv;*.txt;*.md)|*.pdf;*.docx;*.xlsx;*.pptx;*.csv;*.txt;*.md|All Files (*.*)|*.*"
        };

        if (ofd.ShowDialog() == true)
        {
            TxtResultMeta.Text = $"Extracting {Path.GetFileName(ofd.FileName)}...";
            var extractedText = await _engine.ExtractFileContextAsync(ofd.FileName);
            TxtContextInput.Text = extractedText;
            TxtAppSource.Text = $"Attached File: {Path.GetFileName(ofd.FileName)}";
            TxtResultMeta.Text = "Document extracted successfully";
        }
    }

    private async void QuickAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && Enum.TryParse<ContextActionType>(elem.Tag?.ToString(), out var action))
        {
            await RunActionAsync(action);
        }
    }

    private async void BtnRunCustom_Click(object sender, RoutedEventArgs e)
    {
        var instruction = TxtCustomInstruction.Text.Trim();
        if (string.IsNullOrEmpty(instruction)) return;
        await RunActionAsync(ContextActionType.Custom, instruction);
    }

    private void TxtCustomInstruction_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            BtnRunCustom_Click(sender, e);
        }
    }

    private async Task RunActionAsync(ContextActionType action, string? customInstruction = null)
    {
        var contextText = TxtContextInput.Text.Trim();
        if (string.IsNullOrEmpty(contextText))
        {
            MessageBox.Show("Please enter or capture context text before executing an action.", "Context Required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        TxtResultTitle.Text = customInstruction ?? action.ToString();
        TxtResultMeta.Text = "Running local AI inference...";
        TxtMainOutput.Text = "Processing context...";

        var result = await _engine.ExecuteActionAsync(
            action,
            contextText,
            customInstruction,
            _currentActiveApp);

        if (result.Success)
        {
            TxtMainOutput.Text = result.OutputText;
            TxtResultMeta.Text = $"Completed in {result.ElapsedTime.TotalMilliseconds:F0} ms • Local Hardware Acceleration Active";
        }
        else
        {
            TxtMainOutput.Text = $"Inference error: {result.ErrorMessage}";
            TxtResultMeta.Text = "Failed";
        }
    }

    private void BtnCopyOutput_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(TxtMainOutput.Text))
        {
            Clipboard.SetText(TxtMainOutput.Text);
            TxtResultMeta.Text = "Output copied to clipboard";
        }
    }

    private void BtnExportOutput_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog
        {
            Title = "Save Assistant Output",
            Filter = "Markdown File (*.md)|*.md|Text File (*.txt)|*.txt",
            FileName = $"NPUtil-assistant-{DateTime.Now:yyyyMMdd-HHmm}.md"
        };

        if (sfd.ShowDialog() == true)
        {
            File.WriteAllText(sfd.FileName, TxtMainOutput.Text);
            MessageBox.Show("Output saved successfully.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnClearOutput_Click(object sender, RoutedEventArgs e)
    {
        TxtMainOutput.Text = string.Empty;
        TxtResultTitle.Text = "Assistant Output";
        TxtResultMeta.Text = "Ready";
    }

    private void BtnOpenPopup_Click(object sender, RoutedEventArgs e)
    {
        var popup = new ContextAssistantPopupWindow(_engine);
        popup.Show();
    }

    private void BtnViewHistory_Click(object sender, RoutedEventArgs e)
    {
        var entries = _engine.HistoryDb.GetRecentEntries(20);
        if (entries.Count == 0)
        {
            MessageBox.Show("No saved assistant conversation history found.", "History", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var sb = new System.Text.StringBuilder();
        foreach (var entry in entries)
        {
            sb.AppendLine($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] App: {entry.ActiveApp}");
            sb.AppendLine($"Query: {entry.UserPrompt}");
            var respSnippet = entry.Response.Length > 100 ? entry.Response[..100] + "..." : entry.Response;
            sb.AppendLine($"Result: {respSnippet}");
            sb.AppendLine(new string('-', 50));
        }

        MessageBox.Show(sb.ToString(), "Recent Context Assistant History", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
