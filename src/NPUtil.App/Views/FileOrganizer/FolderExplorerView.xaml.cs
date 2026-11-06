using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using NPUtil.Modules.FileOrganizer;
using NPUtil.Modules.FileOrganizer.Core;

namespace NPUtil.App.Views.FileOrganizer;

public partial class FolderExplorerView : UserControl
{
    private readonly FileOrganizerModule _module;
    private CategoryDefinition? _selectedCategory;

    public FolderExplorerView(FileOrganizerModule module)
    {
        InitializeComponent();
        _module = module;

        if (_module.Taxonomy != null)
        {
            _module.Taxonomy.TaxonomyChanged += (s, e) => Dispatcher.Invoke(LoadCategories);
        }

        Loaded += (s, e) => LoadCategories();
    }

    private void LoadCategories()
    {
        if (_module.Taxonomy == null) return;
        var categories = _module.Taxonomy.GetCategories();
        ListCategories.ItemsSource = categories;

        if (categories.Count > 0 && ListCategories.SelectedItem == null)
        {
            ListCategories.SelectedIndex = 0;
        }
    }

    private void ListCategories_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ListCategories.SelectedItem is CategoryDefinition cat)
        {
            _selectedCategory = cat;
            TxtCategoryName.Text = cat.Name;
            TxtRelativePath.Text = cat.RelativePath;
            TxtDescription.Text = cat.Description;
            TxtKeywords.Text = string.Join(", ", cat.Keywords);
            TxtExtensions.Text = string.Join(", ", cat.FileExtensions);
            ChkExcluded.IsChecked = cat.IsExcluded;
        }
    }

    private void BtnSaveChanges_Click(object sender, RoutedEventArgs e)
    {
        if (_module.Taxonomy == null) return;

        var relativePath = TxtRelativePath.Text.Trim();
        var name = TxtCategoryName.Text.Trim();
        if (string.IsNullOrEmpty(relativePath) || string.IsNullOrEmpty(name))
        {
            MessageBox.Show("Please enter both category name and relative path.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var keywords = TxtKeywords.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var extensions = TxtExtensions.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        var cat = new CategoryDefinition
        {
            RelativePath = relativePath,
            Name = name,
            Description = TxtDescription.Text.Trim(),
            Keywords = keywords,
            FileExtensions = extensions,
            IsExcluded = ChkExcluded.IsChecked == true
        };

        _module.Taxonomy.AddOrUpdateCategory(cat);
        MessageBox.Show("Category saved successfully.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        LoadCategories();
    }

    private void BtnNewCategory_Click(object sender, RoutedEventArgs e)
    {
        _selectedCategory = null;
        TxtCategoryName.Text = "New Category";
        TxtRelativePath.Text = @"Work\NewCategory";
        TxtDescription.Text = "Description of files belonging in this category for semantic matching";
        TxtKeywords.Text = "keyword1, keyword2";
        TxtExtensions.Text = ".pdf, .docx";
        ChkExcluded.IsChecked = false;
        TxtCategoryName.Focus();
    }

    private void BtnDeleteCategory_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCategory == null || _module.Taxonomy == null) return;

        var result = MessageBox.Show($"Are you sure you want to remove category '{_selectedCategory.Name}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            _module.Taxonomy.RemoveCategory(_selectedCategory.RelativePath);
            LoadCategories();
        }
    }

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_module.Engine == null || _selectedCategory == null) return;
        var fullPath = System.IO.Path.Combine(_module.Engine.WatchedFolder, _selectedCategory.RelativePath);
        Directory.CreateDirectory(fullPath);
        Process.Start(new ProcessStartInfo
        {
            FileName = fullPath,
            UseShellExecute = true
        });
    }
}
