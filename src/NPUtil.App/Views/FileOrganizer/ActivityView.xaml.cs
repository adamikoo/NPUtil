using System.Windows;
using System.Windows.Controls;
using NPUtil.Core.Localization;
using NPUtil.Modules.FileOrganizer;
using NPUtil.Modules.FileOrganizer.Models;

namespace NPUtil.App.Views.FileOrganizer;

public partial class ActivityView : UserControl
{
    private readonly FileOrganizerModule _module;
    private OrganizeOperationRecord? _selectedRecord;

    public ActivityView(FileOrganizerModule module)
    {
        InitializeComponent();
        _module = module;

        Loaded += (s, e) => RefreshHistory();
    }

    public void RefreshHistory()
    {
        if (_module.HistoryDb == null) return;

        var list = _module.HistoryDb.GetRecentOperations(200);
        GridOperations.ItemsSource = list;

        var (total, active, undone) = _module.HistoryDb.GetCounts();
        TxtHistorySummary.Text = LocalizationManager.Instance.GetString("HistorySummaryFormat", total, active, undone);
        BtnUndoBatch.IsEnabled = _selectedRecord != null && !_selectedRecord.IsUndone;
    }

    private void GridOperations_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridOperations.SelectedItem is OrganizeOperationRecord record)
        {
            _selectedRecord = record;
            BtnUndoBatch.IsEnabled = !record.IsUndone;
        }
        else
        {
            _selectedRecord = null;
            BtnUndoBatch.IsEnabled = false;
        }
    }

    private async void BtnUndoRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is OrganizeOperationRecord record && _module.Undo != null)
        {
            var res = await _module.Undo.UndoOperationAsync(record.Id);
            if (res.Success)
            {
                var title = LocalizationManager.Get("UndoSuccessTitle");
                var msg = LocalizationManager.Instance.GetString("UndoSuccessMsg", res.RestoredPath ?? string.Empty);
                MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                var title = LocalizationManager.Get("UndoFailedTitle");
                var msg = LocalizationManager.Instance.GetString("UndoFailedMsg", res.ErrorMessage ?? string.Empty);
                MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            RefreshHistory();
        }
    }

    private async void BtnUndoBatch_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedRecord == null || _module.Undo == null) return;

        var confirmTitle = LocalizationManager.Get("BatchUndoConfirmTitle");
        var confirmMsg = LocalizationManager.Instance.GetString("BatchUndoConfirmMsg", _selectedRecord.BatchId);
        var confirm = MessageBox.Show(confirmMsg, confirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm == MessageBoxResult.Yes)
        {
            var results = await _module.Undo.UndoBatchAsync(_selectedRecord.BatchId);
            var successCount = results.Count(r => r.Success);
            var doneTitle = LocalizationManager.Get("BatchUndoDoneTitle");
            var doneMsg = LocalizationManager.Instance.GetString("BatchUndoDoneMsg", successCount, results.Count);
            MessageBox.Show(doneMsg, doneTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            RefreshHistory();
        }
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e) => RefreshHistory();
}
