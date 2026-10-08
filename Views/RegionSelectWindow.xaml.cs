using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Mandal.Services;
using Drawing = System.Drawing;

namespace Mandal.Views;

public partial class RegionSelectWindow : Window
{
    private readonly Drawing.Rectangle _virtualScreen;
    private Point? _start;
    private double _sx = 1, _sy = 1;

    /// <summary>Seçilen alan, fizik piksel ve ekran koordinatlarında.</summary>
    public Drawing.Rectangle? Result { get; private set; }

    public static Drawing.Rectangle? Select(Drawing.Bitmap frozenScreen, Drawing.Rectangle virtualScreen)
    {
        var w = new RegionSelectWindow(frozenScreen, virtualScreen);
        w.ShowDialog();
        return w.Result;
    }

    private RegionSelectWindow(Drawing.Bitmap frozenScreen, Drawing.Rectangle virtualScreen)
    {
        InitializeComponent();
        _virtualScreen = virtualScreen;
        Screenshot.Source = ToBitmapSource(frozenScreen);

        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        SourceInitialized += OnSourceInitialized;
        Loaded += (_, _) =>
        {
            Activate();
            Focus();
            UpdateFull();
        };
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (HwndSource.FromHwnd(hwnd)?.CompositionTarget is { } ct)
        {
            _sx = ct.TransformToDevice.M11;
            _sy = ct.TransformToDevice.M22;
        }
        // Fizik piksel olarak tam sanal ekranı kapla.
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST,
            _virtualScreen.X, _virtualScreen.Y, _virtualScreen.Width, _virtualScreen.Height, 0);
    }

    private static BitmapSource ToBitmapSource(Drawing.Bitmap bmp)
    {
        IntPtr h = bmp.GetHbitmap();
        try
        {
            var bs = Imaging.CreateBitmapSourceFromHBitmap(h, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bs.Freeze();
            return bs;
        }
        finally
        {
            NativeMethods.DeleteObject(h);
        }
    }

    private void UpdateFull() => FullGeom.Rect = new Rect(0, 0, ActualWidth, ActualHeight);

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateFull();

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Cancel();
    }

    private void Window_RightDown(object sender, MouseButtonEventArgs e) => Cancel();

    private void Cancel()
    {
        Result = null;
        Close();
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(Root);
        Hint.Visibility = Visibility.Collapsed;
        CaptureMouse();
        UpdateSelection(_start.Value);
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (_start is null) return;
        UpdateSelection(e.GetPosition(Root));
    }

    private void Window_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_start is null) return;
        var rect = RectFrom(_start.Value, e.GetPosition(Root));
        ReleaseMouseCapture();
        _start = null;

        var phys = ToPhysical(rect);
        if (phys.Width >= 2 && phys.Height >= 2)
        {
            Result = phys;
            Close();
            return;
        }

        // Çok küçük: seçimi sıfırla, yeniden dene
        SelGeom.Rect = new Rect(0, 0, 0, 0);
        SelBorder.Visibility = Visibility.Collapsed;
        SizeLabel.Visibility = Visibility.Collapsed;
        Hint.Visibility = Visibility.Visible;
    }

    private void UpdateSelection(Point current)
    {
        if (_start is null) return;
        var r = RectFrom(_start.Value, current);

        SelGeom.Rect = r;

        Canvas.SetLeft(SelBorder, r.X);
        Canvas.SetTop(SelBorder, r.Y);
        SelBorder.Width = r.Width;
        SelBorder.Height = r.Height;
        SelBorder.Visibility = Visibility.Visible;

        var phys = ToPhysical(r);
        SizeText.Text = $"{phys.Width} × {phys.Height}";
        SizeLabel.Visibility = Visibility.Visible;
        SizeLabel.UpdateLayout();

        double lx = r.Right - SizeLabel.ActualWidth;
        double ly = r.Bottom + 6;
        if (ly + SizeLabel.ActualHeight > ActualHeight) ly = r.Top - SizeLabel.ActualHeight - 6;
        if (ly < 0) ly = r.Top + 6;
        if (lx < 0) lx = r.X;
        Canvas.SetLeft(SizeLabel, lx);
        Canvas.SetTop(SizeLabel, ly);
    }

    private static Rect RectFrom(Point a, Point b)
        => new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    private Drawing.Rectangle ToPhysical(Rect r)
    {
        int x = (int)Math.Round(r.X * _sx);
        int y = (int)Math.Round(r.Y * _sy);
        int right = (int)Math.Round(r.Right * _sx);
        int bottom = (int)Math.Round(r.Bottom * _sy);
        return new Drawing.Rectangle(_virtualScreen.X + x, _virtualScreen.Y + y, right - x, bottom - y);
    }
}
