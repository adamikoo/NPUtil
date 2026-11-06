using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using NPUtil.App.Views.FileOrganizer;
using NPUtil.App.Views.Hardware;
using NPUtil.App.Views.Settings;
using NPUtil.Core.Hardware;
using NPUtil.Core.Logging;
using NPUtil.Core.Modules;
using NPUtil.Core.Services;
using NPUtil.Core.Settings;
using NPUtil.Modules.FileOrganizer;

namespace NPUtil.App;

public partial class MainWindow : FluentWindow
{
    private readonly AppLogger _logger = new();
    private readonly HardwareService _hardwareService = new();
    private readonly NotificationService _notificationService = new();
    private readonly SettingsManager _settingsManager = new();
    private readonly ModuleRegistry _moduleRegistry = new();

    private FileOrganizerModule? _fileOrganizerModule;
    private FileOrganizerMainView? _fileOrganizerView;
    private HardwareView? _hardwareView;
    private GlobalSettingsView? _globalSettingsView;
    private TrayIconHelper? _trayHelper;

    public MainWindow()
    {
        InitializeComponent();

        AppLogger.Initialize();

        // Apply theme and watch Windows system theme changes automatically
        SystemThemeWatcher.Watch(this);

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // 1. Initialize File Organizer Module
        _fileOrganizerModule = new FileOrganizerModule();

        var serviceProvider = new SimpleServiceProvider();
        serviceProvider.Register<ILogger>(_logger);
        serviceProvider.Register<IHardwareService>(_hardwareService);
        serviceProvider.Register<INotificationService>(_notificationService);
        serviceProvider.Register<SettingsManager>(_settingsManager);

        await _fileOrganizerModule.InitializeAsync(serviceProvider);
        _moduleRegistry.Register(_fileOrganizerModule);

        // 2. Initialize Views
        _fileOrganizerView = new FileOrganizerMainView(_fileOrganizerModule);
        _hardwareView = new HardwareView(_hardwareService);
        _globalSettingsView = new GlobalSettingsView(_settingsManager);

        // 3. Start Downloads monitoring if configured
        _fileOrganizerModule.Engine?.StartMonitoring();

        // 4. Initialize System Tray Integration
        _trayHelper = new TrayIconHelper(this, _logger);
        _trayHelper.Initialize();
        _trayHelper.TrayDoubleClicked += () => Dispatcher.Invoke(() =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        });

        // 5. Set Initial View
        RootFrame.Navigate(_fileOrganizerView);

        // 6. Update Status Bar
        UpdateStatusBar();

        if (_fileOrganizerModule.Engine != null)
        {
            _fileOrganizerModule.Engine.StatusChanged += (s, ev) => Dispatcher.Invoke(UpdateStatusBar);
        }
    }

    private void UpdateStatusBar()
    {
        var diag = _hardwareService.GetDiagnostics();
        StatusBackend.Text = $"AI Engine: {diag.ActiveAiBackend}";

        if (_fileOrganizerModule?.Engine != null)
        {
            var folder = System.IO.Path.GetFileName(_fileOrganizerModule.Engine.WatchedFolder);
            StatusMonitoring.Text = _fileOrganizerModule.Engine.IsMonitoring
                ? $"Monitoring: Active ({folder})"
                : "Monitoring: Paused";

            _trayHelper?.UpdateTooltip($"NPUtil — File Organizer ({(_fileOrganizerModule.Engine.IsMonitoring ? "Active" : "Paused")})");
        }

        StatusPower.Text = diag.IsOnBattery ? $"Battery: {diag.BatteryPercent}%" : "AC Power";
    }

    private void RootNavigation_SelectionChanged(NavigationView sender, RoutedEventArgs args)
    {
        if (!IsLoaded) return;

        if (sender.SelectedItem is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            if (tag == "FileOrganizer" && _fileOrganizerView != null)
            {
                RootFrame.Navigate(_fileOrganizerView);
            }
            else if (tag == "Hardware" && _hardwareView != null)
            {
                _hardwareView.RefreshDiagnostics();
                RootFrame.Navigate(_hardwareView);
            }
            else if (tag == "Settings" && _globalSettingsView != null)
            {
                RootFrame.Navigate(_globalSettingsView);
            }
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_settingsManager.Current.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        _trayHelper?.Dispose();
        _fileOrganizerModule?.ShutdownAsync().Wait(1000);
    }

    private class SimpleServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services = new();

        public void Register<T>(T instance) where T : class
        {
            _services[typeof(T)] = instance;
        }

        public object? GetService(Type serviceType) =>
            _services.TryGetValue(serviceType, out var val) ? val : null;
    }
}
