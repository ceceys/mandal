using System.Windows;
using System.Windows.Controls;
using Mandal.Services;
using WF = System.Windows.Forms;

namespace Mandal.Views;

public partial class SettingsWindow : Window
{
    private sealed record LangOption(string Code, string Name);

    private readonly Settings _copy;
    private bool _loadingLanguages;

    public SettingsWindow()
    {
        InitializeComponent();
        _copy = App.Current.Settings.Clone();
        DataContext = _copy;

        FillLanguages();
        Loc.Current.LanguageChanged += FillLanguages;
        Closed += (_, _) => Loc.Current.LanguageChanged -= FillLanguages;

        SecondsSlider.Value = Math.Clamp(_copy.ShowOnCaptureSeconds, 1, 10);
        AutostartCheck.IsChecked = Autostart.IsEnabled();
        Loaded += (_, _) => Activate();
    }

    private void FillLanguages()
    {
        _loadingLanguages = true;
        try
        {
            var items = new List<LangOption> { new(Loc.Auto, Loc.T("Menu_LanguageAuto")) };
            items.AddRange(Loc.Languages.Select(l => new LangOption(l.Code, l.Native)));
            LanguageCombo.ItemsSource = items;
            LanguageCombo.SelectedValue = _copy.Language;
            if (LanguageCombo.SelectedValue is null) LanguageCombo.SelectedValue = Loc.Auto;
        }
        finally
        {
            _loadingLanguages = false;
        }
    }

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingLanguages || LanguageCombo.SelectedValue is not string code) return;
        if (code == _copy.Language) return;
        _copy.Language = code;
        App.Current.SetLanguage(code); // anında uygulanır; dil ayarı iptalle geri alınmaz
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        using var dlg = new WF.FolderBrowserDialog
        {
            SelectedPath = Directory.Exists(_copy.ClipFolder) ? _copy.ClipFolder : Settings.DefaultClipFolder,
            UseDescriptionForTitle = true,
            Description = Loc.T("Settings_Folder"),
        };
        if (dlg.ShowDialog() == WF.DialogResult.OK)
        {
            _copy.ClipFolder = dlg.SelectedPath;
            FolderBox.Text = dlg.SelectedPath;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        HotkeyError.Visibility = Visibility.Collapsed;
        FolderError.Visibility = Visibility.Collapsed;

        // Aynı kombinasyon iki kez kullanılmasın
        var seen = new HashSet<(uint, uint)>();
        foreach (var g in new[] { _copy.HotkeyRegion, _copy.HotkeyRegionAlt, _copy.HotkeyFullScreen, _copy.HotkeyWindow, _copy.HotkeyToggleLine })
        {
            if (string.IsNullOrWhiteSpace(g)) continue;
            if (HotkeyManager.TryParse(g, out var mods, out var vk) && !seen.Add((mods, vk)))
            {
                HotkeyError.Visibility = Visibility.Visible;
                return;
            }
        }

        // Klasör oluşturulabilmeli
        try
        {
            var full = Path.GetFullPath(_copy.ClipFolder);
            Directory.CreateDirectory(full);
            _copy.ClipFolder = full;
        }
        catch
        {
            FolderError.Visibility = Visibility.Visible;
            return;
        }

        _copy.ShowOnCaptureSeconds = SecondsSlider.Value;

        bool wantAutostart = AutostartCheck.IsChecked == true;
        if (wantAutostart != Autostart.IsEnabled()) App.Current.SetAutostart(wantAutostart);

        App.Current.ApplySettings(_copy);
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
