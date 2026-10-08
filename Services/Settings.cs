using System.Text.Encodings.Web;
using System.Text.Json;

namespace Mandal.Services;

public sealed class Settings
{
    public string ClipFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Mandal");

    /// <summary>Panoya gelen görüntüler otomatik ipe asılsın.</summary>
    public bool WatchClipboard { get; set; } = true;

    /// <summary>Yeni alıntı asıldığında ip kısa süre görünsün.</summary>
    public bool ShowOnCapture { get; set; } = true;
    public double ShowOnCaptureSeconds { get; set; } = 2.5;

    /// <summary>Tıkla-kopyala veya sürükle sonrası ip kendiliğinden kalksın.</summary>
    public bool HideLineAfterCopy { get; set; } = true;

    public int ThumbnailHeight { get; set; } = 120;

    public string HotkeyRegion { get; set; } = "Ctrl+Shift+S";
    public string HotkeyRegionAlt { get; set; } = "PrintScreen";
    public string HotkeyFullScreen { get; set; } = "Ctrl+Shift+F";
    public string HotkeyWindow { get; set; } = "Ctrl+Shift+W";
    public string HotkeyToggleLine { get; set; } = "Ctrl+Shift+Space";

    public static string Dir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mandal");

    public static string FilePath => Path.Combine(Dir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
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
                    if (string.IsNullOrWhiteSpace(s.ClipFolder)) s.ClipFolder = new Settings().ClipFolder;
                    if (s.ThumbnailHeight < 40) s.ThumbnailHeight = 40;
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
