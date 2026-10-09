using System.Windows;
using System.Windows.Interop;
using Mandal.Models;
using Mandal.Services;

namespace Mandal.Views;

/// <summary>
/// Sürükleme sırasında imlecin yanında giden küçük kart. Fareye karşı tamamen geçirgen
/// (WS_EX_TRANSPARENT) olduğundan altındaki bırakma hedefini engellemez.
/// </summary>
public partial class DragGhostWindow : Window
{
    public DragGhostWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.MakeNoActivateToolWindow(hwnd);
            NativeMethods.MakeClickThrough(hwnd);
        };
    }

    public void ShowFor(ClipItem item)
    {
        if (item.IsImage)
        {
            Img.Source = item.Thumb;
            Img.Visibility = Visibility.Visible;
            Paper.Visibility = Visibility.Collapsed;
        }
        else
        {
            Img.Source = null;
            Img.Visibility = Visibility.Collapsed;
            Txt.Text = item.Preview;
            Paper.Visibility = Visibility.Visible;
        }
        Follow();
        if (!IsVisible) Show();
    }

    /// <summary>İmlecin sağ altına yerleşir (DIP).</summary>
    public void Follow()
    {
        if (!NativeMethods.GetCursorPos(out var p)) return;
        double scale = 1;
        if (PresentationSource.FromVisual(this)?.CompositionTarget is { } ct) scale = ct.TransformToDevice.M11;
        Left = p.X / scale + 14;
        Top = p.Y / scale + 10;
    }
}
