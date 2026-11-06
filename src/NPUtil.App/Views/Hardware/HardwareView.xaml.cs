using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NPUtil.Core.Hardware;
using NPUtil.Core.Localization;
using NPUtil.Core.Logging;

namespace NPUtil.App.Views.Hardware;

public partial class HardwareView : UserControl
{
    private readonly IHardwareService _hardwareService;
    private readonly StringBuilder _logBuffer = new();

    public HardwareView(IHardwareService hardwareService)
    {
        InitializeComponent();
        _hardwareService = hardwareService;

        AppLogger.LogEntryAdded += (s, entry) => Dispatcher.Invoke(() =>
        {
            var line = $"[{entry.Timestamp:HH:mm:ss.fff}] [{entry.Level}] [{entry.Source}] {entry.Message}";
            _logBuffer.AppendLine(line);
            TxtLogViewer.Text = _logBuffer.ToString();
            TxtLogViewer.ScrollToEnd();
        });

        Loaded += (s, e) => RefreshDiagnostics();
    }

    public void RefreshDiagnostics()
    {
        var diag = _hardwareService.GetDiagnostics();

        TxtProcessor.Text = $"{diag.ProcessorName} ({diag.LogicalCores} logical cores)";
        TxtArch.Text = $"{diag.Architecture} (Native Execution)";
        TxtOs.Text = diag.OperatingSystem;
        TxtActiveAi.Text = diag.ActiveAiBackend;
        TxtQnnStatus.Text = diag.IsQnnLibraryAvailable ? "Installed and Verified" : "CPU Fallback Active";
        TxtPowerStatus.Text = diag.IsOnBattery 
            ? LocalizationManager.Instance.GetString("PowerBattery", diag.BatteryPercent) 
            : LocalizationManager.Get("PowerAc");

        // Populate recent logs
        var recent = AppLogger.GetRecentEntries();
        _logBuffer.Clear();
        foreach (var entry in recent)
        {
            _logBuffer.AppendLine($"[{entry.Timestamp:HH:mm:ss.fff}] [{entry.Level}] [{entry.Source}] {entry.Message}");
        }
        TxtLogViewer.Text = _logBuffer.ToString();
        TxtLogViewer.ScrollToEnd();
    }

    private void BtnExportLogs_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog
        {
            Filter = "Log Files (*.log)|*.log|Text Files (*.txt)|*.txt",
            FileName = $"NPUtil-diagnostics-{DateTime.Now:yyyyMMdd-HHmm}.log"
        };

        if (sfd.ShowDialog() == true)
        {
            File.WriteAllText(sfd.FileName, _logBuffer.ToString());
            MessageBox.Show("Logs exported successfully.", "Exported", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
