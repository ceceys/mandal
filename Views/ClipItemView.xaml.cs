using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Mandal.Models;
using Mandal.Services;

namespace Mandal.Views;

public partial class ClipItemView : UserControl
{
    private Point _down;
    private bool _pressed;

    public ClipItemView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            RefreshTooltip();
            Loc.Current.LanguageChanged += RefreshTooltip;
        };
        Unloaded += (_, _) => Loc.Current.LanguageChanged -= RefreshTooltip;
    }

    private ClipItem? Item => DataContext as ClipItem;

    private void RefreshTooltip()
    {
        if (Item is { } it) ToolTip = it.Summary + "\n\n" + Loc.T("Item_Hint");
    }

    private void Photo_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Item is null) return;
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
        App.Current.DragItem(this, Item);
    }

    private void Photo_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_pressed || Item is null) return;
        _pressed = false;
        App.Current.CopyItem(Item);
        ShowCopied();
    }

    private void ShowCopied()
    {
        var anim = new DoubleAnimationUsingKeyFrames();
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(90))));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(650))));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(950))));
        CopiedBadge.BeginAnimation(OpacityProperty, anim);
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (Item is null) return;
        App.Current.CopyItem(Item);
        ShowCopied();
    }

    private async void Ocr_Click(object sender, RoutedEventArgs e)
    {
        if (Item is null || !Item.IsImage) return;
        Cursor = Cursors.Wait;
        try
        {
            if (await App.Current.OcrItemAsync(Item)) ShowCopied();
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
