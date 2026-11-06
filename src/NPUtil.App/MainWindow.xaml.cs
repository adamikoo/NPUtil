using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using NPUtil.App.Views.FileOrganizer;
using NPUtil.App.Views.Hardware;
using NPUtil.App.Views.Settings;
using NPUtil.Core.AI;
using NPUtil.Core.Context;
using NPUtil.Core.Extraction;
using NPUtil.Core.Hardware;
using NPUtil.Core.Localization;
using NPUtil.Core.Logging;
using NPUtil.Core.Modules;
using NPUtil.Core.OCR;
using NPUtil.Core.Services;
using NPUtil.Core.Settings;
using NPUtil.Core.Translation;
using NPUtil.Modules.ContextAssistant;
using NPUtil.Modules.ContextAssistant.UI;
using NPUtil.Modules.FileOrganizer;
using NPUtil.Modules.SmartDataExtractor;
using NPUtil.Modules.SmartDataExtractor.UI;
using NPUtil.Modules.UniversalOcr;
using NPUtil.Modules.UniversalOcr.UI;

namespace NPUtil.App;

public partial class MainWindow : FluentWindow
{
    private readonly AppLogger _logger = new();
    private readonly HardwareService _hardwareService = new();
    private readonly NotificationService _notificationService = new();
    private readonly SettingsManager _settingsManager = new();
    private readonly ModuleRegistry _moduleRegistry = new();

    private FileOrganizerModule? _fileOrganizerModule;
    private ContextAssistantModule? _contextAssistantModule;
    private SmartDataExtractorModule? _smartDataExtractorModule;
    private UniversalOcrModule? _universalOcrModule;

    private FileOrganizerMainView? _fileOrganizerView;
    private ContextAssistantMainView? _contextAssistantView;
    private SmartDataExtractorMainView? _smartDataExtractorView;
    private UniversalOcrMainView? _universalOcrView;

    private HardwareView? _hardwareView;
    private GlobalSettingsView? _globalSettingsView;
    private TrayIconHelper? _trayHelper;

    private HwndSource? _hwndSource;
    private const int HOTKEY_ID_ASSISTANT = 9001;
    private const int HOTKEY_ID_OCR = 9002;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint WM_HOTKEY = 0x0312;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public MainWindow()
    {
        InitializeComponent();

        AppLogger.Initialize();

        // Apply persisted language (stock default: English)
        LocalizationManager.Instance.CurrentLanguage = _settingsManager.Current.Language ?? "en";

        // Apply theme and watch Windows system theme changes automatically
        SystemThemeWatcher.Watch(this);

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // 1. Setup Shared Infrastructure Services
        var modelManager = new ModelManager(_hardwareService, _logger);
        var inferenceEngine = new LocalInferenceEngine(modelManager, _logger);
        var translationService = new LocalTranslationService(_logger);
        var documentExtractor = new DocumentExtractor();
        var ocrService = new WindowsOcrService(_logger);
        var captureService = new ContextCaptureService(_logger);

        var serviceProvider = new SimpleServiceProvider();
        serviceProvider.Register<ILogger>(_logger);
        serviceProvider.Register<IHardwareService>(_hardwareService);
        serviceProvider.Register<INotificationService>(_notificationService);
        serviceProvider.Register<SettingsManager>(_settingsManager);
        serviceProvider.Register<IModelManager>(modelManager);
        serviceProvider.Register<IInferenceEngine>(inferenceEngine);
        serviceProvider.Register<ITranslationService>(translationService);
        serviceProvider.Register<IDocumentExtractor>(documentExtractor);
        serviceProvider.Register<IOcrService>(ocrService);
        serviceProvider.Register<IContextCaptureService>(captureService);

        // 2. Initialize Module 0: File Organizer
        _fileOrganizerModule = new FileOrganizerModule();
        await _fileOrganizerModule.InitializeAsync(serviceProvider);
        _moduleRegistry.Register(_fileOrganizerModule);
        _fileOrganizerView = new FileOrganizerMainView(_fileOrganizerModule);
        _fileOrganizerModule.Engine?.StartMonitoring();

        // 3. Initialize Module 1: Context Assistant
        _contextAssistantModule = new ContextAssistantModule();
        await _contextAssistantModule.InitializeAsync(serviceProvider);
        _moduleRegistry.Register(_contextAssistantModule);
        _contextAssistantView = (ContextAssistantMainView)_contextAssistantModule.CreateView();

        // 4. Initialize Module 2: Smart Data Extractor
        _smartDataExtractorModule = new SmartDataExtractorModule();
        await _smartDataExtractorModule.InitializeAsync(serviceProvider);
        _moduleRegistry.Register(_smartDataExtractorModule);
        _smartDataExtractorView = (SmartDataExtractorMainView)_smartDataExtractorModule.CreateView();

        // 5. Initialize Module 3: Universal OCR & Translator
        _universalOcrModule = new UniversalOcrModule();
        _universalOcrModule.OnSendToAssistant = text => Dispatcher.Invoke(() =>
        {
            NavigateToTag("ContextAssistant");
            _contextAssistantView?.SetContextText(text);
        });
        _universalOcrModule.OnSendToExtractor = text => Dispatcher.Invoke(() =>
        {
            NavigateToTag("SmartDataExtractor");
            _smartDataExtractorView?.SetExtractedText(text);
        });
        await _universalOcrModule.InitializeAsync(serviceProvider);
        _moduleRegistry.Register(_universalOcrModule);
        _universalOcrView = (UniversalOcrMainView)_universalOcrModule.CreateView();

        // 6. Initialize Hardware & Settings Views
        _hardwareView = new HardwareView(_hardwareService);
        _globalSettingsView = new GlobalSettingsView(_settingsManager);

        // 7. Initialize System Tray Integration
        _trayHelper = new TrayIconHelper(this, _logger);
        _trayHelper.Initialize();
        _trayHelper.TrayDoubleClicked += () => Dispatcher.Invoke(() =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        });

