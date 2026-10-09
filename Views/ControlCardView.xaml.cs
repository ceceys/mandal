using System.Windows;
using System.Windows.Controls;

namespace Mandal.Views;

/// <summary>İpin sağ ucundaki sabit kart: al, ayarlar, kaldır.</summary>
public partial class ControlCardView : UserControl
{
    public ControlCardView()
    {
        InitializeComponent();
    }

    private void Capture_Click(object sender, RoutedEventArgs e) => App.Current.CaptureRegion();

    private void Settings_Click(object sender, RoutedEventArgs e) => App.Current.OpenSettings();

    private void Hide_Click(object sender, RoutedEventArgs e) => App.Current.HideLine();

    private void DeleteAll_Click(object sender, RoutedEventArgs e) => App.Current.DeleteAll();
}
