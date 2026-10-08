using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Media.Imaging;
using Mandal.Services;

namespace Mandal.Models;

public enum ClipKind { Image, Text }

/// <summary>İpe asılı tek bir öğe: diskteki PNG (görsel) veya TXT (metin).</summary>
public sealed class ClipItem : INotifyPropertyChanged
{
    public const int MaxTextBytes = 1024 * 1024;
    private const int PreviewChars = 400;

    public string Path { get; }
    public DateTime Time { get; }
    public ClipKind Kind { get; }
    public int Width { get; }
    public int Height { get; }
    public BitmapSource? Thumb { get; }
    public string? Text { get; }

    /// <summary>İpte hafif eğik dursun diye derece cinsinden sabit küçük açı (-3..+3).</summary>
    public double Tilt { get; }

    private string? _hotkey;

    /// <summary>Bu öğeyi panoya kopyalayan kalıcı kısayol (varsa).</summary>
    public string? Hotkey
    {
        get => _hotkey;
        set
        {
            if (_hotkey == value) return;
            _hotkey = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasHotkey));
        }
    }

    public bool HasHotkey => !string.IsNullOrEmpty(_hotkey);
    public bool IsImage => Kind == ClipKind.Image;
    public bool IsText => Kind == ClipKind.Text;
    public string FileName => System.IO.Path.GetFileName(Path);

    public string Preview
    {
        get
        {
            if (Text is null) return "";
            var t = Text.Trim();
            return t.Length <= PreviewChars ? t : t[..PreviewChars] + "…";
        }
    }

    /// <summary>Zaman, boyut ve dosya adı; ipucu metninin dilden bağımsız kısmı.</summary>
    public string Summary => IsImage
        ? $"{Time:G}  ·  {Width}×{Height} px\n{FileName}"
        : $"{Time:G}  ·  {Loc.F("Item_TextChars", Text?.Length ?? 0)}\n{FileName}";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private ClipItem(string path, DateTime time, ClipKind kind, int width, int height, BitmapSource? thumb, string? text)
    {
        Path = path;
        Time = time;
        Kind = kind;
        Width = width;
        Height = height;
        Thumb = thumb;
        Text = text;
        Tilt = TiltFor(System.IO.Path.GetFileName(path));
    }

    public static ClipItem Load(string path, int thumbHeightDip, int thumbMaxWidthDip = 260)
    {
        var ext = System.IO.Path.GetExtension(path);
        if (ext.Equals(".txt", StringComparison.OrdinalIgnoreCase)) return LoadText(path);
        if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase)) return LoadImage(path, thumbHeightDip, thumbMaxWidthDip);
        throw new NotSupportedException($"Desteklenmeyen dosya: {ext}");
    }

    private static ClipItem LoadText(string path)
    {
        var info = new FileInfo(path);
        if (info.Length > MaxTextBytes) throw new InvalidDataException("Metin dosyası çok büyük");
        var text = File.ReadAllText(path, Encoding.UTF8);
        return new ClipItem(path, info.LastWriteTime, ClipKind.Text, 0, 0, null, text);
    }

    private static ClipItem LoadImage(string path, int thumbHeightDip, int thumbMaxWidthDip)
    {
        using var fs = File.OpenRead(path);
        var frame = BitmapFrame.Create(fs, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
        int w = frame.PixelWidth, h = frame.PixelHeight;

        // Küçük resmi 2x çözünürlükte çöz: yüksek DPI'da keskin kalır, bellek yine düşük.
        int targetH = thumbHeightDip * 2, targetW = thumbMaxWidthDip * 2;
        fs.Position = 0;
        var bi = new BitmapImage();
        bi.BeginInit();
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        bi.StreamSource = fs;
        if (w > 0 && h > 0)
        {
            double scale = Math.Min((double)targetH / h, (double)targetW / w);
            if (scale < 1)
            {
                if ((double)targetH / h <= (double)targetW / w) bi.DecodePixelHeight = targetH;
                else bi.DecodePixelWidth = targetW;
            }
        }
        bi.EndInit();
        bi.Freeze();

        return new ClipItem(path, File.GetLastWriteTime(path), ClipKind.Image, w, h, bi, null);
    }

    /// <summary>Tam çözünürlüklü görüntü (kopyalama ve sürükleme için).</summary>
    public BitmapSource LoadFull()
    {
        var bi = new BitmapImage();
        bi.BeginInit();
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        bi.UriSource = new Uri(Path, UriKind.Absolute);
        bi.EndInit();
        bi.Freeze();
        return bi;
    }

    private static double TiltFor(string name)
    {
        int sum = 0;
        foreach (char c in name) sum = sum * 31 + c;
        return ((Math.Abs(sum) % 61) - 30) / 10.0;
    }
}
