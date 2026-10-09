using System.Text.Encodings.Web;
using System.Text.Json;

namespace Mandal.Services;

public sealed class Settings
{
    /// <summary>"auto" = sistem dili; yoksa Loc.Languages'daki kodlardan biri (tr, en, de, ...).</summary>
    public string Language { get; set; } = Loc.Auto;

    public string ClipFolder { get; set; } = DefaultClipFolder;

    /// <summary>Panoya gelen görüntüler otomatik ipe asılsın.</summary>
    public bool WatchClipboard { get; set; } = true;

    /// <summary>Yeni alıntı asıldığında ip kısa süre görünsün.</summary>
    public bool ShowOnCapture { get; set; } = true;
    public double ShowOnCaptureSeconds { get; set; } = 2.5;

    /// <summary>Tıkla-kopyala veya sürükle sonrası ip kendiliğinden kalksın.</summary>
    public bool HideLineAfterCopy { get; set; } = true;

    /// <summary>Kart silerken "emin misin?" sorulsun (varsayılan kapalı).</summary>
    public bool ConfirmDelete { get; set; } = false;

    /// <summary>Fare ipin alanından çıkınca ip hemen yukarı kalksın.</summary>
    public bool HideOnMouseLeave { get; set; } = true;

    /// <summary>Fare sol üst köşeye değince ip açılsın.</summary>
    public bool HotCorner { get; set; } = true;

    /// <summary>Sol üst köşede tıklanabilir küçük mandal dursun.</summary>
    public bool CornerTab { get; set; } = true;

    /// <summary>Önizleme penceresi diğer uygulamaların üstünde kalsın.</summary>
    public bool PreviewTopmost { get; set; } = true;
    public double PreviewWidth { get; set; } = 780;
    public double PreviewHeight { get; set; } = 540;
    /// <summary>-1 = ipin altında ortala.</summary>
    public double PreviewLeft { get; set; } = -1;
    public double PreviewTop { get; set; } = -1;

    public int ThumbnailHeight { get; set; } = 120;

    public string HotkeyRegion { get; set; } = "Ctrl+Shift+S";
    public string HotkeyRegionAlt { get; set; } = "PrintScreen";
    public string HotkeyFullScreen { get; set; } = "Ctrl+Shift+F";
    public string HotkeyWindow { get; set; } = "Ctrl+Shift+W";
    public string HotkeyToggleLine { get; set; } = "Ctrl+Shift+Space";

    /// <summary>Varsayılan: %APPDATA%\Mandal\Clips (içinde gün klasörleri).</summary>
    public static string DefaultClipFolder => Path.Combine(Dir, "Clips");

    public static string Dir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mandal");

    public static string FilePath => Path.Combine(Dir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), JsonOptions);
                if (s is not null)
                {
                    s.Normalize();
                    return s;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Write(ex, "Ayarlar okunamadı, varsayılanlar kullanılıyor");
        }

        var fresh = new Settings();
        fresh.Save();
        return fresh;
    }

    /// <summary>Dosyadan gelen değerleri güvenli aralıklara çeker.</summary>
    public void Normalize()
    {
        Language = string.IsNullOrWhiteSpace(Language) ? Loc.Auto : Language.Trim();
        if (string.IsNullOrWhiteSpace(ClipFolder)) ClipFolder = DefaultClipFolder;
        ThumbnailHeight = Math.Clamp(ThumbnailHeight, 40, 400);
        ShowOnCaptureSeconds = double.IsFinite(ShowOnCaptureSeconds) ? Math.Clamp(ShowOnCaptureSeconds, 0.5, 30) : 2.5;
        PreviewWidth = double.IsFinite(PreviewWidth) ? Math.Clamp(PreviewWidth, 340, 8000) : 780;
        PreviewHeight = double.IsFinite(PreviewHeight) ? Math.Clamp(PreviewHeight, 240, 8000) : 540;
        if (!double.IsFinite(PreviewLeft) || PreviewLeft < -1 || PreviewLeft > 16000) PreviewLeft = -1;
        if (!double.IsFinite(PreviewTop) || PreviewTop < -1 || PreviewTop > 16000) PreviewTop = -1;
        HotkeyRegion = CleanHotkey(HotkeyRegion);
        HotkeyRegionAlt = CleanHotkey(HotkeyRegionAlt);
        HotkeyFullScreen = CleanHotkey(HotkeyFullScreen);
        HotkeyWindow = CleanHotkey(HotkeyWindow);
        HotkeyToggleLine = CleanHotkey(HotkeyToggleLine);
    }

    private static string CleanHotkey(string? s)
    {
        s = (s ?? "").Trim();
        return s.Length > 64 ? "" : s;
    }

    /// <summary>
    /// Alıntı klasörünü oluşturmayı dener. Olmazsa varsayılana döner ve true verir (çağıran kullanıcıyı uyarır).
    /// </summary>
    public bool EnsureClipFolder()
    {
        try
        {
            var full = Path.GetFullPath(ClipFolder);
            Directory.CreateDirectory(full);
            ClipFolder = full;
            return false;
        }
        catch (Exception ex)
        {
            Log.Write(ex, $"Alıntı klasörü kullanılamadı: {ClipFolder}");
            ClipFolder = DefaultClipFolder;
            Directory.CreateDirectory(ClipFolder);
            return true;
        }
    }

    public Settings Clone() => (Settings)MemberwiseClone();

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception ex)
        {
            Log.Write(ex, "Ayarlar kaydedilemedi");
        }
    }
}
