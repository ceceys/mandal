using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static Mandal.Services.NativeMethods;

namespace Mandal.Services;

public static class ScreenCapture
{
    public static Rectangle VirtualScreen => SystemInformation.VirtualScreen;

    /// <summary>Fizik piksel koordinatlı bir alanı yakalar. 24 bit: alfa kanalı sürprizi olmaz.</summary>
    public static Bitmap Capture(Rectangle r)
    {
        if (r.Width <= 0 || r.Height <= 0) throw new ArgumentException("Boş alan", nameof(r));
        var bmp = new Bitmap(r.Width, r.Height, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(r.Left, r.Top, 0, 0, r.Size, CopyPixelOperation.SourceCopy);
        return bmp;
    }

    public static Bitmap CaptureVirtualScreen() => Capture(VirtualScreen);

    public static Rectangle ScreenAtCursor() => Screen.FromPoint(Cursor.Position).Bounds;

    /// <summary>Aktif pencerenin görünür çerçevesi (DWM gölgesi hariç).</summary>
    public static Rectangle? ForegroundWindowBounds()
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;

        Rectangle r = Rectangle.Empty;
        if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT dwm, Marshal.SizeOf<RECT>()) == 0)
            r = dwm.ToRectangle();

        if (r.Width <= 0 || r.Height <= 0)
        {
            if (!GetWindowRect(hwnd, out RECT wr)) return null;
            r = wr.ToRectangle();
        }

        r.Intersect(VirtualScreen);
        return r.Width > 0 && r.Height > 0 ? r : null;
    }

    public static Bitmap Crop(Bitmap source, Rectangle r)
    {
        r.Intersect(new Rectangle(0, 0, source.Width, source.Height));
        if (r.Width <= 0 || r.Height <= 0) throw new ArgumentException("Boş alan", nameof(r));
        var bmp = new Bitmap(r.Width, r.Height, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bmp);
        g.DrawImage(source, new Rectangle(0, 0, r.Width, r.Height), r, GraphicsUnit.Pixel);
        return bmp;
    }
}
