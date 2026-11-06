using System.Windows;
using System.Windows.Controls;
using NPUtil.Modules.FileOrganizer;

namespace NPUtil.App.Views.FileOrganizer;

public partial class FileOrganizerMainView : UserControl
{
    private readonly FileOrganizerModule _module;

    private readonly OverviewView _overviewView;
    private readonly ReviewQueueView _reviewQueueView;
    private readonly FolderExplorerView _folderExplorerView;
    private readonly SearchView _searchView;
    private readonly ActivityView _activityView;
    private readonly SettingsView _settingsView;

    public FileOrganizerMainView(FileOrganizerModule module)
    {
        InitializeComponent();
        _module = module;

        _overviewView = new OverviewView(module);
        _reviewQueueView = new ReviewQueueView(module);
        _folderExplorerView = new FolderExplorerView(module);
        _searchView = new SearchView(module);
        _activityView = new ActivityView(module);
        _settingsView = new SettingsView(module);

        ContentArea.Content = _overviewView;

        if (_module.Engine != null)
        {
            _module.Engine.ReviewQueueChanged += (s, e) => Dispatcher.Invoke(UpdateTabBadge);
            UpdateTabBadge();
        }
    }

    private void UpdateTabBadge()
    {
        if (_module.Engine == null) return;
        var count = _module.Engine.GetReviewQueue().Count;
        TabReviewQueue.Content = count > 0 ? $"Review Queue ({count})" : "Review Queue";
    }

    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;

        if (sender == TabOverview)
        {
            _overviewView.RefreshStatus();
            ContentArea.Content = _overviewView;
        }
        else if (sender == TabReviewQueue)
        {
            _reviewQueueView.RefreshQueue();
            ContentArea.Content = _reviewQueueView;
        }
        else if (sender == TabExplorer)
        {
            ContentArea.Content = _folderExplorerView;
        }
        else if (sender == TabSearch)
        {
            ContentArea.Content = _searchView;
        }
        else if (sender == TabActivity)
        {
            _activityView.RefreshHistory();
            ContentArea.Content = _activityView;
        }
        else if (sender == TabSettings)
        {
            ContentArea.Content = _settingsView;
        }
    }
}
