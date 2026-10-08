using System.Windows;
using Mandal.Models;

namespace Mandal.Views;

public partial class HotkeyAssignWindow : Window
{
    private readonly ClipItem _item;

    public HotkeyAssignWindow(ClipItem item)
    {
        InitializeComponent();
        _item = item;
        ItemLabel.Text = item.Summary.Replace('\n', ' ');
        Box.Hotkey = item.Hotkey ?? "";
        ClearButton.Visibility = item.HasHotkey ? Visibility.Visible : Visibility.Collapsed;
        // İp penceresi odak almaz; bu pencere açılınca kendini öne getirmeli ki tuşlar buraya gelsin.
        Loaded += (_, _) => { Activate(); Box.Focus(); };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Error.Visibility = Visibility.Collapsed;
        var gesture = Box.Hotkey;
        if (string.IsNullOrWhiteSpace(gesture))
        {
            App.Current.AssignItemHotkey(_item, null);
            Close();
            return;
        }
        if (!App.Current.AssignItemHotkey(_item, gesture))
        {
            Error.Visibility = Visibility.Visible;
            return;
        }
        Close();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        App.Current.AssignItemHotkey(_item, null);
        Close();
    }
}
