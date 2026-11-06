using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using NPUtil.Core.Logging;

namespace NPUtil.Core.Context;

public record CapturedContext
{
    public string? SelectedText { get; init; }
    public string? ActiveProcessName { get; init; }
    public string? ActiveWindowTitle { get; init; }
    public IReadOnlyList<string> AttachedFiles { get; init; } = [];
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public bool HasContext => !string.IsNullOrWhiteSpace(SelectedText) || AttachedFiles.Count > 0;
}

public interface IContextCaptureService
{
    bool AllowActiveAppDetection { get; set; }
    Task<CapturedContext> CaptureCurrentContextAsync(CancellationToken ct = default);
    (string? ProcessName, string? WindowTitle) GetForegroundApplicationInfo();
    string? GetCurrentClipboardText();
}

public class ContextCaptureService : IContextCaptureService
{
    private readonly ILogger? _logger;

    public bool AllowActiveAppDetection { get; set; } = true;

    public ContextCaptureService(ILogger? logger = null)
    {
        _logger = logger;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("user32.dll")]
    private static extern bool IsClipboardFormatAvailable(uint format);

    private const uint CF_UNICODETEXT = 13;

    public (string? ProcessName, string? WindowTitle) GetForegroundApplicationInfo()
    {
        if (!AllowActiveAppDetection) return (null, null);

        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return (null, null);

            var sb = new StringBuilder(512);
            GetWindowText(hwnd, sb, sb.Capacity);
            var title = sb.ToString();

            GetWindowThreadProcessId(hwnd, out var pid);
            string? procName = null;
            if (pid > 0)
            {
                using var proc = Process.GetProcessById((int)pid);
                procName = proc.ProcessName;
            }

            return (procName, title);
        }
        catch (Exception ex)
        {
            _logger?.Warning("ContextCapture", $"Could not inspect active window: {ex.Message}", ex);
            return (null, null);
        }
    }

    public string? GetCurrentClipboardText()
    {
        try
        {
            if (!IsClipboardFormatAvailable(CF_UNICODETEXT)) return null;
            if (!OpenClipboard(IntPtr.Zero)) return null;

            try
            {
                var handle = GetClipboardData(CF_UNICODETEXT);
                if (handle == IntPtr.Zero) return null;
                return Marshal.PtrToStringUni(handle);
            }
            finally
            {
                CloseClipboard();
            }
        }
        catch (Exception ex)
        {
            _logger?.Warning("ContextCapture", $"Failed reading Win32 clipboard: {ex.Message}", ex);
            return null;
        }
    }

    public Task<CapturedContext> CaptureCurrentContextAsync(CancellationToken ct = default)
    {
        var (procName, title) = GetForegroundApplicationInfo();
        var capturedText = GetCurrentClipboardText();

        var result = new CapturedContext
        {
            SelectedText = capturedText,
            ActiveProcessName = procName,
            ActiveWindowTitle = title
        };

        return Task.FromResult(result);
    }
}
