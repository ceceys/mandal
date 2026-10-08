using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using Mandal.Models;
using Mandal.Services;
using Mandal.Views;
using Drawing = System.Drawing;
using WF = System.Windows.Forms;

namespace Mandal;

public partial class App : Application
{
    public static new App Current => (App)Application.Current;

    public Settings Settings { get; private set; } = null!;
    public ClipStore Store { get; private set; } = null!;

    private LineWindow? _line;
    private TrayIcon? _tray;
    private MessageWindow? _msg;
    private HotkeyManager? _hotkeys;
    private ClipboardWatcher? _clipboard;

    private Mutex? _mutex;
    private bool _ownsMutex;
    private EventWaitHandle? _showEvent;
    private bool _capturing;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, @"Local\Mandal.SingleInstance", out _ownsMutex);
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Mandal.ShowLine");
        if (!_ownsMutex)
        {
            // Zaten çalışıyor: ona "ipi göster" de ve çık.
            _showEvent.Set();
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, ex) =>
        {
            Log.Write(ex.Exception, "Beklenmeyen hata");
            _tray?.Balloon("Mandal", "Bir hata oluştu, günlüğe yazıldı.", WF.ToolTipIcon.Warning);
            ex.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            Log.Write(ex.ExceptionObject as Exception ?? new Exception(ex.ExceptionObject?.ToString()), "Ölümcül hata");

        Settings = Settings.Load();
        Store = new ClipStore(Settings.ClipFolder, Settings.ThumbnailHeight);
        try { Store.Load(); }
        catch (Exception ex) { Log.Write(ex, "Alıntı klasörü yüklenemedi"); }

        _msg = new MessageWindow();
        _hotkeys = new HotkeyManager(_msg);
        _clipboard = new ClipboardWatcher(_msg, Settings.ClipFolder);
        _clipboard.ImageArrived += OnClipboardImage;
        _clipboard.Enabled = Settings.WatchClipboard;

        _line = new LineWindow(Store);
        _tray = new TrayIcon(this);

        RegisterHotkeys();
        StartShowListener();
    }

    private void RegisterHotkeys()
    {
        var failures = new List<string>();

        void Reg(string gesture, Action action)
        {
            if (string.IsNullOrWhiteSpace(gesture)) return;
            if (!_hotkeys!.Register(gesture, action, out var err))
            {
                failures.Add(err);
                Log.Write("Kısayol: " + err);
            }
        }

        Reg(Settings.HotkeyRegion, CaptureRegion);
        Reg(Settings.HotkeyRegionAlt, CaptureRegion);
        Reg(Settings.HotkeyFullScreen, CaptureFullScreen);
        Reg(Settings.HotkeyWindow, CaptureWindow);
        Reg(Settings.HotkeyToggleLine, ToggleLine);

        if (failures.Count > 0)
            _tray?.Balloon("Bazı kısayollar kaydedilemedi", string.Join("\n", failures), WF.ToolTipIcon.Warning);
    }

    private void StartShowListener()
    {
        var t = new Thread(() =>
        {
            while (true)
            {
                try { _showEvent!.WaitOne(); }
                catch { return; }
                Dispatcher.BeginInvoke(() => _line?.ShowAnimated());
            }
        })
        { IsBackground = true, Name = "Mandal.ShowListener" };
        t.Start();
    }

    // ---------------- Alıntı alma ----------------

    public async void CaptureRegion()
    {
        if (_capturing) return;
        _capturing = true;
        try
        {
            await HideLineForCaptureAsync();
            var vs = ScreenCapture.VirtualScreen;
            using var full = ScreenCapture.CaptureVirtualScreen();
            var sel = RegionSelectWindow.Select(full, vs);
            if (sel is { } r)
            {
                using var crop = ScreenCapture.Crop(full, new Drawing.Rectangle(r.X - vs.X, r.Y - vs.Y, r.Width, r.Height));
                Hang(Store.Add(crop));
            }
        }
        catch (Exception ex) { Fail(ex, "Bölge alıntısı"); }
        finally { _capturing = false; }
    }

    public async void CaptureFullScreen()
    {
        if (_capturing) return;
        _capturing = true;
        try
        {
            await HideLineForCaptureAsync();
            using var bmp = ScreenCapture.Capture(ScreenCapture.ScreenAtCursor());
            Hang(Store.Add(bmp));
        }
        catch (Exception ex) { Fail(ex, "Tam ekran alıntısı"); }
        finally { _capturing = false; }
    }

    public async void CaptureWindow()
    {
        if (_capturing) return;
        _capturing = true;
        try
        {
            var bounds = ScreenCapture.ForegroundWindowBounds();
            if (bounds is null)
            {
                _tray?.Balloon("Mandal", "Aktif pencere bulunamadı.", WF.ToolTipIcon.Warning);
                return;
            }
            await HideLineForCaptureAsync();
            using var bmp = ScreenCapture.Capture(bounds.Value);
            Hang(Store.Add(bmp));
        }
        catch (Exception ex) { Fail(ex, "Pencere alıntısı"); }
        finally { _capturing = false; }
    }

    private async Task HideLineForCaptureAsync()
    {
        if (_line is { IsVisible: true })
        {
            _line.HideImmediate();
            await Task.Delay(120); // ekran yeniden çizilsin
        }
    }

    /// <summary>Yeni öğe ipe asıldı: istenirse ipi kısa süre göster.</summary>
    private void Hang(ClipItem item)
    {
        if (Settings.ShowOnCapture)
            _line?.ShowTemporarily(TimeSpan.FromSeconds(Math.Max(0.5, Settings.ShowOnCaptureSeconds)));
    }

    private void OnClipboardImage(byte[] png)
    {
        try { Hang(Store.AddPng(png)); }
        catch (Exception ex) { Fail(ex, "Pano görüntüsü"); }
    }

    // ---------------- Öğe işlemleri ----------------

    public void CopyItem(ClipItem item)
    {
        try
        {
            var data = new DataObject();
            data.SetFileDropList(new StringCollection { item.Path });
            data.SetImage(item.LoadFull());
            data.SetData("PNG", new MemoryStream(File.ReadAllBytes(item.Path)), false);

            _clipboard?.SuppressFor(TimeSpan.FromSeconds(2));
            Clipboard.SetDataObject(data, true);

            if (Settings.HideLineAfterCopy) _line?.HideAnimated(TimeSpan.FromMilliseconds(650));
        }
        catch (Exception ex) { Fail(ex, "Kopyalama"); }
    }

    public void DragItem(DependencyObject source, ClipItem item)
    {
        try
        {
            var data = new DataObject();
            data.SetFileDropList(new StringCollection { item.Path });
            data.SetImage(item.LoadFull());

            if (_line is not null) _line.IsDragging = true;
            var result = DragDrop.DoDragDrop(source, data, DragDropEffects.Copy);
            if (_line is not null) _line.IsDragging = false;

            if (result != DragDropEffects.None && Settings.HideLineAfterCopy)
                _line?.HideAnimated(TimeSpan.FromMilliseconds(150));
        }
        catch (Exception ex)
        {
            if (_line is not null) _line.IsDragging = false;
            Fail(ex, "Sürükleme");
        }
    }

    public void DeleteItem(ClipItem item)
    {
        try { Store.Delete(item); }
        catch (Exception ex) { Fail(ex, "Silme"); }
    }

    public void OpenItem(ClipItem item)
    {
        try { Process.Start(new ProcessStartInfo(item.Path) { UseShellExecute = true }); }
        catch (Exception ex) { Fail(ex, "Açma"); }
    }

    public void ShowInFolder(ClipItem item)
    {
        try { Process.Start("explorer.exe", $"/select,\"{item.Path}\""); }
        catch (Exception ex) { Fail(ex, "Klasörde göster"); }
    }

    public void SaveItemAs(ClipItem item)
    {
        try
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Alıntıyı kaydet",
                Filter = "PNG görüntü|*.png",
                FileName = item.FileName,
                DefaultExt = ".png",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            };
            if (dlg.ShowDialog() == true) File.Copy(item.Path, dlg.FileName, true);
        }
        catch (Exception ex) { Fail(ex, "Farklı kaydet"); }
    }

    // ---------------- İp ve ayarlar ----------------

    public void ToggleLine() => _line?.Toggle();

    public void OpenFolder()
    {
        try
        {
            Directory.CreateDirectory(Settings.ClipFolder);
            Process.Start("explorer.exe", $"\"{Settings.ClipFolder}\"");
        }
        catch (Exception ex) { Fail(ex, "Klasörü aç"); }
    }

    public void OpenSettingsFile()
    {
        try
        {
            Settings.Save();
            Process.Start(new ProcessStartInfo(Settings.FilePath) { UseShellExecute = true });
        }
        catch (Exception ex) { Fail(ex, "Ayarlar dosyası"); }
    }

    public void SetWatchClipboard(bool on)
    {
        Settings.WatchClipboard = on;
        if (_clipboard is not null) _clipboard.Enabled = on;
        Settings.Save();
    }

    public void SetShowOnCapture(bool on)
    {
        Settings.ShowOnCapture = on;
        Settings.Save();
    }

    public void SetHideLineAfterCopy(bool on)
    {
        Settings.HideLineAfterCopy = on;
        Settings.Save();
    }

    public void SetAutostart(bool on)
    {
        try { Autostart.Set(on); }
        catch (Exception ex) { Fail(ex, "Windows ile başlat"); }
    }

    public void ExitApp() => Shutdown();

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _hotkeys?.Dispose();
        _clipboard?.Dispose();
        _msg?.Dispose();
        if (_ownsMutex) _mutex?.ReleaseMutex();
        base.OnExit(e);
    }

    private void Fail(Exception ex, string context)
    {
        Log.Write(ex, context);
        _tray?.Balloon("Mandal: " + context, ex.Message, WF.ToolTipIcon.Error);
    }
}
