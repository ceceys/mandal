using System.Text.Encodings.Web;
using System.Text.Json;

namespace Mandal.Services;

/// <summary>
/// Öğelere atanan kalıcı kısayollar: bağıl dosya yolu → "Ctrl+Alt+1".
/// %APPDATA%\Mandal\item-hotkeys.json içinde saklanır; yeniden açılışta geri yüklenir.
/// </summary>
public sealed class ItemHotkeys
{
    public static string FilePath => Path.Combine(Settings.Dir, "item-hotkeys.json");

    private Dictionary<string, string> _map = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public IReadOnlyDictionary<string, string> All => _map;

    public void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(FilePath), JsonOptions);
            _map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (raw is null) return;
            foreach (var (rel, gesture) in raw)
            {
                if (string.IsNullOrWhiteSpace(rel) || string.IsNullOrWhiteSpace(gesture)) continue;
                if (rel.Length > 260 || gesture.Length > 64) continue;
                if (Path.IsPathRooted(rel) || rel.Contains("..", StringComparison.Ordinal)) continue; // yalnız klasör içi
                if (!HotkeyManager.TryParse(gesture, out _, out _)) continue;
                _map[rel] = gesture.Trim();
            }
        }
        catch (Exception ex)
        {
            Log.Write(ex, "Öğe kısayolları okunamadı");
            _map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Settings.Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_map, JsonOptions));
        }
        catch (Exception ex)
        {
            Log.Write(ex, "Öğe kısayolları kaydedilemedi");
        }
    }

    public string? Get(string relativePath)
        => _map.TryGetValue(relativePath, out var g) ? g : null;

    public void Set(string relativePath, string gesture) => _map[relativePath] = gesture;

    public bool Remove(string relativePath) => _map.Remove(relativePath);

    /// <summary>Artık var olmayan dosyaların kayıtlarını temizler.</summary>
    public void Prune(Func<string, bool> exists)
    {
        foreach (var rel in _map.Keys.Where(k => !exists(k)).ToList())
            _map.Remove(rel);
    }
}
