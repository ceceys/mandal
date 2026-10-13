using System.Text.Encodings.Web;
using System.Text.Json;

namespace Mandal.Services;

/// <summary>
/// İpten indirilen kartlar: dosyaları klasörde kalır, yalnız ipte görünmez. Mandal hiçbir alıntıyı diskten silmez.
/// Bağıl yollar, indirme işlemlerine göre gruplu: her grup bir "Sil" ya da "İpi boşalt" (son grup geri asılabilir).
/// %APPDATA%\Mandal\hidden.json içinde saklanır.
/// </summary>
public sealed class Hidden
{
    public static string FilePath => Path.Combine(Settings.Dir, "hidden.json");

    private List<List<string>> _batches = new();
    private HashSet<string> _all = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>İpte gösterilmeyecek tüm kartlar.</summary>
    public IReadOnlySet<string> All => _all;

    /// <summary>Son indirme işlemindeki kart sayısı (geri asılabilecek olanlar).</summary>
    public int LastBatchCount => _batches.Count == 0 ? 0 : _batches[^1].Count;

    public void Load()
    {
        _batches = new List<List<string>>();
        _all = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!File.Exists(FilePath)) return;
            var raw = JsonSerializer.Deserialize<List<List<string>>>(File.ReadAllText(FilePath), JsonOptions);
            if (raw is null) return;
            foreach (var batch in raw)
            {
                if (batch is null) continue;
                var clean = new List<string>();
                foreach (var rel in batch)
                {
                    if (string.IsNullOrWhiteSpace(rel) || rel.Length > 260) continue;
                    if (Path.IsPathRooted(rel) || rel.Contains("..", StringComparison.Ordinal)) continue; // yalnız klasör içi
                    if (_all.Add(rel)) clean.Add(rel);
                }
                if (clean.Count > 0) _batches.Add(clean);
            }
        }
        catch (Exception ex)
        {
            Log.Write(ex, "İpten indirilen kartlar okunamadı");
            _batches = new List<List<string>>();
            _all = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Settings.Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_batches, JsonOptions));
        }
        catch (Exception ex)
        {
            Log.Write(ex, "İpten indirilen kartlar kaydedilemedi");
        }
    }

    /// <summary>Bir indirme işlemini kaydeder (tek kart ya da toplu).</summary>
    public void AddBatch(IEnumerable<string> relativePaths)
    {
        var batch = relativePaths.Where(rel => _all.Add(rel)).ToList();
        if (batch.Count > 0) _batches.Add(batch);
    }

    /// <summary>Son indirme işlemini kayıttan çıkarır ve o kartların bağıl yollarını döner.</summary>
    public IReadOnlyList<string> PopLastBatch()
    {
        if (_batches.Count == 0) return Array.Empty<string>();
        var last = _batches[^1];
        _batches.RemoveAt(_batches.Count - 1);
        foreach (var rel in last) _all.Remove(rel);
        return last;
    }

    /// <summary>Dosyası artık olmayan kayıtları düşer (kullanıcı klasörden elle sildiyse).</summary>
    public void Prune(Func<string, bool> exists)
    {
        foreach (var batch in _batches) batch.RemoveAll(rel => !exists(rel));
        _batches.RemoveAll(b => b.Count == 0);
        _all = new HashSet<string>(_batches.SelectMany(b => b), StringComparer.OrdinalIgnoreCase);
    }
}
