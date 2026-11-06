using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Point = System.Windows.Point;

namespace NPUtil.Modules.UniversalOcr.UI;

public partial class ScreenSnipOverlayWindow : Window
{
    private Point _startPoint;
    private bool _isSelecting;

    public event Action<byte[]>? RegionCaptured;
    public event Action<byte[], System.Drawing.Rectangle>? RegionCapturedWithBounds;

    public ScreenSnipOverlayWindow()
    {
        InitializeComponent();
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

            // Convert WPF DIP coordinates to physical monitor device pixels BEFORE hiding the window
            var p1 = PointToScreen(_startPoint);
            var p2 = PointToScreen(cur);

            int physX = (int)Math.Min(p1.X, p2.X);
            int physY = (int)Math.Min(p1.Y, p2.Y);
            int physW = (int)Math.Abs(p2.X - p1.X);
            int physH = (int)Math.Abs(p2.Y - p1.Y);

            // Calculate DIP bounds for positioning UI windows (e.g. ScreenLensWindow)
            int dipX = (int)Math.Min(_startPoint.X, cur.X);
            int dipY = (int)Math.Min(_startPoint.Y, cur.Y);
            int dipW = (int)Math.Abs(cur.X - _startPoint.X);
            int dipH = (int)Math.Abs(cur.Y - _startPoint.Y);

            Hide();

            if (physW > 10 && physH > 10)
            {
                // Brief pause so Desktop Window Manager redraws without the translucent overlay
                System.Threading.Thread.Sleep(35);

                var bounds = new System.Drawing.Rectangle(dipX, dipY, dipW, dipH);
                var bytes = CaptureScreenRegion(physX, physY, physW, physH);
                RegionCaptured?.Invoke(bytes);
                RegionCapturedWithBounds?.Invoke(bytes, bounds);
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

    private static byte[] CaptureScreenRegion(int x, int y, int width, int height)
    {
        using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height), CopyPixelOperation.SourceCopy);
        }

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
}
