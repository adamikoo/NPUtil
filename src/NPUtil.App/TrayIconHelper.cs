using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using NPUtil.Core.Logging;

namespace NPUtil.App;

public class TrayIconHelper : IDisposable
{
    private const int WM_TRAYICON = 0x8000 + 101;
    private const int NIM_ADD = 0x00000000;
    private const int NIM_MODIFY = 0x00000001;
    private const int NIM_DELETE = 0x00000002;
    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_TIP = 0x00000004;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONUP = 0x0205;

    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x00000010;
    private const uint LR_DEFAULTSIZE = 0x00000040;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private readonly Window _window;
    private readonly ILogger _logger;
    private IntPtr _hwnd;
    private IntPtr _hIcon = IntPtr.Zero;
    private HwndSource? _hwndSource;
    private bool _isCreated;

    public event Action? TrayDoubleClicked;
    public event Action? TrayRightClicked;

    public TrayIconHelper(Window window, ILogger logger)
    {
        _window = window;
        _logger = logger;
    }

    public void Initialize()
    {
        var helper = new WindowInteropHelper(_window);
        _hwnd = helper.Handle;
        if (_hwnd == IntPtr.Zero) return;

        _hwndSource = HwndSource.FromHwnd(_hwnd);
        _hwndSource?.AddHook(WndProc);

        CreateNotifyIcon("NPUtil — File Organizer (Active)");
    }

    private void CreateNotifyIcon(string tooltip)
    {
        var data = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1001,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            szTip = tooltip
        };

        try
        {
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "nputil.ico");
            if (System.IO.File.Exists(iconPath))
            {
                _hIcon = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);
                data.hIcon = _hIcon;
            }
        }
        catch { /* fallback icon */ }

        _isCreated = Shell_NotifyIcon(NIM_ADD, ref data);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_TRAYICON)
        {
            var eventType = lParam.ToInt32();
            if (eventType is WM_LBUTTONDBLCLK or WM_LBUTTONUP)
            {
                TrayDoubleClicked?.Invoke();
                handled = true;
            }
            else if (eventType == WM_RBUTTONUP)
            {
                TrayRightClicked?.Invoke();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void UpdateTooltip(string tooltip)
    {
        if (!_isCreated) return;
        var data = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1001,
            uFlags = NIF_TIP,
            szTip = tooltip.Length > 120 ? tooltip[..120] : tooltip
        };
        Shell_NotifyIcon(NIM_MODIFY, ref data);
    }

    public void Dispose()
    {
        if (_isCreated && _hwnd != IntPtr.Zero)
        {
            var data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = 1001
            };
            Shell_NotifyIcon(NIM_DELETE, ref data);
            _isCreated = false;
        }

        if (_hIcon != IntPtr.Zero)
        {
            DestroyIcon(_hIcon);
            _hIcon = IntPtr.Zero;
        }

        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }
    }
}
