using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Point = System.Windows.Point;

namespace NPUtil.Modules.UniversalOcr.UI;

public partial class ScreenSnipOverlayWindow : Window
{
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    private Point _startPoint;
    private bool _isSelecting;
    private Bitmap? _freezeFrame;

    public event Action<byte[]>? RegionCaptured;
    public event Action<byte[], System.Drawing.Rectangle>? RegionCapturedWithBounds;

    public ScreenSnipOverlayWindow()
    {
        InitializeComponent();

        CaptureDesktopFreezeFrame();

        WindowState = WindowState.Normal;
        WindowStyle = WindowStyle.None;
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        Closed += (s, e) =>
        {
            _freezeFrame?.Dispose();
            _freezeFrame = null;
        };
    }

    private void CaptureDesktopFreezeFrame()
    {
        try
        {
            int virtX = GetSystemMetrics(SM_XVIRTUALSCREEN);
            int virtY = GetSystemMetrics(SM_YVIRTUALSCREEN);
            int virtW = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            int virtH = GetSystemMetrics(SM_CYVIRTUALSCREEN);

            if (virtW <= 0) virtW = (int)SystemParameters.PrimaryScreenWidth;
            if (virtH <= 0) virtH = (int)SystemParameters.PrimaryScreenHeight;

            _freezeFrame = new Bitmap(virtW, virtH, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(_freezeFrame))
            {
                g.CopyFromScreen(virtX, virtY, 0, 0, new System.Drawing.Size(virtW, virtH), CopyPixelOperation.SourceCopy);
            }

            using var ms = new MemoryStream();
            _freezeFrame.Save(ms, ImageFormat.Png);
            ms.Seek(0, SeekOrigin.Begin);

            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.StreamSource = ms;
            bi.EndInit();
            bi.Freeze();
            ImgFrozenDesktop.Source = bi;
        }
        catch
        {
            // Fallback gracefully if freeze-frame capture encounters an anomaly
        }
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _startPoint = e.GetPosition(this);
            _isSelecting = true;
            RectSelection.Visibility = Visibility.Visible;
            Canvas.SetLeft(RectSelection, _startPoint.X);
            Canvas.SetTop(RectSelection, _startPoint.Y);
            RectSelection.Width = 0;
            RectSelection.Height = 0;
            CaptureMouse();
        }
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isSelecting)
        {
            var cur = e.GetPosition(this);
            var x = Math.Min(_startPoint.X, cur.X);
            var y = Math.Min(_startPoint.Y, cur.Y);
            var w = Math.Abs(cur.X - _startPoint.X);
            var h = Math.Abs(cur.Y - _startPoint.Y);

            Canvas.SetLeft(RectSelection, x);
            Canvas.SetTop(RectSelection, y);
            RectSelection.Width = w;
            RectSelection.Height = h;
        }
    }

    private void Window_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isSelecting)
        {
            _isSelecting = false;
            ReleaseMouseCapture();

            var cur = e.GetPosition(this);

            double relX = Math.Min(_startPoint.X, cur.X);
            double relY = Math.Min(_startPoint.Y, cur.Y);
            double relW = Math.Abs(cur.X - _startPoint.X);
            double relH = Math.Abs(cur.Y - _startPoint.Y);

            Hide();

            if (relW > 8 && relH > 8)
            {
                byte[]? capturedBytes = null;
                var bounds = new System.Drawing.Rectangle((int)relX, (int)relY, (int)relW, (int)relH);

                if (_freezeFrame != null)
                {
                    double scaleX = (double)_freezeFrame.Width / Math.Max(1, ActualWidth);
                    double scaleY = (double)_freezeFrame.Height / Math.Max(1, ActualHeight);

                    int physX = (int)(relX * scaleX);
                    int physY = (int)(relY * scaleY);
                    int physW = (int)(relW * scaleX);
                    int physH = (int)(relH * scaleY);

                    // Add generous 6px safety padding around selection so characters on the border are never clipped
                    int pad = 6;
                    int finalX = Math.Max(0, physX - pad);
                    int finalY = Math.Max(0, physY - pad);
                    int finalW = Math.Min(_freezeFrame.Width - finalX, physW + (physX - finalX) + pad);
                    int finalH = Math.Min(_freezeFrame.Height - finalY, physH + (physY - finalY) + pad);

                    if (finalW > 4 && finalH > 4)
                    {
                        using var cropped = _freezeFrame.Clone(
                            new System.Drawing.Rectangle(finalX, finalY, finalW, finalH),
                            _freezeFrame.PixelFormat);
                        using var ms = new MemoryStream();
                        cropped.Save(ms, ImageFormat.Png);
                        capturedBytes = ms.ToArray();
                    }
                }

                if (capturedBytes != null)
                {
                    RegionCaptured?.Invoke(capturedBytes);
                    RegionCapturedWithBounds?.Invoke(capturedBytes, bounds);
                }
            }

            Close();
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }
}
