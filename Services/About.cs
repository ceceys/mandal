using System.Diagnostics;
using System.Reflection;

namespace Mandal.Services;

/// <summary>Sürüm ve bağlantılar (Ayarlar penceresinin altındaki küçük satır).</summary>
public static class About
{
    public const string GitHubUrl = "https://github.com/ceceys/mandal";

    /// <summary>Boşsa LinkedIn bağlantısı gösterilmez.</summary>
    public const string LinkedInUrl = "https://www.linkedin.com/in/cuma-ali-dirik";

    public static string Version
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v is null ? "" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    /// <summary>Gömülü CHANGELOG.md; başlık satırı atılır, markdown işaretleri sadeleştirilir.</summary>
    public static string ReleaseNotes
    {
        get
        {
            try
            {
                using var s = typeof(About).Assembly.GetManifestResourceStream("Mandal.CHANGELOG.md");
                if (s is null) return "";
                using var r = new StreamReader(s);
                var lines = r.ReadToEnd().Split('\n')
                    .Select(l => l.TrimEnd('\r'))
                    .Where(l => !l.StartsWith("# ", StringComparison.Ordinal))
                    .Select(l => l.StartsWith("## ", StringComparison.Ordinal) ? l[3..] : l.Replace("`", ""));
                return string.Join("\n", lines).Trim();
            }
            catch (Exception ex)
            {
                Log.Write(ex, "Sürüm notları");
                return "";
            }
        }
    }

    /// <summary>Yalnızca sabit https adresleri açılır.</summary>
    public static void Open(string url)
    {
        if (!url.StartsWith("https://", StringComparison.Ordinal)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Write(ex, "Bağlantı açma"); }
    }
}
