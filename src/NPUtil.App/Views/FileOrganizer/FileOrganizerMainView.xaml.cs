using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;
using NPUtil.Core.Localization;
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
        var reviewText = LocalizationManager.Get("TabReview");
        NavReviewItem.Content = count > 0 ? $"{reviewText} ({count})" : reviewText;
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

        FrameworkElement? target = tag switch
        {
            "Overview" => _overviewView,
            "Review" => _reviewQueueView,
            "Explorer" => _folderExplorerView,
            "Search" => _searchView,
            "Activity" => _activityView,
            "Settings" => _settingsView,
            _ => null
        };

        if (target != null)
        {
            if (tag == "Overview") _overviewView.RefreshStatus();
            else if (tag == "Review") _reviewQueueView.RefreshQueue();
            else if (tag == "Activity") _activityView.RefreshHistory();

            ContentArea.Content = target;
            NPUtil.App.Common.FluentAnimationHelper.AnimateEntrance(target);
        }
    }
}
