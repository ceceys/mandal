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
    public ItemHotkeys ItemHotkeys { get; } = new();

    /// <summary>Ayarlar penceresinden yeni ayarlar uygulandığında.</summary>
    public event Action? SettingsChanged;

    private LineWindow? _line;
    private TrayIcon? _tray;
    private MessageWindow? _msg;
    private HotkeyManager? _hotkeys;
    private ClipboardWatcher? _clipboard;
    private SettingsWindow? _settingsWindow;
    private PreviewWindow? _preview;
    private CornerTabWindow? _cornerTab;
    private readonly System.Windows.Threading.DispatcherTimer _cornerTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private readonly System.Windows.Threading.DispatcherTimer _cleanupTimer = new() { Interval = TimeSpan.FromHours(6) };
    private readonly System.Windows.Threading.DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromHours(24) };
    private bool _updateBusy;
    private string? _readyUpdateFile;
    private Version? _readyUpdateVersion;
    private string _readyUpdateNotes = "";
    private int _cornerTicks;

    private Mutex? _mutex;
    private bool _ownsMutex;
    private EventWaitHandle? _showEvent;
    private bool _capturing;
    private bool _folderFallback;

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
            _tray?.Balloon("Mandal", Loc.T("Msg_Error"), WF.ToolTipIcon.Warning);
            ex.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            Log.Write(ex.ExceptionObject as Exception ?? new Exception(ex.ExceptionObject?.ToString()), "Ölümcül hata");

        Settings = Settings.Load();
        Loc.Current.SetLanguage(Settings.Language);
        _folderFallback = Settings.EnsureClipFolder();

        Store = new ClipStore(Settings.ClipFolder, Settings.ThumbnailHeight);
        try { Store.Load(); }
        catch (Exception ex) { Log.Write(ex, "Alıntı klasörü yüklenemedi"); }

        ItemHotkeys.Load();
        ApplyItemHotkeysToItems();

        _msg = new MessageWindow();
        _hotkeys = new HotkeyManager(_msg);
        _clipboard = new ClipboardWatcher(_msg, Settings.ClipFolder);
        _clipboard.ImageArrived += OnClipboardImage;
        _clipboard.Enabled = Settings.WatchClipboard;

        _line = new LineWindow(Store);
        _line.ShownChanged += shown => { if (_cornerTab is { IsLoaded: true }) _cornerTab.Visibility = shown ? Visibility.Hidden : Visibility.Visible; };
        _tray = new TrayIcon(this);

        RegisterHotkeys();
        StartShowListener();
        UpdateCornerTab();
        _cornerTimer.Tick += CornerTimer_Tick;
        _cornerTimer.Start();

        RunCleanup();
        _cleanupTimer.Tick += (_, _) => RunCleanup();
        _cleanupTimer.Start();

        // Güncelleme: açılıştan 20 sn sonra ve günde bir; ayar kapalıysa hiç ağa çıkılmaz
        _updateTimer.Tick += (_, _) => _ = CheckForUpdatesAsync(manual: false);
        _updateTimer.Start();
        var first = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        first.Tick += (_, _) => { first.Stop(); _ = CheckForUpdatesAsync(manual: false); };
        first.Start();

        if (_folderFallback)
            _tray.Balloon("Mandal", Loc.T("Msg_FolderFallback"), WF.ToolTipIcon.Warning);
    }

    // ---------------- Kısayollar ----------------

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

        // Öğelere atanmış kalıcı kısayollar: basınca o öğe panoya kopyalanır
        foreach (var item in Store.Items.Where(i => i.HasHotkey).ToList())
        {
            var captured = item;
            Reg(captured.Hotkey!, () => CopyItem(captured, quiet: true));
        }

        if (failures.Count > 0)
            _tray?.Balloon(Loc.T("Msg_HotkeysFailed"), string.Join("\n", failures), WF.ToolTipIcon.Warning);
    }

    private void ApplyHotkeys()
    {
        _hotkeys?.UnregisterAll();
        if (!_hotkeysSuspended) RegisterHotkeys();
    }

    private bool _hotkeysSuspended;

    /// <summary>
    /// Bir kısayol kutusu odaktayken sistem geneli kısayollar askıya alınır: kayıtlı bir kombinasyona basılınca
    /// Windows tuşu uygulamaya ulaştırmaz, kutu boş kalırdı. Odak çıkınca <see cref="ResumeHotkeys"/> geri kurar.
    /// </summary>
    public void SuspendHotkeys()
    {
        if (_hotkeysSuspended) return;
        _hotkeysSuspended = true;
        _hotkeys?.UnregisterAll();
    }

    public void ResumeHotkeys()
    {
        if (!_hotkeysSuspended) return;
        _hotkeysSuspended = false;
        ApplyHotkeys();
    }

    /// <summary>Kayıtlı kısayolları ipteki öğelere yazar; dosyası silinmişleri temizler.</summary>
    private void ApplyItemHotkeysToItems()
    {
        ItemHotkeys.Prune(rel => Store.FindByRelativePath(rel) is not null);
        foreach (var item in Store.Items)
            item.Hotkey = ItemHotkeys.Get(Store.RelativePath(item));
        ItemHotkeys.Save();
    }

    /// <summary>Öğeye kısayol atar (null = kaldır). Çakışma varsa false.</summary>
    public bool AssignItemHotkey(ClipItem item, string? gesture)
    {
        var rel = Store.RelativePath(item);

        if (string.IsNullOrWhiteSpace(gesture))
        {
            ItemHotkeys.Remove(rel);
            item.Hotkey = null;
            ItemHotkeys.Save();
            ApplyHotkeys();
            return true;
        }

        if (!HotkeyManager.TryParse(gesture, out var mods, out var vk)) return false;

        bool SameAs(string? other) =>
            !string.IsNullOrWhiteSpace(other) &&
            HotkeyManager.TryParse(other, out var m2, out var v2) && m2 == mods && v2 == vk;

        if (SameAs(Settings.HotkeyRegion) || SameAs(Settings.HotkeyRegionAlt) || SameAs(Settings.HotkeyFullScreen)
            || SameAs(Settings.HotkeyWindow) || SameAs(Settings.HotkeyToggleLine))
            return false;
        if (Store.Items.Any(i => !ReferenceEquals(i, item) && SameAs(i.Hotkey)))
            return false;

        ItemHotkeys.Set(rel, gesture);
        item.Hotkey = gesture;
        ItemHotkeys.Save();
        ApplyHotkeys();
        return true;
    }

    // ---------------- Güncelleme ----------------

    /// <param name="manual">Tepsi menüsünden istendi: sonuç ne olursa olsun bildir.</param>
    public async Task CheckForUpdatesAsync(bool manual)
    {
        if (_updateBusy) return;
        if (!manual && !Settings.AutoUpdate) return;
        _updateBusy = true;
        try
        {
            // Daha önce indirilip bekletilen dosya varsa yeniden sor
            if (_readyUpdateFile is not null && File.Exists(_readyUpdateFile))
            {
                OfferInstall();
                return;
            }

            var rel = await Updater.CheckAsync();
            if (rel is null)
            {
                if (manual) _tray?.Balloon("Mandal", Loc.F("Msg_UpToDate", Updater.Current), WF.ToolTipIcon.Info);
                return;
            }

            Log.Write($"Güncelleme bulundu: {rel.Tag}");
            var file = await Updater.DownloadAsync(rel);
            _readyUpdateFile = file;
            _readyUpdateVersion = rel.Version;
            _readyUpdateNotes = rel.Notes;
            OfferInstall();
        }
        catch (Exception ex)
        {
            Log.Write(ex, "Güncelleme");
            if (manual) _tray?.Balloon("Mandal", Loc.F("Msg_UpdateFailed", ex.Message), WF.ToolTipIcon.Warning);
        }
        finally
        {
            _updateBusy = false;
        }
    }

    private void OfferInstall()
    {
        if (_readyUpdateFile is null || _readyUpdateVersion is null) return;
        if (_line is not null) _line.PopupDepth++;
        bool yes;
        try
        {
            var msg = Loc.F("Msg_UpdateReady", _readyUpdateVersion);
            if (!string.IsNullOrWhiteSpace(_readyUpdateNotes))
            {
                var notes = _readyUpdateNotes.Replace("\r", "");
                if (notes.Length > 420) notes = notes[..420] + "…";
                msg += "\n\n" + notes;
            }
            yes = ConfirmWindow.Ask(msg, dangerous: false);
        }
        finally { if (_line is not null) _line.PopupDepth = Math.Max(0, _line.PopupDepth - 1); }
        if (!yes) return;

        try
        {
            Updater.Install(_readyUpdateFile);
            Shutdown();
        }
        catch (Exception ex) { Fail(ex, Loc.T("Menu_CheckUpdates")); }
    }

    // ---------------- Saklama ----------------

    /// <summary>
    /// Varsayılan: hiçbir şey kendiliğinden silinmez (KeepForever). Kullanıcı kapatırsa
    /// AutoDeleteDays'den eski kartlar silinir; kısayol atanmış kartlar her zaman kalır.
    /// </summary>
    private void RunCleanup()
    {
        if (Settings.KeepForever) return;
        try
        {
            var limit = DateTime.Now.AddDays(-Settings.AutoDeleteDays);
            var old = Store.Items.Where(i => i.Time < limit && !i.HasHotkey).ToList();
            foreach (var item in old)
            {
                try { Store.Delete(item); }
                catch (Exception ex) { Log.Write(ex, "Otomatik temizlik"); }
            }
            if (old.Count > 0) Log.Write($"Otomatik temizlik: {old.Count} kart silindi ({Settings.AutoDeleteDays} günden eski)");
        }
        catch (Exception ex) { Log.Write(ex, "Otomatik temizlik"); }
    }

    // ---------------- Sol üst köşe ----------------

    /// <summary>Fare sol üst köşeye ~360 ms değerse ipi aç.</summary>
    private void CornerTimer_Tick(object? sender, EventArgs e)
    {
        if (!Settings.HotCorner || _line is null || _line.IsShown || _capturing)
        {
            _cornerTicks = 0;
            return;
        }
        if (NativeMethods.GetCursorPos(out var p) && IsInHotCorner(p))
        {
            if (++_cornerTicks >= 3)
            {
                _cornerTicks = 0;
                _line.ShowAnimated();
            }
        }
        else
        {
            _cornerTicks = 0;
        }
    }

    /// <summary>Fare, ayarlardaki köşenin 2 piksellik ucunda mı? (fizik piksel, birincil ekran)</summary>
    private bool IsInHotCorner(NativeMethods.POINT p)
    {
        var b = WF.Screen.PrimaryScreen?.Bounds ?? new Drawing.Rectangle(0, 0, 1920, 1080);
        bool right = Settings.CornerPosition is "TopRight" or "BottomRight";
        bool bottom = Settings.CornerPosition is "BottomLeft" or "BottomRight";
        bool xOk = right ? p.X >= b.Right - 2 : p.X <= b.Left + 1;
        bool yOk = bottom ? p.Y >= b.Bottom - 2 : p.Y <= b.Top + 1;
        return xOk && yOk;
    }

    private void UpdateCornerTab()
    {
        if (Settings.CornerTab)
        {
            if (_cornerTab is { IsLoaded: false }) _cornerTab = null; // dışarıdan kapanmışsa yeniden kurulur
            if (_cornerTab is null)
            {
                var tab = new CornerTabWindow();
                tab.Closed += (_, _) =>
                {
                    if (!ReferenceEquals(_cornerTab, tab)) return;
                    Log.Write("Köşe mandalı penceresi beklenmedik biçimde kapandı; ip gösterilince yeniden kurulacak");
                    _cornerTab = null;
                };
                _cornerTab = tab;
            }
            _cornerTab.ApplyCorner(Settings.CornerPosition);
            if (!_cornerTab.IsVisible) _cornerTab.Show();
            _cornerTab.Visibility = _line is { IsShown: true } ? Visibility.Hidden : Visibility.Visible;
        }
        else if (_cornerTab is not null)
        {
            var tab = _cornerTab;
            _cornerTab = null;
            tab.Close();
        }
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
        catch (Exception ex) { Fail(ex, Loc.T("Ctx_Region")); }
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
        catch (Exception ex) { Fail(ex, Loc.T("Ctx_FullScreen")); }
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
                _tray?.Balloon("Mandal", Loc.T("Msg_NoActiveWindow"), WF.ToolTipIcon.Warning);
                return;
            }
            await HideLineForCaptureAsync();
            using var bmp = ScreenCapture.Capture(bounds.Value);
            Hang(Store.Add(bmp));
        }
        catch (Exception ex) { Fail(ex, Loc.T("Ctx_Window")); }
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
        catch (Exception ex) { Fail(ex, Loc.T("Ctx_Clipboard")); }
    }

    /// <summary>Görseli OCR ile metne çevirir, metni ipe asar ve panoya kopyalar.</summary>
    public async Task<bool> OcrItemAsync(ClipItem item)
    {
        if (!item.IsImage) return false;
        try
        {
            var text = await Ocr.RecognizeAsync(item.Path);
            if (text is null)
            {
                _tray?.Balloon("Mandal", Loc.T("Msg_OcrNoEngine"), WF.ToolTipIcon.Warning);
                return false;
            }
            if (string.IsNullOrWhiteSpace(text))
            {
                _tray?.Balloon("Mandal", Loc.T("Msg_OcrNoText"), WF.ToolTipIcon.Info);
                return false;
            }
            var textItem = Store.AddText(text);
            CopyItem(textItem, quiet: true);
            return true;
        }
        catch (Exception ex)
        {
            Fail(ex, Loc.T("Ctx_Ocr"));
            return false;
        }
    }

    // ---------------- Öğe işlemleri ----------------

    /// <param name="quiet">true: ip gizlenmez (kısayolla veya OCR sonrası kopyalama)</param>
    public void CopyItem(ClipItem item, bool quiet = false)
    {
        try
        {
            var data = new DataObject();
            data.SetFileDropList(new StringCollection { item.Path });
            if (item.IsImage)
            {
                data.SetImage(item.LoadFull());
                data.SetData("PNG", new MemoryStream(File.ReadAllBytes(item.Path)), false);
            }
            else
            {
                data.SetText(item.Text ?? "");
            }

            _clipboard?.SuppressFor(TimeSpan.FromSeconds(2));
            Clipboard.SetDataObject(data, true);

            if (!quiet && Settings.HideLineAfterCopy) _line?.HideAnimated(TimeSpan.FromMilliseconds(650));
        }
        catch (Exception ex) { Fail(ex, Loc.T("Item_Copy")); }
    }

    /// <summary>Düz metni panoya koyar; pano izleyici bizim kopyamızı yok sayar.</summary>
    public void SetClipboardText(string text)
    {
        _clipboard?.SuppressFor(TimeSpan.FromSeconds(2));
        Clipboard.SetText(text);
    }

    private DragGhostWindow? _ghost;

    /// <summary>
    /// Sürükle-bırak: ip yukarı çekilir, imlecin yanında küçük bir kart gider; bırakma iptal olursa ip geri iner.
    /// </summary>
    public void DragItem(DependencyObject source, ClipItem item)
    {
        GiveFeedbackEventHandler? feedback = null;
        try
        {
            var data = new DataObject();
            data.SetFileDropList(new StringCollection { item.Path });
            if (item.IsImage) data.SetImage(item.LoadFull());
            else data.SetText(item.Text ?? "");

            bool wasShown = _line is { IsShown: true };
            if (_line is not null) { _line.IsDragging = true; _line.HideAnimated(); }

            _ghost ??= new DragGhostWindow();
            _ghost.ShowFor(item);
            feedback = (_, e) => _ghost?.Follow();
            DragDrop.AddGiveFeedbackHandler(source, feedback);

            var result = DragDrop.DoDragDrop(source, data, DragDropEffects.Copy);

            DragDrop.RemoveGiveFeedbackHandler(source, feedback);
            feedback = null;
            _ghost.Hide();
            if (_line is not null) _line.IsDragging = false;

            // Hiçbir yere bırakılmadıysa ip geri insin; bırakıldıysa kalksın
            if (result == DragDropEffects.None && wasShown) _line?.ShowAnimated();
        }
        catch (Exception ex)
        {
            if (feedback is not null) DragDrop.RemoveGiveFeedbackHandler(source, feedback);
            _ghost?.Hide();
            if (_line is not null) _line.IsDragging = false;
            Fail(ex, Loc.T("Ctx_Drag"));
        }
    }

    /// <summary>Not kartı: yeni not (item null) veya mevcut metin kartını düzenleme.</summary>
    public void OpenNoteEditor(ClipItem? item)
    {
        if (item is { IsText: false }) return;
        if (_line is not null) _line.PopupDepth++;
        try
        {
            var draftPath = Path.Combine(Settings.Dir, "note-draft.txt");
            string? draft = item is null && File.Exists(draftPath) ? File.ReadAllText(draftPath) : null;

            var w = new NoteWindow(item, draft);
            w.ShowDialog();

            if (w.SavedText is not { } text)
            {
                // İptal: yeni not yazılmışsa taslak olarak sakla; hiçbir şey kaybolmaz
                if (item is null)
                {
                    if (!string.IsNullOrWhiteSpace(w.DraftText)) File.WriteAllText(draftPath, w.DraftText);
                    else if (File.Exists(draftPath)) File.Delete(draftPath);
                }
                return;
            }

            ClipItem saved = item is null ? Store.AddText(text) : Store.UpdateText(item, text);
            if (item is null && File.Exists(draftPath)) File.Delete(draftPath);
            if (item is not null && _preview is { Item: { } p } && ReferenceEquals(p, item)) _preview.ShowItem(saved);
            if (item is null) Hang(saved);
        }
        catch (Exception ex) { Fail(ex, Loc.T("Note_Title")); }
        finally
        {
            if (_line is not null) _line.PopupDepth = Math.Max(0, _line.PopupDepth - 1);
        }
    }

    /// <summary>Kart üzerinde yerinde düzenlenen notu kaydeder.</summary>
    public void UpdateNote(ClipItem item, string text)
    {
        try
        {
            var saved = Store.UpdateText(item, text);
            if (_preview is { Item: { } p } && ReferenceEquals(p, item)) _preview.ShowItem(saved);
        }
        catch (Exception ex) { Fail(ex, Loc.T("Item_Edit")); }
    }

    /// <summary>Büyük önizleme penceresi (tek pencere, öğe değişince yeniden kullanılır).</summary>
    public void OpenPreview(ClipItem item)
    {
        try
        {
            if (_preview is null || !_preview.IsLoaded)
            {
                _preview = new PreviewWindow();
                _preview.Closed += (_, _) => _preview = null;
            }
            _preview.ShowItem(item);
        }
        catch (Exception ex) { Fail(ex, Loc.T("Item_Preview")); }
    }

    /// <summary>Evet/Hayır sorusu; açıkken ip fare çıkışıyla kalkmaz.</summary>
    private bool Confirm(string text)
    {
        if (_line is not null) _line.PopupDepth++;
        try
        {
            return ConfirmWindow.Ask(text, dangerous: true);
        }
        finally
        {
            if (_line is not null) _line.PopupDepth = Math.Max(0, _line.PopupDepth - 1);
        }
    }

    /// <summary>İpteki tüm kartları ve dosyalarını siler; her zaman onay ister.</summary>
    public void DeleteAll()
    {
        try
        {
            if (Store.Items.Count == 0) return;
            if (!Confirm(Loc.T("Msg_DeleteAllConfirm"))) return;

            _preview?.Close();
            foreach (var item in Store.Items.ToList())
            {
                try { Store.Delete(item); }
                catch (Exception ex) { Log.Write(ex, "Tümünü sil"); }
            }
            foreach (var rel in ItemHotkeys.All.Keys.ToList()) ItemHotkeys.Remove(rel);
            ItemHotkeys.Save();
            ApplyHotkeys();
        }
        catch (Exception ex) { Fail(ex, Loc.T("Card_DeleteAll")); }
    }

    public void DeleteItem(ClipItem item)
    {
        try
        {
            if (Settings.ConfirmDelete && !Confirm(Loc.T("Msg_DeleteConfirm"))) return;
            if (_preview is { Item: { } previewed } && ReferenceEquals(previewed, item)) _preview.Close();
            var rel = Store.RelativePath(item);
            Store.Delete(item);
            if (ItemHotkeys.Remove(rel))
            {
                ItemHotkeys.Save();
                ApplyHotkeys();
            }
        }
        catch (Exception ex) { Fail(ex, Loc.T("Item_Delete")); }
    }

    public void OpenItem(ClipItem item)
    {
        try
        {
            if (!Store.IsOwnFile(item.Path)) throw new InvalidOperationException("Beklenmeyen dosya yolu");
            Process.Start(new ProcessStartInfo(item.Path) { UseShellExecute = true });
        }
        catch (Exception ex) { Fail(ex, Loc.T("Item_Open")); }
    }

    public void ShowInFolder(ClipItem item)
    {
        try
        {
            if (!Store.IsOwnFile(item.Path)) throw new InvalidOperationException("Beklenmeyen dosya yolu");
            // Windows dosya adlarında çift tırnak olamaz; yol doğrulandı.
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{item.Path}\"") { UseShellExecute = false });
        }
        catch (Exception ex) { Fail(ex, Loc.T("Item_ShowInFolder")); }
    }

    public void SaveItemAs(ClipItem item)
    {
        try
        {
            bool img = item.IsImage;
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = Loc.T("Dialog_SaveTitle"),
                Filter = img ? Loc.T("Dialog_PngFilter") + "|*.png" : Loc.T("Dialog_TxtFilter") + "|*.txt",
                FileName = item.FileName,
                DefaultExt = img ? ".png" : ".txt",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            };
            if (dlg.ShowDialog() == true) File.Copy(item.Path, dlg.FileName, true);
        }
        catch (Exception ex) { Fail(ex, Loc.T("Item_SaveAs")); }
    }

    // ---------------- İp ve ayarlar ----------------

    public void ToggleLine() => _line?.Toggle();

    public void HideLine() => _line?.HideAnimated();

    public void OpenSettings()
    {
        if (_settingsWindow is { IsLoaded: true })
        {
            _settingsWindow.Activate();
            return;
        }
        _settingsWindow = new SettingsWindow();
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    /// <summary>Ayarlar penceresinden gelen yeni ayarları uygular ve kaydeder.</summary>
    public void ApplySettings(Settings fresh)
    {
        fresh.Normalize();
        bool folderChanged = !string.Equals(
            Path.GetFullPath(fresh.ClipFolder), Store.Folder, StringComparison.OrdinalIgnoreCase);

        Settings = fresh;
        if (Settings.EnsureClipFolder())
            _tray?.Balloon("Mandal", Loc.T("Msg_FolderFallback"), WF.ToolTipIcon.Warning);
        Settings.Save();

        Loc.Current.SetLanguage(Settings.Language);

        if (_clipboard is not null)
        {
            _clipboard.OwnFolder = Settings.ClipFolder;
            _clipboard.Enabled = Settings.WatchClipboard;
        }

        if (folderChanged)
        {
            try
            {
                Store.Reload(Settings.ClipFolder);
                ApplyItemHotkeysToItems();
            }
            catch (Exception ex) { Fail(ex, Loc.T("Settings_Folder")); }
        }

        ApplyHotkeys();
        UpdateCornerTab();
        RunCleanup();
        _line?.RefreshTexts();
        _tray?.Rebuild();
        SettingsChanged?.Invoke();
    }

    public void OpenFolder()
    {
        try
        {
            Directory.CreateDirectory(Settings.ClipFolder);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{Settings.ClipFolder}\"") { UseShellExecute = false });
        }
        catch (Exception ex) { Fail(ex, Loc.T("Ctx_OpenFolder")); }
    }

    public void OpenSettingsFile()
    {
        try
        {
            Settings.Save();
            Process.Start(new ProcessStartInfo(Settings.FilePath) { UseShellExecute = true });
        }
        catch (Exception ex) { Fail(ex, Loc.T("Ctx_SettingsFile")); }
    }

    public void SetLanguage(string code)
    {
        Settings.Language = code;
        Settings.Save();
        Loc.Current.SetLanguage(code);
        _line?.RefreshTexts();
        _tray?.Rebuild();
    }

    public void SetWatchClipboard(bool on)
    {
        Settings.WatchClipboard = on;
        if (_clipboard is not null) _clipboard.Enabled = on;
        Settings.Save();
    }

    /// <summary>Köşedeki mandalı açar/kapatır (tepsi menüsü, mandala sağ tık, Ayarlar).</summary>
    public void SetCornerTab(bool on)
    {
        Settings.CornerTab = on;
        Settings.Save();
        UpdateCornerTab();
        SettingsChanged?.Invoke();
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
        catch (Exception ex) { Fail(ex, Loc.T("Menu_Autostart")); }
    }

    public void ExitApp() => Shutdown();

    protected override void OnExit(ExitEventArgs e)
    {
        _cornerTimer.Stop();
        _ghost?.Close();
        _preview?.Close();
        _settingsWindow?.Close();
        _cornerTab?.Close();
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
