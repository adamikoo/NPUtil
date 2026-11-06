using System.Windows;
using System.Windows.Controls;
using NPUtil.App.Theme;
using NPUtil.Core.Localization;
using NPUtil.Core.Settings;

namespace NPUtil.App.Views.Settings;

public partial class GlobalSettingsView : UserControl
{
    private readonly SettingsManager _settingsManager;

    public GlobalSettingsView(SettingsManager settingsManager)
    {
        InitializeComponent();
        _settingsManager = settingsManager;

        Loaded += (s, e) => LoadSettings();
    }

    private void LoadSettings()
    {
        var current = _settingsManager.Current;

        CmbAppTheme.SelectedIndex = current.Theme switch
        {
            AppTheme.Light => 1,
            AppTheme.Dark => 2,
            _ => 0
        };

        CmbLanguage.SelectedIndex = (current.Language?.ToLowerInvariant() == "cs") ? 1 : 0;

        ChkStartWithWindows.IsChecked = StartupHelper.IsStartupEnabled();
        ChkMinimizeToTray.IsChecked = current.MinimizeToTray;
        ChkCloseToTray.IsChecked = current.CloseToTray;
        ChkThrottleBattery.IsChecked = current.ThrottleOnBattery;

        // Lens Hotkey
        var hotkey = current.LensHotkey ?? "Ctrl+Alt+L";
        CmbLensHotkey.SelectedIndex = hotkey switch
        {
            "Ctrl+Alt+O" => 1,
            "Ctrl+Shift+S" => 2,
            "Ctrl+Shift+L" => 3,
            "Alt+Space" => 4,
            _ => 0
        };
    }

    private void CmbAppTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;

        var selectedTheme = CmbAppTheme.SelectedIndex switch
        {
            1 => AppTheme.Light,
            2 => AppTheme.Dark,
            _ => AppTheme.System
        };

        ThemeManager.ApplyTheme(selectedTheme);
    }

    private void CmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;

        var selectedLang = CmbLanguage.SelectedIndex == 1 ? "cs" : "en";
        LocalizationManager.Instance.CurrentLanguage = selectedLang;
        _settingsManager.Update(s => s.Language = selectedLang);
    }

    private void BtnSaveGlobalSettings_Click(object sender, RoutedEventArgs e)
    {
        var selectedTheme = CmbAppTheme.SelectedIndex switch
        {
            1 => AppTheme.Light,
            2 => AppTheme.Dark,
            _ => AppTheme.System
        };

        var selectedLang = CmbLanguage.SelectedIndex == 1 ? "cs" : "en";
        LocalizationManager.Instance.CurrentLanguage = selectedLang;

        var enableStartup = ChkStartWithWindows.IsChecked == true;
        StartupHelper.SetStartup(enableStartup);

        var selectedHotkey = (CmbLensHotkey.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Ctrl+Alt+L";

        _settingsManager.Update(s =>
        {
            s.Theme = selectedTheme;
            s.Language = selectedLang;
            s.StartWithWindows = enableStartup;
            s.MinimizeToTray = ChkMinimizeToTray.IsChecked == true;
            s.CloseToTray = ChkCloseToTray.IsChecked == true;
            s.ThrottleOnBattery = ChkThrottleBattery.IsChecked == true;
            s.LensHotkey = selectedHotkey;
        });

        TxtGlobalFeedback.Text = LocalizationManager.Get("SettingsSaved");
    }
}
