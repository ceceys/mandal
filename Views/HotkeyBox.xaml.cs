using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Mandal.Services;

namespace Mandal.Views;

/// <summary>Tıkla, tuş kombinasyonuna bas: "Ctrl+Shift+S". Backspace/Delete temizler, Esc odaktan çıkar.</summary>
public partial class HotkeyBox : UserControl
{
    public static readonly DependencyProperty HotkeyProperty = DependencyProperty.Register(
        nameof(Hotkey), typeof(string), typeof(HotkeyBox),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHotkeyChanged));

    public string Hotkey
    {
        get => (string)GetValue(HotkeyProperty);
        set => SetValue(HotkeyProperty, value);
    }

    public HotkeyBox()
    {
        InitializeComponent();
        Render();
    }

    private static void OnHotkeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((HotkeyBox)d).Render();

    private void Render()
    {
        var v = Hotkey ?? "";
        ValueText.Text = v;
        Placeholder.Visibility = string.IsNullOrEmpty(v) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        Focus();
        e.Handled = true;
    }

    private void OnFocusChanged(object sender, KeyboardFocusChangedEventArgs e)
    {
        bool focused = IsKeyboardFocusWithin;
        Frame.BorderBrush = focused ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("LineBrush");
        Frame.BorderThickness = new Thickness(focused ? 2 : 1);

        // Kutu odaktayken sistem geneli kısayollar askıda: yoksa kayıtlı bir kombinasyona basınca
        // Windows tuşu bize vermez, kutuya hiçbir şey yazılmazdı (ör. Ctrl+Shift+F alıntı alırdı).
        if (focused) App.Current.SuspendHotkeys(); else App.Current.ResumeHotkeys();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Değiştiricisiz Enter / Tab / Esc pencerenin işi: Kaydet, odak gezdirme, İptal
        if (Keyboard.Modifiers == ModifierKeys.None && key is Key.Return or Key.Tab or Key.Escape)
            return;

        e.Handled = true;

        switch (key)
        {
            case Key.Back:
            case Key.Delete:
                Hotkey = "";
                return;
            case Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
                or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin:
                return; // ana tuşu bekle
            case Key.Snapshot:
                return; // PrintScreen yalnız KeyUp'ta gelir
        }

        Apply(key, Keyboard.Modifiers);
    }

    private void OnPreviewKeyUp(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Snapshot)
        {
            e.Handled = true;
            Apply(key, Keyboard.Modifiers);
        }
    }

    private void Apply(Key key, ModifierKeys mods)
    {
        var name = KeyName(key);
        if (name is null) return;

        bool isFunction = key is >= Key.F1 and <= Key.F24 || key == Key.Snapshot;
        if (mods == ModifierKeys.None && !isFunction) return; // harf/rakam tek başına sistem geneli kısayol olmaz

        var parts = new List<string>(4);
        if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (mods.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (mods.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (mods.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(name);

        var gesture = string.Join("+", parts);
        if (HotkeyManager.TryParse(gesture, out _, out _)) Hotkey = gesture;
    }

    private static string? KeyName(Key key)
    {
        if (key >= Key.A && key <= Key.Z) return key.ToString();
        if (key >= Key.D0 && key <= Key.D9) return ((int)key - (int)Key.D0).ToString();
        if (key >= Key.F1 && key <= Key.F24) return key.ToString();
        return key switch
        {
            Key.Snapshot => "PrintScreen",
            Key.Space => "Space",
            Key.Return => "Enter",
            Key.Tab => "Tab",
            Key.Insert => "Insert",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.Oem3 => "`",
            Key.Up or Key.Down or Key.Left or Key.Right => key.ToString(),
            _ => null,
        };
    }
}
