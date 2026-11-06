using System.Diagnostics;
using Microsoft.Win32;
using NPUtil.Core.Logging;

namespace NPUtil.Core.Settings;

public static class StartupHelper
{
    private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "NPUtil";

    public static bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, writable: false);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }

    public static void SetStartup(bool enable, ILogger? logger = null)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, writable: true);
            if (key == null) return;

            if (enable)
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(AppName, $"\"{exePath}\" --background");
                    logger?.Info("StartupHelper", $"Registered {AppName} for Windows startup: {exePath}");
                }
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
                logger?.Info("StartupHelper", $"Unregistered {AppName} from Windows startup.");
            }
        }
        catch (Exception ex)
        {
            logger?.Warning("StartupHelper", "Failed to update Windows startup registry key.", ex);
        }
    }
}
