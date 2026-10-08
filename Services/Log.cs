namespace Mandal.Services;

internal static class Log
{
    private static readonly object Gate = new();
    public static string FilePath => Path.Combine(Settings.Dir, "mandal.log");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Settings.Dir);
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // günlük yazılamıyorsa sessizce geç
        }
    }

    public static void Write(Exception ex, string? context = null)
        => Write((context is null ? "" : context + ": ") + ex);
}
