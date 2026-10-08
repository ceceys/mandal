using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Mandal.Services;

namespace Mandal.Views;

/// <summary>Sol üst köşede duran küçük mandal: tıklayınca ip açılır/kapanır.</summary>
public partial class CornerTabWindow : Window
{
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
}
