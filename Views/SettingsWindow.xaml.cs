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
        MaxHeight = SystemParameters.WorkArea.Height - 16; // ekrana sığmazsa içerik kayar, düğmeler altta kalır
        _copy = App.Current.Settings.Clone();
        DataContext = _copy;

        FillLanguages();
        Loc.Current.LanguageChanged += FillLanguages;
        Closed += (_, _) => Loc.Current.LanguageChanged -= FillLanguages;

        SecondsSlider.Value = Math.Clamp(_copy.ShowOnCaptureSeconds, 1, 10);
        AutostartCheck.IsChecked = Autostart.IsEnabled();
        DaysSlider.Value = Math.Clamp(_copy.AutoDeleteDays, 1, 365);
        UpdateStorageUi();
        FillCorners();

        VersionText.Text = "Mandal " + About.Version;
        NotesBox.Text = About.ReleaseNotes;
        if (string.IsNullOrEmpty(About.LinkedInUrl))
        {
            LinkedInSep.Text = "";
            LinkedInLink.Inlines.Clear();
        }
        Loc.Current.LanguageChanged += UpdateStorageUi;
        Loc.Current.LanguageChanged += FillCorners;
        Loc.Current.LanguageChanged += RefreshNotes;
        Closed += (_, _) =>
        {
            Loc.Current.LanguageChanged -= UpdateStorageUi;
            Loc.Current.LanguageChanged -= FillCorners;
            Loc.Current.LanguageChanged -= RefreshNotes;
        };
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

    private bool _loadingCorners;

    private void FillCorners()
    {
        _loadingCorners = true;
        try
        {
            CornerCombo.ItemsSource = Settings.Corners.Select(c => new LangOption(c, Loc.T("Corner_" + c))).ToList();
            CornerCombo.SelectedValue = _copy.CornerPosition;
        }
        finally { _loadingCorners = false; }
    }

    private void CornerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingCorners || CornerCombo.SelectedValue is not string code) return;
        _copy.CornerPosition = code;
    }

    private void UpdateStorageUi()
    {
        AutoPanel.IsEnabled = KeepCheck.IsChecked != true;
        AutoPanel.Opacity = AutoPanel.IsEnabled ? 1 : 0.5;
        AutoLabel.Text = Loc.F("Settings_AutoDelete", (int)DaysSlider.Value);
    }

    private void KeepCheck_Changed(object sender, RoutedEventArgs e) => UpdateStorageUi();

    private void DaysSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (AutoLabel is not null) UpdateStorageUi();
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
        if (ApplyChanges()) Close();
    }

    /// <summary>Uygula: pencere açık kalır, değişiklikler hemen etkili olur; birden fazla ayarı deneyerek görebilirsin.</summary>
    private void Apply_Click(object sender, RoutedEventArgs e) => ApplyChanges();

    /// <returns>Doğrulama geçtiyse true.</returns>
    private bool ApplyChanges()
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
                return false;
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
            return false;
        }

        _copy.ShowOnCaptureSeconds = SecondsSlider.Value;
        _copy.AutoDeleteDays = (int)DaysSlider.Value;

        bool wantAutostart = AutostartCheck.IsChecked == true;
        if (wantAutostart != Autostart.IsEnabled()) App.Current.SetAutostart(wantAutostart);

        App.Current.ApplySettings(_copy);
        Activate(); // ip veya köşe mandalı yenilenirken odak bizde kalsın
        return true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void RefreshNotes() => NotesBox.Text = About.ReleaseNotes;

    private void GitHub_Click(object sender, RoutedEventArgs e) => About.Open(About.GitHubUrl);

    private void CheckUpdates_Click(object sender, RoutedEventArgs e) => _ = App.Current.CheckForUpdatesAsync(manual: true);

    private void LinkedIn_Click(object sender, RoutedEventArgs e) => About.Open(About.LinkedInUrl);
}
