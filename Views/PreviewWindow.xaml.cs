using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Mandal.Models;
using Mandal.Services;

namespace Mandal.Views;

/// <summary>
/// Büyük önizleme: ekranın üstünde açılır, kenarlardan boyutlandırılır, başlıktan taşınır,
/// tekerlekle yakınlaştırılır, isteğe bağlı olarak diğer pencerelerin üstünde kalır.
/// Boyut, konum ve "üstte kal" ayarı hatırlanır.
/// </summary>
public partial class PreviewWindow : Window
{
    private const double MinScale = 0.1, MaxScale = 8;

    private double _scale = 1;
    private bool _fit = true;
    private bool _panning;
    private Point _panStart;
    private double _panH, _panV;

    public ClipItem? Item { get; private set; }

    public PreviewWindow()
    {
        InitializeComponent();

        var s = App.Current.Settings;
        double screenW = SystemParameters.PrimaryScreenWidth, screenH = SystemParameters.PrimaryScreenHeight;
        Width = Math.Clamp(s.PreviewWidth, MinWidth, screenW);
        Height = Math.Clamp(s.PreviewHeight, MinHeight, screenH);
        if (s.PreviewLeft >= 0 && s.PreviewTop >= 0 && s.PreviewLeft < screenW - 80 && s.PreviewTop < screenH - 80)
        {
            Left = s.PreviewLeft;
            Top = s.PreviewTop;
        }
        else
        {
            // İpin hemen altında, ortada
            Left = Math.Max(0, (screenW - Width) / 2);
            Top = 206 + 6;
        }
        Topmost = s.PreviewTopmost;
        PinToggle.IsChecked = Topmost;

        Scroll.SizeChanged += (_, _) => { if (_fit) Fit(); };
        Closing += (_, _) => SaveGeometry();
    }

    public void ShowItem(ClipItem item)
    {
        Item = item;
        TitleText.Text = item.Summary.Replace("\n", "   ·   ");

        bool img = item.IsImage;
        FitButton.Visibility = img ? Visibility.Visible : Visibility.Collapsed;
        ActualButton.Visibility = img ? Visibility.Visible : Visibility.Collapsed;
        CopySelButton.Visibility = img ? Visibility.Collapsed : Visibility.Visible;
        EditButton.Visibility = img ? Visibility.Collapsed : Visibility.Visible;

        if (img)
        {
            Img.Source = item.LoadFull();
            Scroll.Visibility = Visibility.Visible;
            TextView.Visibility = Visibility.Collapsed;
            _fit = true;
            Dispatcher.BeginInvoke(Fit, System.Windows.Threading.DispatcherPriority.Loaded);
        }
        else
        {
            Img.Source = null;
            TextView.Text = item.Text ?? "";
            Scroll.Visibility = Visibility.Collapsed;
            TextView.Visibility = Visibility.Visible;
            StatusText.Text = Loc.F("Item_TextChars", item.Text?.Length ?? 0) + "   ·   " + Loc.T("Note_SelectHint");
        }

        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    // ---------------- Yakınlaştırma ----------------

    private void Fit()
    {
        if (Img.Source is not BitmapSource bmp) return;
        double availW = Math.Max(1, Scroll.ViewportWidth > 0 ? Scroll.ViewportWidth : Scroll.ActualWidth) - 24;
        double availH = Math.Max(1, Scroll.ViewportHeight > 0 ? Scroll.ViewportHeight : Scroll.ActualHeight) - 24;
        double scale = Math.Min(availW / bmp.PixelWidth, availH / bmp.PixelHeight);
        _fit = true;
        SetScale(scale);
    }

    private void SetScale(double scale)
    {
        _scale = Math.Clamp(scale, MinScale, MaxScale);
        Zoom.ScaleX = _scale;
        Zoom.ScaleY = _scale;
        if (Img.Source is BitmapSource bmp)
            StatusText.Text = $"{_scale * 100:0}%   ·   {bmp.PixelWidth}×{bmp.PixelHeight} px";
    }

    /// <summary>İmlecin altındaki nokta sabit kalacak şekilde yakınlaştırır.</summary>
    private void ZoomAt(double factor, Point? at)
    {
        if (Img.Source is null) return;
        _fit = false;
        double old = _scale;
        SetScale(_scale * factor);
        double ratio = _scale / old;
        Scroll.UpdateLayout();
        var p = at ?? new Point(Scroll.ViewportWidth / 2, Scroll.ViewportHeight / 2);
        Scroll.ScrollToHorizontalOffset((Scroll.HorizontalOffset + p.X) * ratio - p.X);
        Scroll.ScrollToVerticalOffset((Scroll.VerticalOffset + p.Y) * ratio - p.Y);
    }

    private void Scroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ZoomAt(e.Delta > 0 ? 1.15 : 1 / 1.15, e.GetPosition(Scroll));
        e.Handled = true;
    }

