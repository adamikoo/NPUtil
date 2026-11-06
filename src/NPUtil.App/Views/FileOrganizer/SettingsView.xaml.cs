using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NPUtil.Modules.FileOrganizer;
using NPUtil.Modules.FileOrganizer.Models;

namespace NPUtil.App.Views.FileOrganizer;

public partial class SettingsView : UserControl
{
    private readonly FileOrganizerModule _module;

    public SettingsView(FileOrganizerModule module)
    {
        InitializeComponent();
        _module = module;

        Loaded += (s, e) => LoadCurrentSettings();
    }

    private void LoadCurrentSettings()
    {
        if (_module.Engine == null) return;

        TxtWatchedPath.Text = _module.Engine.WatchedFolder;

        switch (_module.Engine.Mode)
        {
            case OrganizerMode.ReviewEverything:
                RadioReviewEverything.IsChecked = true;
                break;
            case OrganizerMode.IntelligentAuto:
                RadioIntelligentAuto.IsChecked = true;
                break;
            case OrganizerMode.FullyAutomatic:
                RadioFullyAutomatic.IsChecked = true;
                break;
        }

        SliderThreshold.Value = _module.Engine.HighConfidenceThreshold * 100.0;
        TxtThresholdValue.Text = $"{SliderThreshold.Value:F0}%";
    }

    private void SliderThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtThresholdValue != null)
        {
            TxtThresholdValue.Text = $"{e.NewValue:F0}%";
        }
    }

    private void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Downloads or Target Folder to Organize",
            InitialDirectory = TxtWatchedPath.Text
        };

        if (dialog.ShowDialog() == true)
        {
            TxtWatchedPath.Text = dialog.FolderName;
        }
    }

    private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
    {
        if (_module.Engine == null) return;

        if (RadioReviewEverything.IsChecked == true)
            _module.Engine.Mode = OrganizerMode.ReviewEverything;
        else if (RadioIntelligentAuto.IsChecked == true)
            _module.Engine.Mode = OrganizerMode.IntelligentAuto;
        else if (RadioFullyAutomatic.IsChecked == true)
            _module.Engine.Mode = OrganizerMode.FullyAutomatic;

        _module.Engine.HighConfidenceThreshold = SliderThreshold.Value / 100.0;

        var targetPath = TxtWatchedPath.Text.Trim();
        if (Directory.Exists(targetPath) && !targetPath.Equals(_module.Engine.WatchedFolder, StringComparison.OrdinalIgnoreCase))
        {
            _module.Engine.StartMonitoring(targetPath);
        }

        TxtSaveFeedback.Text = "Settings saved successfully.";
    }
}
