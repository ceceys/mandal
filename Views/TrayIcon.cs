using System.Drawing;
using System.Windows;
using Mandal.Services;
using WF = System.Windows.Forms;

namespace Mandal.Views;

public sealed class TrayIcon : IDisposable
{
    private readonly WF.NotifyIcon _icon;
    private readonly App _app;

    public TrayIcon(App app)
    {
        _app = app;
        _icon = new WF.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = Loc.T("Tray_Text"),
            Visible = true,
            ContextMenuStrip = BuildMenu(),
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == WF.MouseButtons.Left) app.ToggleLine();
        };
        Loc.Current.LanguageChanged += Rebuild;
    }

    /// <summary>Dil veya kısayol değişince menüyü yeniden kurar.</summary>
    public void Rebuild()
    {
        var old = _icon.ContextMenuStrip;
        _icon.ContextMenuStrip = BuildMenu();
        _icon.Text = Loc.T("Tray_Text");
        old?.Dispose();
    }

    private WF.ContextMenuStrip BuildMenu()
    {
        var app = _app;
        var s = app.Settings;
        var menu = new WF.ContextMenuStrip();

        menu.Items.Add(new WF.ToolStripMenuItem(Loc.T("Menu_ToggleLine"), null, (_, _) => app.ToggleLine())
        {
            ShortcutKeyDisplayString = s.HotkeyToggleLine,
            Font = new Font(menu.Font, System.Drawing.FontStyle.Bold),
        });
        menu.Items.Add(new WF.ToolStripSeparator());

        menu.Items.Add(new WF.ToolStripMenuItem(Loc.T("Menu_CaptureRegion"), null, (_, _) => app.CaptureRegion())
            { ShortcutKeyDisplayString = s.HotkeyRegion });
        menu.Items.Add(new WF.ToolStripMenuItem(Loc.T("Menu_CaptureFullScreen"), null, (_, _) => app.CaptureFullScreen())
            { ShortcutKeyDisplayString = s.HotkeyFullScreen });
        menu.Items.Add(new WF.ToolStripMenuItem(Loc.T("Menu_CaptureWindow"), null, (_, _) => app.CaptureWindow())
            { ShortcutKeyDisplayString = s.HotkeyWindow });
        menu.Items.Add(new WF.ToolStripSeparator());

        var watch = new WF.ToolStripMenuItem(Loc.T("Menu_WatchClipboard"), null,
            (_, _) => app.SetWatchClipboard(!app.Settings.WatchClipboard));
        var showOnCapture = new WF.ToolStripMenuItem(Loc.T("Menu_ShowOnCapture"), null,
            (_, _) => app.SetShowOnCapture(!app.Settings.ShowOnCapture));
        var hideAfterCopy = new WF.ToolStripMenuItem(Loc.T("Menu_HideAfterCopy"), null,
            (_, _) => app.SetHideLineAfterCopy(!app.Settings.HideLineAfterCopy));
        var autostart = new WF.ToolStripMenuItem(Loc.T("Menu_Autostart"), null,
            (_, _) => app.SetAutostart(!Autostart.IsEnabled()));
        menu.Items.Add(watch);
        menu.Items.Add(showOnCapture);
        menu.Items.Add(hideAfterCopy);
        menu.Items.Add(autostart);

        // Dil alt menüsü
        var language = new WF.ToolStripMenuItem(Loc.T("Menu_Language"));
        var autoItem = new WF.ToolStripMenuItem(Loc.T("Menu_LanguageAuto"), null, (_, _) => app.SetLanguage(Loc.Auto))
            { Checked = app.Settings.Language == Loc.Auto };
        language.DropDownItems.Add(autoItem);
        language.DropDownItems.Add(new WF.ToolStripSeparator());
        foreach (var (code, native) in Loc.Languages)
        {
            var c = code;
            language.DropDownItems.Add(new WF.ToolStripMenuItem(native, null, (_, _) => app.SetLanguage(c))
                { Checked = app.Settings.Language == c });
        }
        menu.Items.Add(language);
        menu.Items.Add(new WF.ToolStripSeparator());

        menu.Items.Add(new WF.ToolStripMenuItem(Loc.T("Menu_Settings"), null, (_, _) => app.OpenSettings()));
        menu.Items.Add(new WF.ToolStripMenuItem(Loc.T("Menu_OpenFolder"), null, (_, _) => app.OpenFolder()));
        menu.Items.Add(new WF.ToolStripMenuItem(Loc.T("Menu_OpenSettings"), null, (_, _) => app.OpenSettingsFile()));
        menu.Items.Add(new WF.ToolStripSeparator());
        menu.Items.Add(new WF.ToolStripMenuItem(Loc.T("Menu_Exit"), null, (_, _) => app.ExitApp()));

        menu.Opening += (_, _) =>
        {
            watch.Checked = app.Settings.WatchClipboard;
            showOnCapture.Checked = app.Settings.ShowOnCapture;
            hideAfterCopy.Checked = app.Settings.HideLineAfterCopy;
            autostart.Checked = Autostart.IsEnabled();
        };

        return menu;
    }

    private static Icon LoadIcon()
    {
        try
        {
            var sri = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/mandal.ico"));
            if (sri is not null) return new Icon(sri.Stream);
        }
        catch (Exception ex)
        {
            Log.Write(ex, "Tepsi simgesi yüklenemedi");
        }
        return SystemIcons.Application;
    }

    public void Balloon(string title, string text, WF.ToolTipIcon kind = WF.ToolTipIcon.Info)
        => _icon.ShowBalloonTip(4000, title, text, kind);

    public void Dispose()
    {
        Loc.Current.LanguageChanged -= Rebuild;
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
    }
}