        // 8. Register Global Shortcuts (Ctrl+Alt+A for Assistant, Ctrl+Alt+O for OCR Snip)
        RegisterGlobalShortcuts();

        // 9. Set Initial View
        NavigateToTag("FileOrganizer");

        // 10. Update Status Bar
        UpdateStatusBar();

        if (_fileOrganizerModule.Engine != null)
        {
            _fileOrganizerModule.Engine.StatusChanged += (s, ev) => Dispatcher.Invoke(UpdateStatusBar);
        }

        LocalizationManager.Instance.PropertyChanged += (s, ev) => Dispatcher.Invoke(UpdateStatusBar);
    }

    private void RegisterGlobalShortcuts()
    {
        try
        {
            var helper = new WindowInteropHelper(this);
            _hwndSource = HwndSource.FromHwnd(helper.Handle);
            _hwndSource.AddHook(HwndHook);

            // Ctrl+Alt+A (Context Assistant)
            RegisterHotKey(helper.Handle, HOTKEY_ID_ASSISTANT, MOD_CONTROL | MOD_ALT, 0x41);

            // Dynamic Lens Hotkey (Default Ctrl+Alt+L or user configured)
            RegisterLensHotkey(helper.Handle);

            _settingsManager.SettingsChanged += (s, e) => Dispatcher.Invoke(() =>
            {
                if (_hwndSource != null)
                {
                    RegisterLensHotkey(helper.Handle);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.Warning("MainWindow", $"Failed to register global hotkeys: {ex.Message}", ex);
        }
    }

    private void RegisterLensHotkey(IntPtr hWnd)
    {
        UnregisterHotKey(hWnd, HOTKEY_ID_OCR);

        var hotkeyStr = _settingsManager.Current.LensHotkey ?? "Ctrl+Alt+L";
        uint mod = 0;
        uint vk = 0x4C; // 'L'

        if (hotkeyStr.Contains("Ctrl")) mod |= MOD_CONTROL;
        if (hotkeyStr.Contains("Alt")) mod |= MOD_ALT;
        if (hotkeyStr.Contains("Shift")) mod |= MOD_SHIFT;
        if (hotkeyStr.Contains("Win")) mod |= MOD_WIN;

        if (hotkeyStr.EndsWith("L", StringComparison.OrdinalIgnoreCase)) vk = 0x4C;
        else if (hotkeyStr.EndsWith("O", StringComparison.OrdinalIgnoreCase)) vk = 0x4F;
        else if (hotkeyStr.EndsWith("S", StringComparison.OrdinalIgnoreCase)) vk = 0x53;
        else if (hotkeyStr.EndsWith("Space", StringComparison.OrdinalIgnoreCase)) vk = 0x20;

        RegisterHotKey(hWnd, HOTKEY_ID_OCR, mod, vk);
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (id == HOTKEY_ID_ASSISTANT && _contextAssistantModule?.Engine != null)
            {
                handled = true;
                Dispatcher.Invoke(() =>
                {
                    var popup = new ContextAssistantPopupWindow(_contextAssistantModule.Engine, () =>
                    {
                        Show();
                        WindowState = WindowState.Normal;
                        Activate();
                        NavigateToTag("ContextAssistant");
                    });
                    popup.Show();
                });
            }
            else if (id == HOTKEY_ID_OCR)
            {
                handled = true;
                Dispatcher.Invoke(TriggerLensCapture);
            }
        }
        return IntPtr.Zero;
    }

    public void TriggerLensCapture()
    {
        if (_universalOcrModule?.Engine == null) return;

        var overlay = new ScreenSnipOverlayWindow();
        overlay.RegionCapturedWithBounds += (bytes, bounds) =>
        {
            Dispatcher.Invoke(() =>
            {
                var lens = new ScreenLensWindow(
                    _universalOcrModule.Engine,
                    bytes,
                    bounds,
                    onOpenInFullApp: () => Dispatcher.Invoke(() =>
                    {
                        Show();
                        WindowState = WindowState.Normal;
                        Activate();
                        NavigateToTag("UniversalOcr");
                        _universalOcrView?.ProcessBytesAsync(bytes);
                    }),
                    onRetake: () => Dispatcher.Invoke(TriggerLensCapture)
                );
                lens.Show();
            });
        };
        overlay.Show();
    }

    private void UpdateStatusBar()
    {
        var diag = _hardwareService.GetDiagnostics();
        StatusBackend.Text = $"AI Engine: {diag.ActiveAiBackend}";

        if (_fileOrganizerModule?.Engine != null)
        {
            var folder = System.IO.Path.GetFileName(_fileOrganizerModule.Engine.WatchedFolder);
            StatusMonitoring.Text = _fileOrganizerModule.Engine.IsMonitoring
                ? LocalizationManager.Instance.GetString("StatusMonitoringActive", folder)
                : LocalizationManager.Get("StatusMonitoringPaused");
        }

        StatusPower.Text = diag.IsOnBattery
            ? LocalizationManager.Instance.GetString("PowerBattery", diag.BatteryPercent)
            : LocalizationManager.Get("PowerAc");
    }

    private void RootNavItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is NavigationViewItem item)
        {
            NavigateToTag(item.Tag?.ToString());
        }
    }

    private void RootNavigation_SelectionChanged(NavigationView sender, RoutedEventArgs args)
    {
        if (sender.SelectedItem is NavigationViewItem item)
        {
            NavigateToTag(item.Tag?.ToString());
        }
        else if (args.Source is NavigationViewItem sourceItem)
        {
            NavigateToTag(sourceItem.Tag?.ToString());
        }
    }

    private void NavigateToTag(string? tag)
    {
        if (string.IsNullOrEmpty(tag)) return;

        foreach (var menuItem in RootNavigation.MenuItems)
        {
            if (menuItem is NavigationViewItem ni) ni.IsActive = (ni.Tag?.ToString() == tag);
        }
        foreach (var menuItem in RootNavigation.FooterMenuItems)
        {
            if (menuItem is NavigationViewItem ni) ni.IsActive = (ni.Tag?.ToString() == tag);
        }

        FrameworkElement? targetView = tag switch
        {
            "FileOrganizer" => _fileOrganizerView,
            "ContextAssistant" => _contextAssistantView,
            "SmartDataExtractor" => _smartDataExtractorView,
            "UniversalOcr" => _universalOcrView,
            "Hardware" => _hardwareView,
            "Settings" => _globalSettingsView,
            _ => null
        };

        if (targetView != null)
        {
            if (tag == "Hardware" && _hardwareView != null)
            {
                _hardwareView.RefreshDiagnostics();
            }
            RootFrame.Navigate(targetView);
            NPUtil.App.Common.FluentAnimationHelper.AnimateEntrance(targetView);
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

        try
        {
            var helper = new WindowInteropHelper(this);
            UnregisterHotKey(helper.Handle, HOTKEY_ID_ASSISTANT);
            UnregisterHotKey(helper.Handle, HOTKEY_ID_OCR);
        }
        catch { /* ignore */ }

        _trayHelper?.Dispose();
        _fileOrganizerModule?.ShutdownAsync().Wait(500);
        _contextAssistantModule?.ShutdownAsync().Wait(500);
        _smartDataExtractorModule?.ShutdownAsync().Wait(500);
        _universalOcrModule?.ShutdownAsync().Wait(500);
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
