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
            Hide();

            var cur = e.GetPosition(this);
            var x = (int)Math.Min(_startPoint.X, cur.X);
            var y = (int)Math.Min(_startPoint.Y, cur.Y);
            var w = (int)Math.Abs(cur.X - _startPoint.X);
            var h = (int)Math.Abs(cur.Y - _startPoint.Y);

            if (w > 10 && h > 10)
            {
                var bytes = CaptureScreenRegion(x, y, w, h);
                RegionCaptured?.Invoke(bytes);
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
