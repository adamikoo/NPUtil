using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;
using NPUtil.Modules.FileOrganizer;

namespace NPUtil.App.Views.FileOrganizer;

public partial class FileOrganizerMainView : System.Windows.Controls.UserControl
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

        Loaded += (s, e) =>
        {
            if (TopNav.MenuItems.Count > 0 && TopNav.MenuItems[0] is NavigationViewItem firstItem)
            {
                firstItem.IsActive = true;
            }
        };
    }

    private void UpdateTabBadge()
    {
        if (_module.Engine == null) return;
        var count = _module.Engine.GetReviewQueue().Count;
        NavReviewItem.Content = count > 0 ? $"Fronta k revizi ({count})" : "Fronta k revizi";
    }

    private void NavItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is NavigationViewItem item)
        {
            SwitchTab(item.Tag?.ToString());
        }
    }

    private void TopNav_SelectionChanged(NavigationView sender, RoutedEventArgs args)
    {
        if (sender.SelectedItem is NavigationViewItem item)
        {
            SwitchTab(item.Tag?.ToString());
        }
        else if (args.Source is NavigationViewItem sourceItem)
        {
            SwitchTab(sourceItem.Tag?.ToString());
        }
    }

    private void SwitchTab(string? tag)
    {
        if (string.IsNullOrEmpty(tag)) return;

        foreach (var menuItem in TopNav.MenuItems)
        {
            if (menuItem is NavigationViewItem navItem)
            {
                navItem.IsActive = (navItem.Tag?.ToString() == tag);
            }
        }

        switch (tag)
        {
            case "Overview":
                _overviewView.RefreshStatus();
                ContentArea.Content = _overviewView;
                break;
            case "Review":
                _reviewQueueView.RefreshQueue();
                ContentArea.Content = _reviewQueueView;
                break;
            case "Explorer":
                ContentArea.Content = _folderExplorerView;
                break;
            case "Search":
                ContentArea.Content = _searchView;
                break;
            case "Activity":
                _activityView.RefreshHistory();
                ContentArea.Content = _activityView;
                break;
            case "Settings":
                ContentArea.Content = _settingsView;
                break;
        }
    }
}
