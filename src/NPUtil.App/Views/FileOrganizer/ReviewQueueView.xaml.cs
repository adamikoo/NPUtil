using System.Windows;
using System.Windows.Controls;
using NPUtil.Modules.FileOrganizer;
using NPUtil.Modules.FileOrganizer.Core;
using NPUtil.Modules.FileOrganizer.Models;

namespace NPUtil.App.Views.FileOrganizer;

public partial class ReviewQueueView : UserControl
{
    private readonly FileOrganizerModule _module;

    public ReviewQueueView(FileOrganizerModule module)
    {
        InitializeComponent();
        _module = module;

        if (_module.Engine != null)
        {
            _module.Engine.ReviewQueueChanged += (s, e) => Dispatcher.Invoke(RefreshQueue);
        }

        Loaded += (s, e) =>
        {
            LoadCategoriesDropdown();
            RefreshQueue();
        };
    }

    private void LoadCategoriesDropdown()
    {
        if (_module.Taxonomy == null) return;
        var categories = _module.Taxonomy.GetCategories().Select(c => c.RelativePath).ToList();
        CmbAllCategories.ItemsSource = categories;
        if (categories.Count > 0) CmbAllCategories.SelectedIndex = 0;
    }

    public void RefreshQueue()
    {
        if (_module.Engine == null) return;

        var items = _module.Engine.GetReviewQueue();
        GridReviewQueue.ItemsSource = items;
        TxtPendingCount.Text = $"{items.Count} items awaiting review";
        BtnApproveAll.IsEnabled = items.Count > 0;
    }

    private void GridReviewQueue_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridReviewQueue.SelectedItem is ReviewItem item)
        {
            TxtSelectedExplanation.Text = $"{item.FileName}\n{item.Explanation}\nExtracted Keywords: [{string.Join(", ", item.Keywords)}]";
            if (item.Alternatives.Count > 0)
            {
                PanelAlternatives.Visibility = Visibility.Visible;
                ListAlternatives.ItemsSource = item.Alternatives;
            }
            else
            {
                PanelAlternatives.Visibility = Visibility.Collapsed;
            }

            CmbAllCategories.SelectedItem = item.SuggestedCategory;
        }
        else
        {
            TxtSelectedExplanation.Text = "Select an item above to inspect classification reasoning and alternative categories.";
            PanelAlternatives.Visibility = Visibility.Collapsed;
        }
    }

    private async void BtnApproveRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ReviewItem item && _module.Engine != null)
        {
            await _module.Engine.ApproveItemAsync(item.Id);
            RefreshQueue();
        }
    }

    private void BtnRejectRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ReviewItem item && _module.Engine != null)
        {
            _module.Engine.RejectItem(item.Id);
            RefreshQueue();
        }
    }

    private async void BtnChooseAlternative_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is DestinationSuggestion suggestion &&
            GridReviewQueue.SelectedItem is ReviewItem item && _module.Engine != null)
        {
            await _module.Engine.ApproveItemAsync(item.Id, suggestion.RelativePath);
            RefreshQueue();
        }
    }

    private async void BtnApplyCustomCategory_Click(object sender, RoutedEventArgs e)
    {
        if (GridReviewQueue.SelectedItem is ReviewItem item &&
            CmbAllCategories.SelectedItem is string newCategory &&
            _module.Engine != null)
        {
            await _module.Engine.ApproveItemAsync(item.Id, newCategory);
            RefreshQueue();
        }
    }

    private async void BtnApproveAll_Click(object sender, RoutedEventArgs e)
    {
        if (_module.Engine == null) return;
        BtnApproveAll.IsEnabled = false;
        try
        {
            var count = await _module.Engine.ApproveAllPendingAsync();
            MessageBox.Show($"Approved and organized {count} files.", "Batch Approval Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        finally
        {
            RefreshQueue();
        }
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshQueue();
    }
}
