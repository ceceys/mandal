using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Mandal.Views;

/// <summary>İpin sağ ucundaki sabit kart: al, not, ayarlar, kaldır; çizginin altında ipi boşalt ve panoyu temizle.</summary>
public partial class ControlCardView : UserControl
{
    private readonly DispatcherTimer _doneTimer = new() { Interval = TimeSpan.FromMilliseconds(1400) };

    public ControlCardView()
    {
        InitializeComponent();
        _doneTimer.Tick += (_, _) =>
        {
            _doneTimer.Stop();
            ClipboardDone.Visibility = Visibility.Hidden;
            ClipboardLabel.Visibility = Visibility.Visible;
        };
    }

    private void Capture_Click(object sender, RoutedEventArgs e) => App.Current.CaptureRegion();

    private void Settings_Click(object sender, RoutedEventArgs e) => App.Current.OpenSettings();

    private void AddNote_Click(object sender, RoutedEventArgs e) => App.Current.OpenNoteEditor(null);

    private void Hide_Click(object sender, RoutedEventArgs e) => App.Current.HideLine();

    private void ClearLine_Click(object sender, RoutedEventArgs e) => App.Current.ClearLine();

    /// <summary>Panoyu temizler; ipte değişen bir şey olmadığı için yazı kısa süre "Temizlendi" olur.</summary>
    private void ClearClipboard_Click(object sender, RoutedEventArgs e)
    {
        if (!App.Current.ClearClipboard()) return;
        ClipboardLabel.Visibility = Visibility.Hidden;
        ClipboardDone.Visibility = Visibility.Visible;
        _doneTimer.Stop();
        _doneTimer.Start();
    }
}
