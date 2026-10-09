using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Mandal.Services;

namespace Mandal.Views;

/// <summary>
/// Sol üst köşede çapraz asılı mandal: tıklayınca ip açılır/kapanır.
/// Fare yaklaşınca öne doğru büyür, hafif sallanır; uzaklaşınca geri çekilir.
/// </summary>
public partial class CornerTabWindow : Window
{
    private const double RestAngle = -38;

    public CornerTabWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
            NativeMethods.MakeNoActivateToolWindow(new WindowInteropHelper(this).Handle);
    }

    private void OnClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        App.Current.ToggleLine();
    }

    private void OnEnter(object sender, MouseEventArgs e)
    {
        var pop = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 };
        var dur = new Duration(TimeSpan.FromMilliseconds(260));

        PinScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.35, dur) { EasingFunction = pop });
        PinScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.35, dur) { EasingFunction = pop });
        PinMove.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(8, dur) { EasingFunction = pop });
        PinMove.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, dur) { EasingFunction = pop });
        Pin.BeginAnimation(OpacityProperty, new DoubleAnimation(1, dur));
        PinShadow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, new DoubleAnimation(14, dur));
        PinShadow.BeginAnimation(DropShadowEffect.ShadowDepthProperty, new DoubleAnimation(6, dur));

        // İpe asılı gibi hafif sallanma
        var swing = new DoubleAnimationUsingKeyFrames();
        swing.KeyFrames.Add(new EasingDoubleKeyFrame(RestAngle + 10, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(140))));
        swing.KeyFrames.Add(new EasingDoubleKeyFrame(RestAngle - 6, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(320))));
        swing.KeyFrames.Add(new EasingDoubleKeyFrame(RestAngle + 3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(480))));
        swing.KeyFrames.Add(new EasingDoubleKeyFrame(RestAngle, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(620)))
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
        PinRotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(RestAngle, dur) { EasingFunction = ease });
    }
}
