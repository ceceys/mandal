using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Mandal.Models;
using Mandal.Services;

namespace Mandal.Views;

public partial class LineWindow : Window
{
    private const double RopeY = 28;
    private static readonly Duration SlideDuration = new(TimeSpan.FromMilliseconds(220));

    /// <summary>Yatay kaydırma animasyonu için yardımcı özellik.</summary>
    private static readonly DependencyProperty ScrollOffsetProperty = DependencyProperty.Register(
        nameof(ScrollOffset), typeof(double), typeof(LineWindow),
        new PropertyMetadata(0.0, (d, e) => ((LineWindow)d).Scroller.ScrollToHorizontalOffset((double)e.NewValue)));

    private double ScrollOffset
    {
        get => (double)GetValue(ScrollOffsetProperty);
        set => SetValue(ScrollOffsetProperty, value);
    }

    private readonly ClipStore _store;
    private readonly DispatcherTimer _autoHide = new();
    private readonly DispatcherTimer _leaveTimer = new() { Interval = TimeSpan.FromMilliseconds(420) };

    /// <summary>Açık sağ tık menüsü / iletişim kutusu sayısı; sıfırdan büyükken fare çıkışı ipi kaldırmaz.</summary>
    public int PopupDepth { get; set; }
    private bool _shown;
    private bool _temporary;

    public bool IsShown => _shown;

    /// <summary>İp indi (true) / kalktı (false).</summary>
    public event Action<bool>? ShownChanged;

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

        _leaveTimer.Tick += (_, _) =>
        {
            _leaveTimer.Stop();
            if (_shown && !IsMouseOver && !IsDragging && PopupDepth == 0) HideAnimated();
        };

        store.Items.CollectionChanged += (_, _) =>
        {
            if (_reorder is not null) EndReorder(commit: false); // yeni kart geldi: yerler değişir, sıralama baştan
            UpdateEmptyCard();
            Relayout();
        };

