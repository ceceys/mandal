using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Mandal.Views;

/// <summary>
/// Programın kendi dilinde Evet/Hayır sorusu: mandaldan sarkan bir kart.
/// Yukarıdan düşer, hafif sallanır. Enter ve Esc = Hayır (güvenli varsayılan).
/// </summary>
public partial class ConfirmWindow : Window
{
    public bool Result { get; private set; }

    public ConfirmWindow(string message, bool dangerous)
    {
        InitializeComponent();
        MessageText.Text = message;
        if (!dangerous)
        {
            YesButton.Background = (Brush)FindResource("AccentBrush");
            YesButton.BorderBrush = YesButton.Background;
        }

        // İpin hemen altında, ortada
        Left = Math.Max(0, (SystemParameters.PrimaryScreenWidth - Width) / 2);
        Top = 196;

        Loaded += (_, _) =>
        {
            Activate();
            NoButton.Focus();
            PlayDrop();
        };
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

    public static bool Ask(string message, bool dangerous = true)
    {
        var w = new ConfirmWindow(message, dangerous);
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
        if (e.OriginalSource is not System.Windows.Controls.Button) DragMove();
    }
}
