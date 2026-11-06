using System.Windows;
using NPUtil.App.Theme;
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

public partial class MainWindow : Window
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

    public MainWindow()
    {
        InitializeComponent();

        AppLogger.Initialize();
        ThemeManager.ApplyTheme(_settingsManager.Current.Theme);

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

        // 4. Set Initial View
        MainContentHost.Content = _fileOrganizerView;

        // 5. Update Status Bar
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
        }

        StatusPower.Text = diag.IsOnBattery ? $"Battery: {diag.BatteryPercent}%" : "AC Power";
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;

        if (sender == NavFileOrganizer && _fileOrganizerView != null)
        {
            MainContentHost.Content = _fileOrganizerView;
        }
        else if (sender == NavHardware && _hardwareView != null)
        {
            _hardwareView.RefreshDiagnostics();
            MainContentHost.Content = _hardwareView;
        }
        else if (sender == NavGlobalSettings && _globalSettingsView != null)
        {
            MainContentHost.Content = _globalSettingsView;
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
