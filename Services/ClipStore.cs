using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using Mandal.Models;

namespace Mandal.Services;

/// <summary>
/// Alıntı klasörü ile ipteki öğeler arasındaki köprü.
/// Dosyalar gün klasörlerinde tutulur: Klasör\yyyy-MM-dd\HH-mm-ss.png (veya .txt).
/// </summary>
public sealed class ClipStore
{
    public ObservableCollection<ClipItem> Items { get; } = new();
    public string Folder { get; private set; }
    private readonly int _thumbHeight;

    public ClipStore(string folder, int thumbHeight)
    {
        Folder = Path.GetFullPath(folder);
        _thumbHeight = thumbHeight;
    }

    public void Load()
    {
        Directory.CreateDirectory(Folder);
        Items.Clear();

        var files = Directory.EnumerateFiles(Folder, "*.*", SearchOption.AllDirectories)
            .Select(f => new FileInfo(f))
            .Where(f => f.Extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                     || f.Extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.LastWriteTime);

        foreach (var f in files)
        {
            if (f.Extension.Equals(".png", StringComparison.OrdinalIgnoreCase) && !Png.FileHasSignature(f.FullName))
            {
                Log.Write($"PNG değil, atlandı: {f.Name}");
                continue;
            }
            try { Items.Add(ClipItem.Load(f.FullName, _thumbHeight)); }
            catch (Exception ex) { Log.Write(ex, $"Yüklenemedi: {f.Name}"); }
        }
    }

    /// <summary>Klasör değişince ipi yeni klasörden doldurur.</summary>
    public void Reload(string folder)
    {
        Folder = Path.GetFullPath(folder);
        Load();
    }

    /// <summary>Klasöre göre bağıl yol (kısayol kayıtları için anahtar).</summary>
    public string RelativePath(ClipItem item) => Path.GetRelativePath(Folder, item.Path);