    private void Scroll_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_fit) { _fit = false; ZoomAt(1 / _scale, e.GetPosition(Scroll)); } // 1:1
        else Fit();
    }

    // ---------------- Kaydırma (sürükleyerek) ----------------

    private void Scroll_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Scroll.ScrollableWidth <= 0 && Scroll.ScrollableHeight <= 0) return;
        _panning = true;
        _panStart = e.GetPosition(this);
        _panH = Scroll.HorizontalOffset;
        _panV = Scroll.VerticalOffset;
        Scroll.CaptureMouse();
        Scroll.Cursor = Cursors.SizeAll;
    }

    private void Scroll_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_panning) return;
        var d = e.GetPosition(this) - _panStart;
        Scroll.ScrollToHorizontalOffset(_panH - d.X);
        Scroll.ScrollToVerticalOffset(_panV - d.Y);
    }

    private void Scroll_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_panning) return;
        _panning = false;
        Scroll.ReleaseMouseCapture();
        Scroll.Cursor = Cursors.Arrow;
    }

    // ---------------- Düğmeler ve klavye ----------------

    private void Fit_Click(object sender, RoutedEventArgs e) => Fit();

    private void Actual_Click(object sender, RoutedEventArgs e) => ZoomAt(1 / _scale, null);

    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        Topmost = PinToggle.IsChecked == true;
        App.Current.Settings.PreviewTopmost = Topmost;
        App.Current.Settings.Save();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (Item is not null) App.Current.CopyItem(Item, quiet: true);
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (Item is not null) App.Current.OpenItem(Item);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // ---------------- Metin kartı: zahmetsiz kopyalama ----------------

    /// <summary>Fareyle seçip bırakınca seçim panoya gider; Ctrl+C beklemeye gerek yok.</summary>
    private void TextView_MouseUp(object sender, MouseButtonEventArgs e) => CopySelectionIfAny();

    private void TextView_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.LeftShift or Key.RightShift) CopySelectionIfAny();
    }

    private void CopySelection_Click(object sender, RoutedEventArgs e)
    {
        if (!CopySelectionIfAny()) TextView.SelectAll();
    }

    private bool CopySelectionIfAny()
    {
        if (TextView.SelectionLength == 0) return false;
        try
        {
            App.Current.SetClipboardText(TextView.SelectedText);
            ShowSelBadge();
            return true;
        }
        catch (Exception ex)
        {
            Log.Write(ex, "Seçim kopyalama");
            return false;
        }
    }

    private void ShowSelBadge()
    {
        var anim = new DoubleAnimationUsingKeyFrames();
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(80))));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(700))));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1000))));
        SelBadge.BeginAnimation(OpacityProperty, anim);
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (Item is { IsText: true } it) App.Current.OpenNoteEditor(it);
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape: Close(); break;
            case Key.Add or Key.OemPlus: ZoomAt(1.25, null); break;
            case Key.Subtract or Key.OemMinus: ZoomAt(1 / 1.25, null); break;
            case Key.D0 or Key.NumPad0: Fit(); break;
            case Key.D1 or Key.NumPad1: ZoomAt(1 / _scale, null); break;
            default: return;
        }
        e.Handled = true;
    }

    private void SaveGeometry()
    {
        if (WindowState != WindowState.Normal) return;
        var s = App.Current.Settings;
        s.PreviewWidth = Width;
        s.PreviewHeight = Height;
        s.PreviewLeft = Left;
        s.PreviewTop = Top;
        s.Save();
    }
}
