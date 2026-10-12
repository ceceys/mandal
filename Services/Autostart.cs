using Microsoft.Win32;

namespace Mandal.Services;

public static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ValueName = "Mandal";

    /// <summary>Kayıt var ve Görev Yöneticisi → Başlangıç'ta devre dışı bırakılmamış.</summary>
    public static bool IsEnabled() => RegisteredCommand() is not null && !DisabledInTaskManager();

    private static string? RegisteredCommand()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        return key?.GetValue(ValueName) as string;
    }

    private static string Command =>
        $"\"{Environment.ProcessPath ?? throw new InvalidOperationException("Çalışan dosya yolu bulunamadı")}\"";

    /// <summary>Görev Yöneticisi devre dışı bırakınca StartupApproved değerinin ilk baytı tek sayı olur (03).</summary>
    private static bool DisabledInTaskManager()
    {
        using var key = Registry.CurrentUser.OpenSubKey(ApprovedKey, false);
        return key?.GetValue(ValueName) is byte[] { Length: > 0 } b && (b[0] & 1) == 1;
    }

    /// <summary>Kullanıcı açıp kapattı. Açarken Görev Yöneticisi'ndeki "devre dışı" işareti de kalkar.</summary>
    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
        if (enabled)
        {
            key.SetValue(ValueName, Command);
            using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, true);
            approved?.DeleteValue(ValueName, false);
        }
        else
        {
            key.DeleteValue(ValueName, false);
        }
    }

    /// <summary>
    /// Açılışta kaydı ayara uydurur: açıksa kayıt bu exe'yi göstersin (exe taşındıysa yol yenilenir),
    /// kapalıysa kayıt kalmasın. Görev Yöneticisi'ndeki seçime dokunmaz.
    /// </summary>
    public static void Sync(bool enabled)
    {
        var current = RegisteredCommand();
        bool inSync = enabled ? string.Equals(current, Command, StringComparison.OrdinalIgnoreCase) : current is null;
        if (inSync) return;

        using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
        if (enabled) key.SetValue(ValueName, Command);
        else key.DeleteValue(ValueName, false);
    }
}
