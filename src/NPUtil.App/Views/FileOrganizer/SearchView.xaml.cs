using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NPUtil.Modules.FileOrganizer;
using NPUtil.Modules.FileOrganizer.Search;

namespace NPUtil.App.Views.FileOrganizer;

public partial class SearchView : UserControl
{
    private readonly FileOrganizerModule _module;
    private SearchResultItem? _selectedItem;

    public SearchView(FileOrganizerModule module)
    {
        InitializeComponent();
        _module = module;

        Loaded += (s, e) =>
        {
            LoadFilters();
            PerformSearch();
        };
    }

    private void LoadFilters()
    {
        if (_module.Taxonomy != null)
        {
            var cats = new List<string> { "All Categories" };
            cats.AddRange(_module.Taxonomy.GetCategories().Select(c => c.Name));
            CmbCategoryFilter.ItemsSource = cats;
            CmbCategoryFilter.SelectedIndex = 0;
        }

        var exts = new List<string> { "All Types", ".pdf", ".docx", ".xlsx", ".pptx", ".txt", ".md", ".cs", ".py", ".jpg", ".png", ".zip", ".exe" };
        CmbExtensionFilter.ItemsSource = exts;
        CmbExtensionFilter.SelectedIndex = 0;
    }

    private async void PerformSearch()
    {
        if (_module.SearchIndex == null) return;

        var query = TxtSearchQuery.Text.Trim();
        var catFilter = CmbCategoryFilter.SelectedIndex > 0 ? (string)CmbCategoryFilter.SelectedItem : null;
        var extFilter = CmbExtensionFilter.SelectedIndex > 0 ? (string)CmbExtensionFilter.SelectedItem : null;

        BtnSearch.IsEnabled = false;
        try
        {
            var results = await _module.SearchIndex.SearchAsync(query, catFilter, extFilter);
            GridSearchResults.ItemsSource = results;
            TxtResultCount.Text = $"{results.Count} results found";
        }
        finally
        {
            BtnSearch.IsEnabled = true;
        }
    }

    private void BtnSearch_Click(object sender, RoutedEventArgs e) => PerformSearch();

    private void TxtSearchQuery_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            PerformSearch();
        }
    }

    private void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) PerformSearch();
    }

    private void GridSearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridSearchResults.SelectedItem is SearchResultItem item)
        {
            _selectedItem = item;
            TxtSelectedFilePath.Text = item.FilePath;
            BtnOpenFile.IsEnabled = File.Exists(item.FilePath);
            BtnOpenFolder.IsEnabled = File.Exists(item.FilePath);
            BtnCopyPath.IsEnabled = true;
        }
        else
        {
            _selectedItem = null;
            TxtSelectedFilePath.Text = "Select a search result to view location.";
            BtnOpenFile.IsEnabled = false;
            BtnOpenFolder.IsEnabled = false;
            BtnCopyPath.IsEnabled = false;
        }
    }

    private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedItem != null && File.Exists(_selectedItem.FilePath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _selectedItem.FilePath,
                UseShellExecute = true
            });
        }
    }

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedItem != null && File.Exists(_selectedItem.FilePath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{_selectedItem.FilePath}\"",
                UseShellExecute = true
            });
        }
    }

    private void BtnCopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedItem != null)
        {
            Clipboard.SetText(_selectedItem.FilePath);
        }
    }
}
