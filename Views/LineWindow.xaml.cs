using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Mandal.Services;

namespace Mandal.Views;

public partial class LineWindow : Window
{
    private const double RopeY = 28;
    private static readonly Duration SlideDuration = new(TimeSpan.FromMilliseconds(220));

    private readonly ClipStore _store;
    private readonly DispatcherTimer _autoHide = new();
    private bool _shown;
    private bool _temporary;

    public bool IsShown => _shown;

    /// <summary>Sürükleme sürerken otomatik gizleme ertelenir.</summary>
    public bool IsDragging { get; set; }

    public LineWindow(ClipStore store)
    {
        InitializeComponent();
        _store = store;
        DataContext = store;

        _autoHide.Tick += (_, _) =>
        {
            if (IsDragging || IsMouseOver) return; // bir sonraki tikte yeniden bak
            _autoHide.Stop();
            HideAnimated();
        };

        store.Items.CollectionChanged += (_, _) => UpdateEmptyHint();
        UpdateEmptyHint();

        SourceInitialized += (_, _) =>
            NativeMethods.MakeNoActivateToolWindow(new WindowInteropHelper(this).Handle);
    }

    private void Place()
    {
        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
    }

    /// <summary>Kullanıcı istedi: ip iner ve kendiliğinden kalkmaz.</summary>
    public void ShowAnimated()
    {
        _temporary = false;
        _autoHide.Stop();
        Reveal();
    }

    /// <summary>Yeni alıntıda kısa görünüm; kullanıcı zaten açmışsa dokunma.</summary>
    public void ShowTemporarily(TimeSpan duration)
    {
        if (_shown && !_temporary) return;
        _temporary = true;
        Reveal();
        _autoHide.Stop();
        _autoHide.Interval = duration;
        _autoHide.Start();
    }

    private void Reveal()
    {
        Place();
        if (!IsVisible)
        {
            Slide.BeginAnimation(TranslateTransform.YProperty, null);
            Slide.Y = -Height;
            Show();
        }
        _shown = true;
        Scroller.ScrollToHorizontalOffset(0);
        Animate(0, null);
    }

    public void HideAnimated(TimeSpan? delay = null)
    {
        _autoHide.Stop();
        _temporary = false;
        if (!_shown) return;
        _shown = false;

        if (delay is { } d && d > TimeSpan.Zero)
        {
            var t = new DispatcherTimer { Interval = d };
            t.Tick += (_, _) =>
            {
                t.Stop();
                if (!_shown) Animate(-Height, () => { if (!_shown) Hide(); });
            };
            t.Start();
        }
        else
        {
            Animate(-Height, () => { if (!_shown) Hide(); });
        }
    }

    public void HideImmediate()
    {
        _autoHide.Stop();
        _temporary = false;
        _shown = false;
        Slide.BeginAnimation(TranslateTransform.YProperty, null);
        Slide.Y = -Height;
        Hide();
    }

    public void Toggle()
    {
        if (_shown) HideAnimated();
        else ShowAnimated();
    }

    private void Animate(double to, Action? completed)
    {
        var anim = new DoubleAnimation(to, SlideDuration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        if (completed is not null) anim.Completed += (_, _) => completed();
        Slide.BeginAnimation(TranslateTransform.YProperty, anim);
    }

    private void UpdateRope()
    {
        double w = ActualWidth;
        if (w <= 30) return;
        var data = Geometry.Parse(FormattableString.Invariant(
            $"M 11,{RopeY} Q {w / 2:F1},{RopeY + 10} {w - 11:F1},{RopeY}"));
        Rope.Data = data;
        RopeHighlight.Data = data;
    }

    private void UpdateEmptyHint()
        => EmptyHint.Visibility = _store.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateRope();

    private void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        if (_temporary) _autoHide.Stop();
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_temporary && _shown && !IsDragging) _autoHide.Start();
    }

    private void Scroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        Scroller.ScrollToHorizontalOffset(Scroller.HorizontalOffset - e.Delta);
        e.Handled = true;
    }

    private void Capture_Click(object sender, RoutedEventArgs e) => App.Current.CaptureRegion();

    private void Hide_Click(object sender, RoutedEventArgs e) => HideAnimated();
}
