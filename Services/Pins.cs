using System.Text.Encodings.Web;
using System.Text.Json;

namespace Mandal.Services;

/// <summary>
/// Sabitlenen kartlar: bağıl dosya yolları, ipteki sırayla (soldan sağa).
/// %APPDATA%\Mandal\pins.json içinde saklanır; yeniden açılışta sıra geri gelir.
/// </summary>
public sealed class Pins
{
    public static string FilePath => Path.Combine(Settings.Dir, "pins.json");

    private List<string> _order = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public IReadOnlyList<string> Order => _order;

    public void Load()
    {
        _order = new List<string>();
        try
        {
            if (!File.Exists(FilePath)) return;
            var raw = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(FilePath), JsonOptions);
            if (raw is null) return;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rel in raw)
            {
                if (string.IsNullOrWhiteSpace(rel) || rel.Length > 260) continue;
                if (Path.IsPathRooted(rel) || rel.Contains("..", StringComparison.Ordinal)) continue; // yalnız klasör içi
                if (seen.Add(rel)) _order.Add(rel);
            }
        }
        catch (Exception ex)
        {
            Log.Write(ex, "Sabitlenen kartlar okunamadı");
            _order = new List<string>();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Settings.Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_order, JsonOptions));
        }
        catch (Exception ex)
        {
            Log.Write(ex, "Sabitlenen kartlar kaydedilemedi");
        }
    }

    public void Set(IEnumerable<string> relativePaths) => _order = relativePaths.ToList();
}
