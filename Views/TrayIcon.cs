using System.Drawing;
using System.Windows;
using Mandal.Services;
using WF = System.Windows.Forms;

namespace Mandal.Views;

public sealed class TrayIcon : IDisposable
{
    private readonly WF.NotifyIcon _icon;
    private readonly WF.ToolStripMenuItem _watch;
    private readonly WF.ToolStripMenuItem _showOnCapture;
    private readonly WF.ToolStripMenuItem _hideAfterCopy;
    private readonly WF.ToolStripMenuItem _autostart;

    public TrayIcon(App app)
    {
        var s = app.Settings;
        var menu = new WF.ContextMenuStrip();

        var toggle = new WF.ToolStripMenuItem("İpi göster / gizle", null, (_, _) => app.ToggleLine())
        {
            ShortcutKeyDisplayString = s.HotkeyToggleLine,
            Font = new Font(menu.Font, System.Drawing.FontStyle.Bold),
        };
        menu.Items.Add(toggle);
        menu.Items.Add(new WF.ToolStripSeparator());

        menu.Items.Add(new WF.ToolStripMenuItem("Bölge al", null, (_, _) => app.CaptureRegion())
            { ShortcutKeyDisplayString = s.HotkeyRegion });
        menu.Items.Add(new WF.ToolStripMenuItem("Tam ekran al", null, (_, _) => app.CaptureFullScreen())
            { ShortcutKeyDisplayString = s.HotkeyFullScreen });
        menu.Items.Add(new WF.ToolStripMenuItem("Aktif pencere al", null, (_, _) => app.CaptureWindow())
            { ShortcutKeyDisplayString = s.HotkeyWindow });
        menu.Items.Add(new WF.ToolStripSeparator());

        _watch = new WF.ToolStripMenuItem("Panoyu izle (panoya düşen görüntüler asılır)", null,
            (_, _) => app.SetWatchClipboard(!app.Settings.WatchClipboard));
        _showOnCapture = new WF.ToolStripMenuItem("Yeni alıntıda ipi kısa süre göster", null,
            (_, _) => app.SetShowOnCapture(!app.Settings.ShowOnCapture));
        _hideAfterCopy = new WF.ToolStripMenuItem("Kopyalayınca ipi kaldır", null,
            (_, _) => app.SetHideLineAfterCopy(!app.Settings.HideLineAfterCopy));
        _autostart = new WF.ToolStripMenuItem("Windows ile başlat", null,
            (_, _) => app.SetAutostart(!Autostart.IsEnabled()));
        menu.Items.Add(_watch);
        menu.Items.Add(_showOnCapture);
        menu.Items.Add(_hideAfterCopy);
        menu.Items.Add(_autostart);
        menu.Items.Add(new WF.ToolStripSeparator());

        menu.Items.Add(new WF.ToolStripMenuItem("Alıntı klasörünü aç", null, (_, _) => app.OpenFolder()));
        menu.Items.Add(new WF.ToolStripMenuItem("Ayarlar dosyasını aç", null, (_, _) => app.OpenSettingsFile()));
        menu.Items.Add(new WF.ToolStripSeparator());
        menu.Items.Add(new WF.ToolStripMenuItem("Çıkış", null, (_, _) => app.ExitApp()));

        menu.Opening += (_, _) =>
        {
            _watch.Checked = app.Settings.WatchClipboard;
            _showOnCapture.Checked = app.Settings.ShowOnCapture;
            _hideAfterCopy.Checked = app.Settings.HideLineAfterCopy;
            _autostart.Checked = Autostart.IsEnabled();
        };

        _icon = new WF.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Mandal · çamaşır ipi",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == WF.MouseButtons.Left) app.ToggleLine();
        };
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
        _icon.Visible = false;
        _icon.Dispose();
    }
}
