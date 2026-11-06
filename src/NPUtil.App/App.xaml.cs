using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace NPUtil.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogCrash("DispatcherUnhandledException", e.Exception);
        MessageBox.Show(
            $"V aplikaci NPUtil došlo k chybě:\n\n{e.Exception.Message}\n\nPodrobnosti naleznete v logu:\n{GetCrashLogPath()}",
            "NPUtil - Chyba aplikace",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = false;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogCrash("AppDomainUnhandledException", ex);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogCrash("UnobservedTaskException", e.Exception);
        e.SetObserved();
    }

    private static string GetCrashLogPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var logDir = Path.Combine(localAppData, "NPUtil", "Logs");
        Directory.CreateDirectory(logDir);
        return Path.Combine(logDir, "crash.log");
    }

    private static void LogCrash(string type, Exception ex)
    {
        try
        {
            var path = GetCrashLogPath();
            var text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{type}] {ex}\n\n";
            File.AppendAllText(path, text);
        }
        catch
        {
            // Ignore failure writing crash log
        }
    }
}

