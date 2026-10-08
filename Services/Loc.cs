using System.ComponentModel;
using System.Globalization;
using System.Text.Json;

namespace Mandal.Services;

/// <summary>
/// Arayüz metinleri. Diller gömülü kaynak olarak Resources\Strings.{kod}.json dosyalarında.
/// XAML'de: Text="{Binding [Anahtar], Source={x:Static svc:Loc.Current}}"; kodda: Loc.T("Anahtar").
/// Dil değişince PropertyChanged("Item[]") ile tüm bağlar yenilenir.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Current { get; } = new();

    /// <summary>Desteklenen diller: (kod, kendi dilindeki adı).</summary>
    public static readonly IReadOnlyList<(string Code, string Native)> Languages = new[]
    {
        ("tr", "Türkçe"),
        ("en", "English"),
        ("de", "Deutsch"),
        ("fr", "Français"),
        ("es", "Español"),
        ("it", "Italiano"),
        ("pt", "Português"),
        ("ru", "Русский"),
        ("ar", "العربية"),
        ("zh", "中文"),
        ("ja", "日本語"),
        ("ko", "한국어"),
    };

    public const string Auto = "auto";
    private const string Fallback = "en";

    private readonly Dictionary<string, string> _fallback;
    private Dictionary<string, string> _strings;

    public string Code { get; private set; } = Fallback;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? LanguageChanged;

    private Loc()
    {
        _fallback = Load(Fallback);
        _strings = _fallback;
    }

    public string this[string key] => Get(key);

    public static string T(string key) => Current.Get(key);

    public static string F(string key, params object?[] args)
    {
        var format = Current.Get(key);
        try { return string.Format(CultureInfo.CurrentCulture, format, args); }
        catch (FormatException) { return format; }
    }

    /// <summary>"auto" veya bilinmeyen kodu gerçek bir dil koduna çevirir.</summary>
    public static string Resolve(string? setting)
    {
        if (string.IsNullOrWhiteSpace(setting) || setting == Auto)
        {
            var sys = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return Languages.Any(l => l.Code == sys) ? sys : Fallback;
        }
        return Languages.Any(l => l.Code == setting) ? setting : Fallback;
    }

    public void SetLanguage(string? setting)
    {
        var code = Resolve(setting);
        _strings = code == Fallback ? _fallback : Load(code);
        Code = code;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        LanguageChanged?.Invoke();
    }

    private string Get(string key)
    {
        if (_strings.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v)) return v;
        if (_fallback.TryGetValue(key, out v) && !string.IsNullOrEmpty(v)) return v;
        return key;
    }

    private static Dictionary<string, string> Load(string code)
    {
        try
        {
            using var stream = typeof(Loc).Assembly.GetManifestResourceStream($"Mandal.Resources.Strings.{code}.json");
            if (stream is null)
            {
                Log.Write($"Dil dosyası yok: {code}");
                return new Dictionary<string, string>();
            }
            return JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? new Dictionary<string, string>();
        }
        catch (Exception ex)
        {
            Log.Write(ex, $"Dil dosyası okunamadı: {code}");
            return new Dictionary<string, string>();
        }
    }
}
