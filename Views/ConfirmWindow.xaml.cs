using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Mandal.Views;

/// <summary>
/// Programın kendi dilinde Evet/Hayır sorusu: mandaldan sarkan bir kart.
/// Yukarıdan düşer, hafif sallanır. Enter ve Esc = Hayır (güvenli varsayılan).
/// Yükseklik içeriğe göre; ayrıntı metni (sürüm notları) kaydırılır ve kart her ekranda çalışma alanına sığar,
/// böylece düğmeler hiçbir çözünürlükte, ölçekte ya da not uzunluğunda kesilmez.
/// </summary>
public partial class ConfirmWindow : Window
{
    /// <summary>İpin hemen altı: ekran yetiyorsa kart buradan sarkar.</summary>
    private const double PreferredTop = 196;

    /// <summary>Kartın ayrıntı alanı dışındaki kısmı için ayrılan pay (mandal, ileti, düğmeler, gölge).</summary>
    private const double ChromeReserve = 300;

    public bool Result { get; private set; }

    public ConfirmWindow(string message, bool dangerous, string? details = null)
    {
        InitializeComponent();
        MessageText.Text = message;
        if (!string.IsNullOrWhiteSpace(details))
        {
            DetailsText.Text = details.Trim();
            DetailsBox.Visibility = Visibility.Visible;
        }
        if (!dangerous)
        {
            YesButton.Background = (Brush)FindResource("AccentBrush");
            YesButton.BorderBrush = YesButton.Background;
        }

        PlaceOnScreen();

        Loaded += (_, _) =>
        {
            Activate();
            NoButton.Focus();
            PlayDrop();
        };
    }

    /// <summary>
    /// Kartı ölçer ve birincil ekranın çalışma alanına (görev çubuğu hariç) sığdırır: ayrıntı alanı en fazla
    /// kalan yüksekliği alır, kart ipin altına sığmıyorsa yukarı kayar. Göstermeden önce yapılır, kart zıplamaz.
    /// </summary>
    private void PlaceOnScreen()
    {
        var wa = SystemParameters.WorkArea;
        DetailsScroll.MaxHeight = Math.Clamp(wa.Height - ChromeReserve, 72, 260);
        MaxHeight = wa.Height;

        Card.Measure(new Size(Width, double.PositiveInfinity));
        double height = Math.Min(Card.DesiredSize.Height, wa.Height);

        Left = wa.Left + Math.Max(0, (wa.Width - Width) / 2);
        Top = wa.Top + Math.Clamp(wa.Height - height, 0, PreferredTop);
    }

    private void PlayDrop()
    {
        var drop = new DoubleAnimation(0, new Duration(TimeSpan.FromMilliseconds(380)))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 },
        };
        Drop.BeginAnimation(TranslateTransform.YProperty, drop);

        var swing = new DoubleAnimationUsingKeyFrames();
        swing.KeyFrames.Add(new EasingDoubleKeyFrame(2.5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220))));
        swing.KeyFrames.Add(new EasingDoubleKeyFrame(-2.2, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(480))));
        swing.KeyFrames.Add(new EasingDoubleKeyFrame(0.6, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(700))));
        swing.KeyFrames.Add(new EasingDoubleKeyFrame(-1.5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(900)))
            { EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut } });
        Swing.BeginAnimation(RotateTransform.AngleProperty, swing);
    }

    /// <param name="details">İletinin altında, kaydırılabilir alanda gösterilecek uzun metin (isteğe bağlı).</param>
    public static bool Ask(string message, bool dangerous = true, string? details = null)
    {
        var w = new ConfirmWindow(message, dangerous, details);
        w.ShowDialog();
        return w.Result;
    }

    private void Finish(bool result)
    {
        Result = result;
        // Yukarı kaçarak kapanır
        var up = new DoubleAnimation(-60, new Duration(TimeSpan.FromMilliseconds(180)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
        };
        up.Completed += (_, _) => Close();
        Drop.BeginAnimation(TranslateTransform.YProperty, up);
        Card.BeginAnimation(OpacityProperty, new DoubleAnimation(0, new Duration(TimeSpan.FromMilliseconds(180))));
    }

    private void Yes_Click(object sender, RoutedEventArgs e) => Finish(true);

    private void No_Click(object sender, RoutedEventArgs e) => Finish(false);

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Escape or Key.Return && !YesButton.IsKeyboardFocused) { Finish(false); e.Handled = true; }
        else if (e.Key == Key.Return && YesButton.IsKeyboardFocused) { Finish(true); e.Handled = true; }
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is System.Windows.Controls.Button) return;
        // Notların kaydırma çubuğundan tutulunca pencere sürüklenmesin
        for (var d = e.OriginalSource as DependencyObject; d is not null;
             d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            if (d is System.Windows.Controls.Primitives.ScrollBar) return;
        DragMove();
    }
}