    public ClipItem? FindByRelativePath(string rel)
    {
        var full = Path.GetFullPath(Path.Combine(Folder, rel));
        return Items.FirstOrDefault(i => string.Equals(i.Path, full, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Yol bizim klasörümüzün içinde ve .png/.txt mi? Dış süreç başlatmadan önce kontrol.</summary>
    public bool IsOwnFile(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var root = Folder.TrimEnd('\\') + "\\";
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                && (full.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                    || full.EndsWith(".txt", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    public ClipItem Add(Bitmap bitmap)
    {
        var path = NewPath(".png");
        bitmap.Save(path, ImageFormat.Png);
        return AddExisting(path);
    }

    public ClipItem AddPng(byte[] png)
    {
        if (!Png.HasSignature(png)) throw new InvalidDataException("Geçersiz PNG verisi");
        var path = NewPath(".png");
        File.WriteAllBytes(path, png);
        return AddExistingOrDelete(path);
    }

    public ClipItem AddText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Boş metin", nameof(text));
        if (Encoding.UTF8.GetByteCount(text) > ClipItem.MaxTextBytes) throw new InvalidDataException("Metin çok uzun");
        var path = NewPath(".txt");
        File.WriteAllText(path, text, new UTF8Encoding(false));
        return AddExistingOrDelete(path);
    }

    /// <summary>Metin kartının içeriğini değiştirir; dosya aynı kalır, öğe yerinde yenilenir.</summary>
    public ClipItem UpdateText(ClipItem item, string text)
    {
        if (!item.IsText) throw new InvalidOperationException("Görsel kart düzenlenemez");
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Boş metin", nameof(text));
        if (Encoding.UTF8.GetByteCount(text) > ClipItem.MaxTextBytes) throw new InvalidDataException("Metin çok uzun");

        File.WriteAllText(item.Path, text, new UTF8Encoding(false));
        var fresh = ClipItem.Load(item.Path, _thumbHeight);
        fresh.Hotkey = item.Hotkey;
        int i = Items.IndexOf(item);
        if (i >= 0)
        {
            fresh.IsPinned = item.IsPinned;
            Items[i] = fresh;
        }
        else
        {
            Items.Insert(PinnedCount, fresh);
        }
        return fresh;
    }

    // ---------------- Sabitlenen kartlar: hep ipin başında ----------------

    /// <summary>İpin başındaki sabit kartların sayısı. Sabitler hep önde durur, yeni alıntılar onların arkasına asılır.</summary>
    public int PinnedCount
    {
        get
        {
            int n = 0;
            while (n < Items.Count && Items[n].IsPinned) n++;
            return n;
        }
    }

    /// <summary>Yüklemeden sonra kayıtlı sabitleri verilen sırayla öne dizer; dosyası olmayan kayıtlar atlanır.</summary>
    public void ApplyPins(IEnumerable<string> relativePaths)
    {
        int n = PinnedCount;
        foreach (var rel in relativePaths)
        {
            var item = FindByRelativePath(rel);
            if (item is null || item.IsPinned) continue;
            item.IsPinned = true;
            int from = Items.IndexOf(item);
            if (from != n) Items.Move(from, n);
            n++;
        }
    }

    /// <summary>
    /// Sabitler: kart sabitlerin sonuna geçer. Sabitlemeyi kaldır: kart zamanına göre yerine döner (yeni önde).
    /// </summary>
    public void SetPinned(ClipItem item, bool pinned)
    {
        int from = Items.IndexOf(item);
        if (from < 0 || item.IsPinned == pinned) return;

        if (pinned)
        {
            int end = PinnedCount;
            if (from != end) Items.Move(from, end);
            item.IsPinned = true;
            return;
        }

        // Kendinden eski ilk sabit olmayan kartın önüne; yoksa en sona
        int to = Items.Count - 1;
        for (int i = PinnedCount; i < Items.Count; i++)
        {
            if (Items[i].Time < item.Time) { to = i - 1; break; }
        }
        item.IsPinned = false;
        if (to != from) Items.Move(from, to);
    }

    /// <summary>Sabit kartı sabitler içinde başka sıraya taşır (sürükleyerek sıralama).</summary>
    public void MovePinned(ClipItem item, int index)
    {
        int from = Items.IndexOf(item);
        if (from < 0 || !item.IsPinned) return;
        int to = Math.Clamp(index, 0, PinnedCount - 1);
        if (to != from) Items.Move(from, to);
    }

    private ClipItem AddExistingOrDelete(string path)
    {
        try
        {
            return AddExisting(path);
        }
        catch
        {
            try { File.Delete(path); } catch { /* yoksay */ }
            throw;
        }
    }

    private ClipItem AddExisting(string path)
    {
        var item = ClipItem.Load(path, _thumbHeight);
        Items.Insert(PinnedCount, item);
        return item;
    }

    public void Delete(ClipItem item)
    {
        Items.Remove(item);
        try
        {
            if (File.Exists(item.Path)) File.Delete(item.Path);
            // Gün klasörü boşaldıysa kaldır
            var dir = Path.GetDirectoryName(item.Path);
            if (dir is not null
                && !string.Equals(Path.GetFullPath(dir), Folder, StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(dir)
                && !Directory.EnumerateFileSystemEntries(dir).Any())
                Directory.Delete(dir);
        }
        catch (Exception ex)
        {
            Log.Write(ex, $"Silinemedi: {item.FileName}");
            throw;
        }
    }

    private string NewPath(string ext)
    {
        var now = DateTime.Now;
        var dayDir = Path.Combine(Folder, now.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(dayDir);
        var stamp = now.ToString("HH-mm-ss");
        var path = Path.Combine(dayDir, stamp + ext);
        int i = 2;
        while (File.Exists(path))
            path = Path.Combine(dayDir, $"{stamp}_{i++}{ext}");
        return path;
    }
}
