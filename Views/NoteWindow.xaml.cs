using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Mandal.Models;
using Mandal.Services;

namespace Mandal.Views;

/// <summary>Not yazma / düzenleme kartı. Kaydedince not ipe metin kartı olarak asılır.</summary>
public partial class NoteWindow : Window
{
    private readonly ClipItem? _editing;

    /// <summary>Kaydedilen metin; iptal edilirse null.</summary>
    public string? SavedText { get; private set; }

    /// <summary>İptal edilse bile yazılanlar: taslak olarak saklanır, hiçbir not kaybolmaz.</summary>
    public string DraftText => Editor.Text;

    public NoteWindow(ClipItem? editing, string? draft = null)
    {
        InitializeComponent();
        _editing = editing;
        if (editing is not null)
        {
            Editor.Text = editing.Text ?? "";
            HeaderText.Text = Loc.T("Item_Edit");
        }
        else if (!string.IsNullOrEmpty(draft))
        {
            Editor.Text = draft;
        }
        UpdateState();

        Left = Math.Max(0, (SystemParameters.PrimaryScreenWidth - Width) / 2);
        Top = 196;

        Loaded += (_, _) =>
        {
            Activate();
            Editor.Focus();
            Editor.CaretIndex = Editor.Text.Length;
            PlayDrop();
        };
    }

    private void PlayDrop()
    {
        Drop.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, new Duration(TimeSpan.FromMilliseconds(380)))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 },
        });
        var swing = new DoubleAnimationUsingKeyFrames();
        swing.KeyFrames.Add(new EasingDoubleKeyFrame(2, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220))));
        swing.KeyFrames.Add(new EasingDoubleKeyFrame(-1.8, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(480))));
        swing.KeyFrames.Add(new EasingDoubleKeyFrame(0.4, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(700))));
        swing.KeyFrames.Add(new EasingDoubleKeyFrame(-1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(900)))
            { EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut } });
        Swing.BeginAnimation(RotateTransform.AngleProperty, swing);
    }

    private void UpdateState()
    {
        bool empty = string.IsNullOrWhiteSpace(Editor.Text);
        Placeholder.Visibility = Editor.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        SaveButton.IsEnabled = !empty;
        CopyButton.IsEnabled = !empty;
        CharCount.Text = Loc.F("Item_TextChars", Editor.Text.Length);
    }

    private void Editor_TextChanged(object sender, TextChangedEventArgs e) => UpdateState();

    private void Finish(string? result)
    {
        SavedText = result;
        var up = new DoubleAnimation(-60, new Duration(TimeSpan.FromMilliseconds(180)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
        };
        up.Completed += (_, _) => Close();
        Drop.BeginAnimation(TranslateTransform.YProperty, up);
        Card.BeginAnimation(OpacityProperty, new DoubleAnimation(0, new Duration(TimeSpan.FromMilliseconds(180))));
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Editor.Text)) return;
        Finish(Editor.Text.TrimEnd());
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Finish(null);

    private void CopyAll_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(Editor.Text); }
        catch (Exception ex) { Log.Write(ex, "Not kopyalama"); }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Finish(null); e.Handled = true; }
        else if (e.Key == Key.Return && Keyboard.Modifiers == ModifierKeys.Control) { Save_Click(sender, e); e.Handled = true; }
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not (Button or TextBox) && !Editor.IsMouseOver) DragMove();
    }
}
