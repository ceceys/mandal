using System.Diagnostics;
using System.Reflection;

namespace Mandal.Services;

/// <summary>Sürüm ve bağlantılar (Ayarlar penceresinin altındaki küçük satır).</summary>
public static class About
{
    public const string GitHubUrl = "https://github.com/ceceys/mandal";

    /// <summary>Boşsa LinkedIn bağlantısı gösterilmez.</summary>
    public const string LinkedInUrl = "";

    public static string Version
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v is null ? "" : $"{v.Major}.{v.Minor}.{v.Build}";
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
