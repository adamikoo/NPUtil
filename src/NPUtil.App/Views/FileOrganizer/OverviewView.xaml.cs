using System.Windows;
using System.Windows.Controls;
using NPUtil.Modules.FileOrganizer;

namespace NPUtil.App.Views.FileOrganizer;

public partial class OverviewView : UserControl
{
    private readonly FileOrganizerModule _module;

    public OverviewView(FileOrganizerModule module)
    {
        InitializeComponent();
        _module = module;

        if (_module.Engine != null)
        {
            _module.Engine.StatusChanged += (s, e) => Dispatcher.Invoke(RefreshStatus);
            _module.Engine.ReviewQueueChanged += (s, e) => Dispatcher.Invoke(RefreshStatus);
        }

        if (_module.ModelMgr != null)
        {
            _module.ModelMgr.DownloadProgress += (s, e) => Dispatcher.Invoke(() =>
            {
                PanelDownloadProgress.Visibility = Visibility.Visible;
                ProgressDownload.Value = e.PercentComplete;
                TxtDownloadProgress.Text = e.StatusMessage;
            });
        }

        Loaded += (s, e) => RefreshStatus();
    }

    public void RefreshStatus()
    {
        if (_module.Engine == null) return;

        TxtWatchedFolder.Text = _module.Engine.WatchedFolder;
        TxtMonitoringStatus.Text = _module.Engine.IsMonitoring ? "Status: Active Monitoring" : "Status: Paused";
        BtnToggleMonitoring.Content = _module.Engine.IsMonitoring ? "Pause Monitoring" : "Resume Monitoring";

        var (totalMoved, active, undone) = _module.Engine.GetOperationCounts();
        TxtOrganizedCount.Text = active.ToString();
        TxtUndoneCount.Text = undone.ToString();
        TxtReviewCount.Text = _module.Engine.GetReviewQueue().Count.ToString();
        TxtIndexedCount.Text = _module.Engine.GetIndexedFilesCount().ToString();

        TxtAiBackend.Text = _module.EmbeddingEng?.ActiveBackendName ?? "Auto-detecting...";

        if (_module.ModelMgr != null)
        {
            var info = _module.ModelMgr.GetModelInfo();
            if (info.IsInstalled)
            {
                TxtModelStatus.Text = $"Neural Model Installed: {info.Name} (Apache 2.0)";
                BtnDownloadModel.Visibility = Visibility.Collapsed;
                PanelDownloadProgress.Visibility = Visibility.Collapsed;
            }
            else
            {
                TxtModelStatus.Text = "Using built-in multi-lingual lexical-semantic vectorizer (offline). Neural ONNX model ready to download.";
                BtnDownloadModel.Visibility = Visibility.Visible;
            }
        }

        if (_module.HistoryDb != null)
        {
            var recent = _module.HistoryDb.GetRecentOperations(10);
            GridRecentActivity.ItemsSource = recent;
        }
    }

    private void BtnToggleMonitoring_Click(object sender, RoutedEventArgs e)
    {
        if (_module.Engine == null) return;
        if (_module.Engine.IsMonitoring)
        {
            _module.Engine.StopMonitoring();
        }
        else
        {
            _module.Engine.StartMonitoring();
        }
        RefreshStatus();
    }

    private async void BtnScanNow_Click(object sender, RoutedEventArgs e)
    {
        if (_module.Engine == null) return;
        BtnScanNow.IsEnabled = false;
        BtnScanNow.Content = "Scanning...";
        try
        {
            var count = await _module.Engine.RunReconciliationScanAsync();
            MessageBox.Show($"Reconciliation scan completed. Processed {count} files.", "Scan Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        finally
        {
            BtnScanNow.IsEnabled = true;
            BtnScanNow.Content = "Scan Now";
            RefreshStatus();
        }
    }

    private async void BtnDownloadModel_Click(object sender, RoutedEventArgs e)
    {
        if (_module.ModelMgr == null) return;
        BtnDownloadModel.IsEnabled = false;
        PanelDownloadProgress.Visibility = Visibility.Visible;

        try
        {
            await _module.ModelMgr.DownloadModelAsync();
            _module.EmbeddingEng?.ReloadModel();
            MessageBox.Show("Neural embedding model successfully downloaded and activated!", "Model Activated", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Model download failed: {ex.Message}", "Download Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnDownloadModel.IsEnabled = true;
            RefreshStatus();
        }
    }
}