        ItemsHost.MouseMove += (_, e) => { if (_reorder is not null) ReorderMove(e); };
        ItemsHost.MouseLeftButtonUp += (_, e) =>
        {
            if (_reorder is null) return;
            e.Handled = true;
            EndReorder(commit: true);
        };
        ItemsHost.LostMouseCapture += (_, _) => { if (_reorder is not null) EndReorder(commit: false); };
        _reorderWatch.Tick += (_, _) =>
        {
            if (_reorder is null) { _reorderWatch.Stop(); return; }
            // İp odak almaz, Esc'yi klavye olayı olarak göremeyiz; tuşun durumuna bakılır
            if ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_ESCAPE) & 0x8000) != 0) EndReorder(commit: false);
            else if (Mouse.LeftButton != MouseButtonState.Pressed) EndReorder(commit: true);
        };
        Scroller.SizeChanged += (_, _) => Relayout();
        UpdateEmptyCard();
        RefreshTexts();
        Loc.Current.LanguageChanged += RefreshTexts;

        SourceInitialized += (_, _) =>
            NativeMethods.MakeNoActivateToolWindow(new WindowInteropHelper(this).Handle);
    }

    /// <summary>Yerinde not düzenleme için pencereyi geçici olarak odak alabilir yapar (ve odaklar) ya da geri çevirir.</summary>
    public void AllowActivate(bool on)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        NativeMethods.SetNoActivate(hwnd, !on);
        if (on) Activate();
    }

    /// <summary>Dil veya kısayol değişince metinleri yeniler.</summary>
    public void RefreshTexts()
    {
        var hotkey = App.Current.Settings.HotkeyRegion;
        if (string.IsNullOrWhiteSpace(hotkey)) hotkey = App.Current.Settings.HotkeyRegionAlt;
        EmptyText.Text = Loc.F("Line_EmptyCard", hotkey);
        Relayout();
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
        bool was = _shown;
        _shown = true;
        Scroller.ScrollToHorizontalOffset(0);
        Animate(0, null);
        if (!was) ShownChanged?.Invoke(true);
    }

    public void HideAnimated(TimeSpan? delay = null)
    {
        _autoHide.Stop();
        _leaveTimer.Stop();
        _temporary = false;
        if (!_shown) return;
        _shown = false;
        ShownChanged?.Invoke(false);

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
        bool was = _shown;
        _shown = false;
        Slide.BeginAnimation(TranslateTransform.YProperty, null);
        Slide.Y = -Height;
        Hide();
        if (was) ShownChanged?.Invoke(false);
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
        RopeShadow.Data = data;
    }

    /// <summary>"Uzayan ip": öğe sayısına ve genişliğe göre kart boyutlarını hesaplar.</summary>
    private void Relayout()
    {
        double available = Scroller.ActualWidth;
        if (available <= 0) return;
        LineLayout.Current.Update(_store.Items.Count, available, App.Current.Settings.ThumbnailHeight);
    }

    private void UpdateEmptyCard()
        => EmptyCard.Visibility = _store.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void UpdateArrows()
    {
        bool overflow = Scroller.ScrollableWidth > 1;
        LeftArrow.Visibility = overflow && Scroller.HorizontalOffset > 1 ? Visibility.Visible : Visibility.Collapsed;
        RightArrow.Visibility = overflow && Scroller.HorizontalOffset < Scroller.ScrollableWidth - 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Slider gibi: bir görünüm genişliği kadar sağa/sola kayar.</summary>
    private void Page(int direction)
    {
        double step = Scroller.ViewportWidth * 0.85;
        double target = Math.Clamp(Scroller.HorizontalOffset + direction * step, 0, Scroller.ScrollableWidth);
        var anim = new DoubleAnimation(Scroller.HorizontalOffset, target, new Duration(TimeSpan.FromMilliseconds(260)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        BeginAnimation(ScrollOffsetProperty, anim);
    }

    // ---------------- Sabit kartları sürükleyerek sıralama ----------------

    private const double ReorderLift = -5;

    /// <summary>Süren sıralama: sabitlerin kapları ve başlangıçtaki yerleri (ItemsHost koordinatında).</summary>
    private sealed class ReorderSession
    {
        public required ClipItem Item { get; init; }
        public required ClipItemView View { get; init; }
        public required FrameworkElement[] Slots { get; init; }
        public required double[] Lefts { get; init; }
        public required double[] Widths { get; init; }
        public required double[] Shifts { get; init; }
        public required int From { get; init; }
        public required double StartX { get; init; }
        public double Dx { get; set; }
        public int Target { get; set; }
    }

    private ReorderSession? _reorder;
    private readonly DispatcherTimer _reorderWatch = new() { Interval = TimeSpan.FromMilliseconds(50) };

    /// <summary>
    /// Sabit kart yana çekildi: kart ip boyunca imleci izler, öteki sabitler yer açar; bırakınca yeni sıra kaydedilir.
    /// İpin altına çekilirse sıralama bırakılır, kart her zamanki gibi başka uygulamaya sürüklenir. Esc iptal eder.
    /// </summary>
    /// <param name="movedX">Sürükleme eşiği aşılana kadar imlecin yatayda aldığı yol</param>
    /// <returns>Sıralama başlamadıysa (tek sabit varsa) false; çağıran dışarı sürüklemeye geçer.</returns>
    public bool BeginReorder(ClipItemView view, ClipItem item, double movedX)
    {
        if (_reorder is not null) return false;
        int count = _store.PinnedCount;
        int from = _store.Items.IndexOf(item);
        if (count < 2 || from < 0 || from >= count) return false;

        var slots = new FrameworkElement[count];
        var lefts = new double[count];
        var widths = new double[count];
        for (int i = 0; i < count; i++)
        {
            if (ItemsHost.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement slot) return false;
            slot.RenderTransform = new TranslateTransform(); // önceki yerleşme animasyonu sürüyorsa sıfırla
            slots[i] = slot;
            lefts[i] = slot.TranslatePoint(new Point(0, 0), ItemsHost).X;
            widths[i] = slot.ActualWidth;
        }

        if (!ItemsHost.CaptureMouse()) return false;
        _reorder = new ReorderSession
        {
            Item = item,
            View = view,
            Slots = slots,
            Lefts = lefts,
            Widths = widths,
            Shifts = new double[count],
            From = from,
            Target = from,
            StartX = Mouse.GetPosition(ItemsHost).X - movedX,
        };
        IsDragging = true;
        Panel.SetZIndex(slots[from], 1);
        AnimateSlot(slots[from], TranslateTransform.YProperty, ReorderLift, 120);
        _reorderWatch.Start();
        ReorderMove(null);
        return true;
    }

    private void ReorderMove(MouseEventArgs? e)
    {
        var r = _reorder!;

        // İpin altına çekildi: sıralama yerine başka uygulamaya sürükle
        if (e is not null && e.GetPosition(this).Y > ActualHeight)
        {
            var view = r.View;
            EndReorder(commit: false);
            view.DragOut();
            return;
        }

        double x = (e?.GetPosition(ItemsHost) ?? Mouse.GetPosition(ItemsHost)).X;
        double minDx = r.Lefts[0] - r.Lefts[r.From];
        double maxDx = r.Lefts[^1] + r.Widths[^1] - r.Widths[r.From] - r.Lefts[r.From];
        r.Dx = Math.Clamp(x - r.StartX, minDx, maxDx);
        ((TranslateTransform)r.Slots[r.From].RenderTransform).X = r.Dx;

        // Sürüklenen kartın ortası komşunun ortasını geçince komşu, kartın genişliği kadar kayıp yer açar
        double center = r.Lefts[r.From] + r.Widths[r.From] / 2 + r.Dx;
        int target = r.From;
        for (int j = 0; j < r.Slots.Length; j++)
        {
            if (j == r.From) continue;
            double neighbor = r.Lefts[j] + r.Widths[j] / 2;
            double shift = 0;
            if (j < r.From && center < neighbor) { shift = r.Widths[r.From]; target--; }
            else if (j > r.From && center > neighbor) { shift = -r.Widths[r.From]; target++; }
            if (shift != r.Shifts[j])
            {
                r.Shifts[j] = shift;
                AnimateSlot(r.Slots[j], TranslateTransform.XProperty, shift, 170);
            }
        }
        r.Target = target;
    }

    private void EndReorder(bool commit)
    {
        var r = _reorder;
        if (r is null) return;
        _reorder = null; // LostMouseCapture yeniden girmesin
        _reorderWatch.Stop();
        if (ItemsHost.IsMouseCaptured) ItemsHost.ReleaseMouseCapture();
        IsDragging = false;

        var dragged = r.Slots[r.From];
        if (!commit || r.Target == r.From)
        {
            // Herkes yerine döner
            foreach (var slot in r.Slots) AnimateSlot(slot, TranslateTransform.XProperty, 0, 200);
            AnimateSlot(dragged, TranslateTransform.YProperty, 0, 200, () => Panel.SetZIndex(dragged, 0));
            return;
        }

        // Yeni sıra: komşular zaten yeni yerlerinde duruyor; kaydırmaları sıfırla, listeyi taşı,
        // sürüklenen kartı bırakıldığı yerden yeni yuvasına kaydır.
        double droppedLeft = r.Lefts[r.From] + r.Dx;
        foreach (var slot in r.Slots) slot.RenderTransform = new TranslateTransform();
        Panel.SetZIndex(dragged, 0);
        App.Current.MovePinned(r.Item, r.Target);
        ItemsHost.UpdateLayout();

        if (ItemsHost.ItemContainerGenerator.ContainerFromItem(r.Item) is FrameworkElement moved)
        {
            double newLeft = moved.TranslatePoint(new Point(0, 0), ItemsHost).X;
            moved.RenderTransform = new TranslateTransform(droppedLeft - newLeft, ReorderLift);
            Panel.SetZIndex(moved, 1);
            AnimateSlot(moved, TranslateTransform.XProperty, 0, 200);
            AnimateSlot(moved, TranslateTransform.YProperty, 0, 200, () => Panel.SetZIndex(moved, 0));
        }
    }

    private static void AnimateSlot(UIElement slot, DependencyProperty property, double to, double ms, Action? completed = null)
    {
        if (slot.RenderTransform is not TranslateTransform t) slot.RenderTransform = t = new TranslateTransform();
        var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        if (completed is not null) anim.Completed += (_, _) => completed();
        t.BeginAnimation(property, anim);
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateRope();

    private void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        _leaveTimer.Stop();
        if (_temporary) _autoHide.Stop();
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_shown || IsDragging) return;
        // Varsayılan: fare bandın dışına çıkınca ip yukarı kaçar
        if (App.Current.Settings.HideOnMouseLeave && PopupDepth == 0) _leaveTimer.Start();
        else if (_temporary) _autoHide.Start();
    }

    private void Scroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        BeginAnimation(ScrollOffsetProperty, null);
        Scroller.ScrollToHorizontalOffset(Scroller.HorizontalOffset - e.Delta);
        e.Handled = true;
    }

    private void Scroller_ScrollChanged(object sender, ScrollChangedEventArgs e) => UpdateArrows();

    private void LeftArrow_Click(object sender, RoutedEventArgs e) => Page(-1);

    private void RightArrow_Click(object sender, RoutedEventArgs e) => Page(+1);
}
