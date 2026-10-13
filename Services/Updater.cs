using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Mandal.Services;

/// <summary>
/// GitHub Releases üzerinden güncelleme. Uygulamanın tek ağ erişimi budur ve yalnızca
/// ayarlardaki "otomatik güncelle" açıkken (veya kullanıcı elle isteyince) çalışır.
/// Akış: /releases/latest → sürüm karşılaştır → dosyayı indir → SHA256SUMS.txt ile doğrula → kur.
/// </summary>
public sealed class Updater
{
    public const string Owner = "ceceys";
    public const string Repo = "mandal";
    private const string LatestApi = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
    private const long MaxDownloadBytes = 200L * 1024 * 1024;

    public sealed record Release(Version Version, string Tag, string? InstallerUrl, string? PortableUrl, string? SumsUrl, string Notes);

    public static Version Current
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
        }
    }

    /// <summary>Kurulum paketiyle mi kuruldu (yanında kaldırıcı var) yoksa taşınabilir exe mi?</summary>
    public static bool IsInstalled =>
        Directory.EnumerateFiles(AppContext.BaseDirectory, "unins*.exe").Any();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Mandal", Current.ToString()));
        c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return c;
    }

    /// <returns>Daha yeni bir sürüm varsa onu; yoksa null.</returns>
    public static async Task<Release?> CheckAsync(CancellationToken ct = default)
    {
        using var http = CreateClient();
        using var doc = JsonDocument.Parse(await http.GetStringAsync(LatestApi, ct));
        var root = doc.RootElement;

        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var ver)) return null;
        ver = new Version(ver.Major, ver.Minor, Math.Max(0, ver.Build));
        if (ver <= Current) return null;

        string? installer = null, portable = null, sums = null;
        if (root.TryGetProperty("assets", out var assets))
        {
            foreach (var a in assets.EnumerateArray())
            {
                var name = a.GetProperty("name").GetString() ?? "";
                var url = a.GetProperty("browser_download_url").GetString();
                if (url is null || !url.StartsWith("https://", StringComparison.Ordinal)) continue;
                if (name.StartsWith("Mandal-Setup-", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) installer = url;
                else if (name.Equals("Mandal.exe", StringComparison.OrdinalIgnoreCase)) portable = url;
                else if (name.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase)) sums = url;
            }
        }
        var notes = await FetchNotesAsync(http, tag, ver, ct)
            ?? PlainText(root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "");
        return new Release(ver, tag, installer, portable, sums, notes);
    }

    /// <summary>
    /// Yeni sürümün notları, uygulamanın dilinde: o sürümün etiketindeki Resources\Notes.{dil}.md dosyasının ilk bölümü
    /// (yoksa İngilizcesi). Böylece GitHub'daki sürüm açıklaması ne olursa olsun kısa ve çevrili notlar gösterilir.
    /// Bulunamazsa null; çağıran sürüm açıklamasına döner.
    /// </summary>
    private static async Task<string?> FetchNotesAsync(HttpClient http, string tag, Version ver, CancellationToken ct)
    {
        foreach (var code in new[] { Loc.Current.Code, "en" }.Distinct())
        {
            try
            {
                var url = $"https://api.github.com/repos/{Owner}/{Repo}/contents/Resources/Notes.{code}.md?ref={Uri.EscapeDataString(tag)}";
                using var doc = JsonDocument.Parse(await http.GetStringAsync(url, ct));
                var b64 = doc.RootElement.GetProperty("content").GetString();
                if (string.IsNullOrEmpty(b64)) continue;
                var text = Encoding.UTF8.GetString(Convert.FromBase64String(b64.Replace("\n", "")));
                var section = FirstSection(text, ver);
                if (!string.IsNullOrWhiteSpace(section)) return section;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.Write($"Sürüm notu alınamadı ({code}): {ex.Message}");
            }
        }
        return null;
    }

    /// <summary>Notes.xx.md biçimi: "1.5.0 — tarih" başlığı, altında "- " maddeleri, bölümler boş satırla ayrılır.</summary>
    private static string FirstSection(string text, Version ver)
    {
        var lines = text.Replace("\r", "").Split('\n');
        int i = 0;
        while (i < lines.Length && string.IsNullOrWhiteSpace(lines[i])) i++;
        if (i >= lines.Length || !lines[i].TrimStart().StartsWith($"{ver.Major}.{ver.Minor}.{ver.Build}", StringComparison.Ordinal))
            return ""; // ilk bölüm bu sürümün değilse gösterme
        var items = new List<string>();
        for (i++; i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]); i++)
        {
            var l = lines[i].Trim();
            items.Add(l.StartsWith("- ", StringComparison.Ordinal) ? "•  " + l[2..] : l);
        }
        return string.Join("\n", items);
    }

    /// <summary>Markdown sürüm açıklamasını düz metne çevirir (yedek yol).</summary>
    public static string PlainText(string markdown)
    {
        var s = markdown.Replace("\r", "");
        s = Regex.Replace(s, @"\[([^\]]+)\]\([^)]+\)", "$1");                    // [metin](adres) → metin
        s = Regex.Replace(s, @"(\*\*|__|`)", "");                                // kalın, kod
        s = Regex.Replace(s, @"^#{1,6}\s*", "", RegexOptions.Multiline);         // başlıklar
        s = Regex.Replace(s, @"^\s*[-*]\s+", "•  ", RegexOptions.Multiline);     // maddeler
        s = Regex.Replace(s, @"\n{3,}", "\n\n");
        s = s.Trim();
        return s.Length <= 4000 ? s : s[..4000] + "…";
    }

    /// <summary>Uygun dosyayı geçici klasöre indirir ve SHA-256 özetini doğrular. Özet dosyası yoksa reddeder.</summary>
    public static async Task<string> DownloadAsync(Release r, CancellationToken ct = default)
    {
        bool installer = IsInstalled && r.InstallerUrl is not null;
        var url = installer ? r.InstallerUrl : (r.PortableUrl ?? r.InstallerUrl);
        if (url is null) throw new InvalidOperationException("Sürümde indirilecek dosya yok");
        if (r.SumsUrl is null) throw new InvalidOperationException("SHA256SUMS.txt yok; doğrulanamayan dosya kurulmaz");

        var fileName = Path.GetFileName(new Uri(url).AbsolutePath);
        var dir = Path.Combine(Path.GetTempPath(), "Mandal-Update");
        Directory.CreateDirectory(dir);
        var target = Path.Combine(dir, fileName);

        using var http = CreateClient();

        // Özetler
        var sumsText = await http.GetStringAsync(r.SumsUrl, ct);
        string? expected = null;
        foreach (var line in sumsText.Split('\n'))
        {
            var parts = line.Trim().Split(new[] { ' ', '*' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[^1].Equals(fileName, StringComparison.OrdinalIgnoreCase)) { expected = parts[0]; break; }
        }
        if (expected is null) throw new InvalidOperationException($"Özet listesinde {fileName} yok");

        // Dosya
        using (var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            resp.EnsureSuccessStatusCode();
            if (resp.Content.Headers.ContentLength is > MaxDownloadBytes) throw new InvalidOperationException("Dosya çok büyük");
            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            await using var dst = File.Create(target);
            var buf = new byte[81920];
            long total = 0;
            int n;
            while ((n = await src.ReadAsync(buf, ct)) > 0)
            {
                total += n;
                if (total > MaxDownloadBytes) throw new InvalidOperationException("Dosya çok büyük");
                await dst.WriteAsync(buf.AsMemory(0, n), ct);
            }
        }

        await using (var fs = File.OpenRead(target))
        {
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct));
            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(target);
                throw new InvalidOperationException("SHA-256 özeti uyuşmuyor; dosya silindi");
            }
        }
        return target;
    }

    /// <summary>İndirilen dosyayı kurar: kurulum paketi sessiz çalışır, taşınabilir exe kendini değiştirir. Uygulama kapanmalıdır.</summary>
    public static void Install(string file)
    {
        if (Path.GetFileName(file).StartsWith("Mandal-Setup-", StringComparison.OrdinalIgnoreCase))
        {
            Process.Start(new ProcessStartInfo(file)
            {
                UseShellExecute = true,
                Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS /NORESTART",
            });
            return;
        }

        // Taşınabilir: çıkışımızı bekle, üzerine yaz, yeniden başlat
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Çalışan dosya yolu yok");
        var pid = Environment.ProcessId;
        var cmd = Path.Combine(Path.GetDirectoryName(file)!, "apply-update.cmd");
        var script = new StringBuilder()
            .AppendLine("@echo off")
            .AppendLine(":wait")
            .AppendLine($"tasklist /FI \"PID eq {pid}\" 2>nul | find \"{pid}\" >nul && (timeout /t 1 /nobreak >nul & goto wait)")
            .AppendLine($"copy /y \"{file}\" \"{exe}\" >nul")
            .AppendLine($"start \"\" \"{exe}\"")
            .AppendLine($"del \"{file}\" >nul 2>&1")
            .AppendLine("del \"%~f0\"")
            .ToString();
        File.WriteAllText(cmd, script, Encoding.Default);
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{cmd}\"") { UseShellExecute = false, CreateNoWindow = true });
    }
}
