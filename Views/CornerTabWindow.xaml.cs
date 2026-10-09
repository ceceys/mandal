using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Mandal.Services;

namespace Mandal.Views;

/// <summary>
/// Ekranın seçilen köşesinde çapraz asılı mandal: tıklayınca ip açılır/kapanır.
/// Fare yaklaşınca ekranın içine doğru büyür, hafif sallanır; uzaklaşınca geri çekilir.
/// </summary>
public partial class CornerTabWindow : Window
{
    private double _restAngle = -38;
    private double _dirX = 1, _dirY = 1; // "öne" hareket yönü: ekranın içine doğru

    public CornerTabWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
            NativeMethods.MakeNoActivateToolWindow(new WindowInteropHelper(this).Handle);
        ApplyCorner(App.Current.Settings.CornerPosition);
    }

    /// <summary>Pencereyi köşeye taşır, mandalı o köşeden sarkacak biçimde döndürür.</summary>
    public void ApplyCorner(string corner)
    {
        double w = SystemParameters.PrimaryScreenWidth, h = SystemParameters.PrimaryScreenHeight;
        bool right = corner is "TopRight" or "BottomRight";
        bool bottom = corner is "BottomLeft" or "BottomRight";

        Left = right ? w - Width : 0;
        Top = bottom ? h - Height : 0;

        Pin.HorizontalAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        Pin.VerticalAlignment = bottom ? VerticalAlignment.Bottom : VerticalAlignment.Top;
        Pin.Margin = new Thickness(right ? 0 : 18, bottom ? 0 : 2, right ? 18 : 0, bottom ? 2 : 0);
        Pin.RenderTransformOrigin = new Point(0.5, bottom ? 1 : 0);

        // Gövde her zaman ekranın merkezine doğru eğik
        _restAngle = (bottom ? 142 : 38) * (right ? 1 : -1);
        if (bottom) _restAngle = right ? 142 : -142;
        PinRotate.Angle = _restAngle;
        _dirX = right ? -1 : 1;
        _dirY = bottom ? -1 : 1;
        PinMove.X = 0;
        PinMove.Y = 0;
    }

    private void OnClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        App.Current.ToggleLine();
    }

    private void OnHideClick(object sender, RoutedEventArgs e) => App.Current.SetCornerTab(false);

    private void OnEnter(object sender, MouseEventArgs e)
    {
        var pop = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 };
        var dur = new Duration(TimeSpan.FromMilliseconds(260));

        PinScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.35, dur) { EasingFunction = pop });
        PinScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.35, dur) { EasingFunction = pop });
        PinMove.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(8 * _dirX, dur) { EasingFunction = pop });
        PinMove.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8 * _dirY, dur) { EasingFunction = pop });
        Pin.BeginAnimation(OpacityProperty, new DoubleAnimation(1, dur));
        PinShadow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, new DoubleAnimation(14, dur));
        PinShadow.BeginAnimation(DropShadowEffect.ShadowDepthProperty, new DoubleAnimation(6, dur));

        // İpe asılı gibi hafif sallanma
        var swing = new DoubleAnimationUsingKeyFrames();
        swing.KeyFrames.Add(new EasingDoubleKeyFrame(_restAngle + 10, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(140))));
        swing.KeyFrames.Add(new EasingDoubleKeyFrame(_restAngle - 6, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(320))));
        swing.KeyFrames.Add(new EasingDoubleKeyFrame(_restAngle + 3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(480))));
        swing.KeyFrames.Add(new EasingDoubleKeyFrame(_restAngle, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(620)))
            { EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut } });
        PinRotate.BeginAnimation(RotateTransform.AngleProperty, swing);
    }

    private void OnLeave(object sender, MouseEventArgs e)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var dur = new Duration(TimeSpan.FromMilliseconds(320));

        PinScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, dur) { EasingFunction = ease });
        PinScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, dur) { EasingFunction = ease });
        PinMove.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, dur) { EasingFunction = ease });
        PinMove.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, dur) { EasingFunction = ease });
        Pin.BeginAnimation(OpacityProperty, new DoubleAnimation(0.85, dur));
        PinShadow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, new DoubleAnimation(6, dur));
        PinShadow.BeginAnimation(DropShadowEffect.ShadowDepthProperty, new DoubleAnimation(2, dur));
        PinRotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(_restAngle, dur) { EasingFunction = ease });
    }
}
