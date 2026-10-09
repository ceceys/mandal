using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Mandal.Models;
using Mandal.Services;

namespace Mandal.Views;

public partial class ClipItemView : UserControl
{
    private Point _down;
    private bool _pressed;      // fotoğraf/kart üzerinde basıldı: tık = kopyala, sürükle = taşı
    private bool _textPressed;  // seçilebilir not metni üzerinde basıldı
    private bool _pinPressed;

    public ClipItemView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            RefreshTooltip();
            ApplySettings();
            Loc.Current.LanguageChanged += RefreshTooltip;
            App.Current.SettingsChanged += ApplySettings;
        };
        Unloaded += (_, _) =>
        {
            Loc.Current.LanguageChanged -= RefreshTooltip;
            App.Current.SettingsChanged -= ApplySettings;
        };
    }

    private ClipItem? Item => DataContext as ClipItem;

    private bool TextSelectable => Item is { IsText: true } && App.Current.Settings.SelectOnCard;

    private bool _editing;

    private void ApplySettings()
    {
        if (_editing) return;
        // Seçim kapalıysa metin kutusu fareyi görmez; kart tek parça gibi davranır
        TextCard.IsHitTestVisible = TextSelectable;
        TextCard.Cursor = TextSelectable ? Cursors.IBeam : Cursors.Hand;
    }

    // ---------------- Yerinde not düzenleme ----------------

    private void EditInline_Click(object sender, RoutedEventArgs e)
    {
        if (Item is { IsText: true }) BeginInlineEdit();
    }

    private void BeginInlineEdit()
    {
        if (_editing || Item is null) return;
        _editing = true;
        TextCard.Text = Item.Text ?? "";
        TextCard.IsReadOnly = false;
        TextCard.IsReadOnlyCaretVisible = true;
        TextCard.IsHitTestVisible = true;
        TextCard.Cursor = Cursors.IBeam;
        TextCard.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        TextCard.Background = System.Windows.Media.Brushes.White;
        if (Line is { } l) { l.PopupDepth++; l.AllowActivate(true); }
        TextCard.Focus();
        TextCard.CaretIndex = TextCard.Text.Length;
    }

    private void EndInlineEdit(bool save)
    {
        if (!_editing || Item is null) return;
        _editing = false;
        var text = TextCard.Text;
        TextCard.IsReadOnly = true;
        TextCard.IsReadOnlyCaretVisible = false;
        TextCard.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
        TextCard.Background = System.Windows.Media.Brushes.Transparent;
        TextCard.Select(0, 0);
        ApplySettings();
        if (Line is { } l) { l.PopupDepth = Math.Max(0, l.PopupDepth - 1); l.AllowActivate(false); }

        if (save && !string.IsNullOrWhiteSpace(text) && text.TrimEnd() != (Item.Text ?? "").TrimEnd())
            App.Current.UpdateNote(Item, text.TrimEnd()); // öğe yenilenir, kart yeniden bağlanır
        else
            TextCard.Text = Item.Preview;
    }

    private void TextCard_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_editing) return;
        if (e.Key == Key.Escape) { EndInlineEdit(save: false); e.Handled = true; }
        else if (e.Key == Key.Return && Keyboard.Modifiers == ModifierKeys.Control) { EndInlineEdit(save: true); e.Handled = true; }
    }

    private void TextCard_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_editing) EndInlineEdit(save: true);
    }

    private void RefreshTooltip()
    {
        if (Item is { } it) ToolTip = it.Summary + "\n\n" + Loc.T("Item_Hint");
    }

    // ---------------- Kart gövdesi ----------------

    private void Photo_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Item is null) return;
        if (_editing) return; // düzenleme sırasında metin kutusu her şeyi yönetir

        if (TextSelectable && IsOverText(e))
        {
            // Metin kutusu seçimi yönetir (çift tık kelimeyi seçer); bırakınca kopyalanır
            _textPressed = true;
            _pressed = false;
            return;
        }

        if (e.ClickCount == 2)
        {
            _pressed = false;
            App.Current.OpenPreview(Item);
            e.Handled = true;
            return;
        }
        _down = e.GetPosition(this);
        _pressed = true;
    }

    private void Photo_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_pressed || Item is null || e.LeftButton != MouseButtonState.Pressed) return;

        var d = e.GetPosition(this) - _down;
        if (Math.Abs(d.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(d.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        _pressed = false;
        StartDrag();
    }

    /// <summary>Mandal kopar, kart sürüklenir; bitince mandal geri takılır.</summary>
    private void StartDrag()
    {
        if (Item is null) return;
        SnapPin(true);
        try { App.Current.DragItem(this, Item); }
        finally { SnapPin(false); }
    }

    private void SnapPin(bool off)
    {
        var dur = new Duration(TimeSpan.FromMilliseconds(off ? 140 : 320));
        var ease = off ? new CubicEase { EasingMode = EasingMode.EaseOut } : (IEasingFunction)new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.7 };
        PinSnapRotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(off ? -28 : 0, dur) { EasingFunction = ease });
        PinSnapMove.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(off ? -12 : 0, dur) { EasingFunction = ease });
        PinSnapMove.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(off ? 6 : 0, dur) { EasingFunction = ease });
        PinHandle.BeginAnimation(OpacityProperty, new DoubleAnimation(off ? 0.35 : 1, dur));
    }

    private void Photo_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (Item is null || _editing) return;

        if (_textPressed)
        {
            _textPressed = false;
            // Seçim varsa yalnız seçim; yoksa tüm not
            Dispatcher.BeginInvoke(() =>
            {
                if (TextCard.SelectionLength > 0)
                {
                    App.Current.SetClipboardText(TextCard.SelectedText);
                    ShowCopied(Loc.T("Preview_SelectionCopied"));
                }
                else
                {
                    App.Current.CopyItem(Item);
                    ShowCopied(Loc.T("Item_Copied"));
                }
            }, System.Windows.Threading.DispatcherPriority.Input);
            return;
        }

        if (!_pressed) return;
        _pressed = false;
        App.Current.CopyItem(Item);
        ShowCopied(Loc.T("Item_Copied"));
    }

    private bool IsOverText(MouseEventArgs e)
    {
        var p = e.GetPosition(TextCard);
        return p.X >= 0 && p.Y >= 0 && p.X <= TextCard.ActualWidth && p.Y <= TextCard.ActualHeight;
    }

    // ---------------- Mandal tutamağı: not kartını sürükle-bırak ----------------

    private void Pin_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Item is null) return;
        _down = e.GetPosition(this);
        _pinPressed = true;
        e.Handled = true;
    }

    private void Pin_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_pinPressed || Item is null || e.LeftButton != MouseButtonState.Pressed) return;
        var d = e.GetPosition(this) - _down;
        if (Math.Abs(d.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(d.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;
        _pinPressed = false;
        StartDrag();
    }

    private void Pin_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_pinPressed || Item is null) return;
        _pinPressed = false;
        App.Current.CopyItem(Item);
        ShowCopied(Loc.T("Item_Copied"));
    }

    private void ShowCopied(string text)
    {
        CopiedText.Text = text;
        var anim = new DoubleAnimationUsingKeyFrames();
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(90))));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(650))));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(950))));
        CopiedBadge.BeginAnimation(OpacityProperty, anim);
    }

    // ---------------- Menü ve düğmeler ----------------

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (Item is null) return;
        App.Current.CopyItem(Item);
        ShowCopied(Loc.T("Item_Copied"));
    }

    private async void Ocr_Click(object sender, RoutedEventArgs e)
    {
        if (Item is null || !Item.IsImage) return;
        Cursor = Cursors.Wait;
        try
        {
            if (await App.Current.OcrItemAsync(Item)) ShowCopied(Loc.T("Item_Copied"));
        }
        finally
        {
            Cursor = Cursors.Hand;
        }
    }

    private LineWindow? Line => Window.GetWindow(this) as LineWindow;

    private void Menu_Opened(object sender, RoutedEventArgs e)
    {
        if (Line is { } l) l.PopupDepth++;
    }

    private void Menu_Closed(object sender, RoutedEventArgs e)
    {
        if (Line is { } l) l.PopupDepth = Math.Max(0, l.PopupDepth - 1);
    }

    private void AssignHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (Item is null) return;
        var line = Line;
        if (line is not null) line.PopupDepth++;
        try { new HotkeyAssignWindow(Item).ShowDialog(); }
        finally { if (line is not null) line.PopupDepth = Math.Max(0, line.PopupDepth - 1); }
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (Item is { IsText: true }) App.Current.OpenNoteEditor(Item);
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (Item is not null) App.Current.OpenPreview(Item);
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (Item is not null) App.Current.OpenItem(Item);
    }

    private void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (Item is not null) App.Current.SaveItemAs(Item);
    }

    private void ShowInFolder_Click(object sender, RoutedEventArgs e)
    {
        if (Item is not null) App.Current.ShowInFolder(Item);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Item is not null) App.Current.DeleteItem(Item);
    }
}
