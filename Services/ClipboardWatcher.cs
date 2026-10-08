using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static Mandal.Services.NativeMethods;
using WF = System.Windows.Forms;

namespace Mandal.Services;

/// <summary>Panoya düşen görüntüleri yakalar; kendi kopyalarımızı, tekrarları ve geçersiz veriyi ayıklar.</summary>
public sealed class ClipboardWatcher : IDisposable
{
    private readonly MessageWindow _window;
    private string _ownFolder;
    private bool _enabled;
    private DateTime _suppressUntil = DateTime.MinValue;
    private string? _lastHash;
    private int _retries;

    /// <summary>Doğrulanmış PNG baytları ile tetiklenir.</summary>
    public event Action<byte[]>? ImageArrived;

    public ClipboardWatcher(MessageWindow window, string ownFolder)
    {
        _window = window;
        _ownFolder = NormalizeFolder(ownFolder);
        _window.Message += OnMessage;
    }

    /// <summary>Kendi alıntı klasörümüz: buradan kopyalanan dosyalar yeniden asılmaz.</summary>
    public string OwnFolder
    {
        get => _ownFolder;
        set => _ownFolder = NormalizeFolder(value);
    }

    private static string NormalizeFolder(string folder)
        => Path.GetFullPath(folder).TrimEnd('\\') + "\\";

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (value == _enabled) return;
            _enabled = value;
            if (value) AddClipboardFormatListener(_window.Handle);
            else RemoveClipboardFormatListener(_window.Handle);
        }
    }

    /// <summary>Kendi kopyalamamızdan sonra kısa süre pano değişikliklerini yok say.</summary>
    public void SuppressFor(TimeSpan span) => _suppressUntil = DateTime.UtcNow + span;

    private void OnMessage(int msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg != WM_CLIPBOARDUPDATE) return;
        _retries = 0;
        Handle();
    }

    private void Handle()
    {
        if (!_enabled || DateTime.UtcNow < _suppressUntil) return;

        byte[]? png;
        try
        {
            png = ReadPng();
        }
        catch (COMException) when (_retries < 3)
        {
            // Başka uygulama panoyu henüz bırakmadı: biraz sonra tekrar dene.
            _retries++;
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150 * _retries) };
            t.Tick += (_, _) => { t.Stop(); Handle(); };
            t.Start();
            return;
        }
        catch (Exception ex)
        {
            Log.Write(ex, "Pano okunamadı");
            return;
        }

        if (png is null || png.Length == 0) return;

        var hash = Convert.ToHexString(SHA256.HashData(png));
        if (hash == _lastHash) return;
        _lastHash = hash;

        ImageArrived?.Invoke(png);
    }

    private byte[]? ReadPng()
    {
        var data = Clipboard.GetDataObject();
        if (data is null) return null;

        // Kendi klasörümüzden dosya kopyalanmışsa (bizim tıkla-kopyala dahil) yeniden asma.
        // Yollar yalnızca karşılaştırılır, açılmaz.
        if (data.GetDataPresent(DataFormats.FileDrop) &&
            data.GetData(DataFormats.FileDrop) is string[] files &&
            files.Any(f => f.StartsWith(_ownFolder, StringComparison.OrdinalIgnoreCase)))
            return null;

        // 1) Doğrudan PNG (Snipping Tool, tarayıcılar, çoğu araç bunu da koyar). İmza + boyut doğrulanır.
        if (data.GetDataPresent("PNG"))
        {
            var bytes = ReadStream(data.GetData("PNG"));
            if (bytes is not null)
            {
                if (Png.HasSignature(bytes)) return bytes;
                Log.Write("Panodaki 'PNG' verisi geçersiz, bitmap yoluna düşüldü");
            }
        }

        if (!data.GetDataPresent(DataFormats.Bitmap) && !data.GetDataPresent(DataFormats.Dib))
            return null;

        // 2) WinForms DIB okuması (alfa sorunu yaşamaz); yeniden PNG'ye kodlanır.
        try
        {
            using var img = WF.Clipboard.GetImage();
            if (img is not null)
            {
                if ((long)img.Width * img.Height > Png.MaxPixels)
                {
                    Log.Write($"Pano görüntüsü çok büyük, atlandı: {img.Width}×{img.Height}");
                    return null;
                }
                using var buf = new MemoryStream();
                img.Save(buf, System.Drawing.Imaging.ImageFormat.Png);
                return buf.ToArray();
            }
        }
        catch (Exception ex)
        {
            Log.Write(ex, "WinForms pano görüntüsü");
        }

        // 3) WPF yedek yolu
        var bs = Clipboard.GetImage();
        if (bs is null) return null;
        if ((long)bs.PixelWidth * bs.PixelHeight > Png.MaxPixels) return null;
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bs));
        using var outStream = new MemoryStream();
        enc.Save(outStream);
        return outStream.ToArray();
    }

    private static byte[]? ReadStream(object? obj)
    {
        switch (obj)
        {
            case MemoryStream ms:
                if (ms.Length > Png.MaxBytes) { Log.Write("Panodaki PNG çok büyük, atlandı"); return null; }
                return ms.ToArray();
            case Stream s:
            {
                if (s.CanSeek && s.Length > Png.MaxBytes) { Log.Write("Panodaki PNG çok büyük, atlandı"); return null; }
                using var buf = new MemoryStream();
                var chunk = new byte[81920];
                long total = 0;
                int n;
                while ((n = s.Read(chunk, 0, chunk.Length)) > 0)
                {
                    total += n;
                    if (total > Png.MaxBytes) { Log.Write("Panodaki PNG çok büyük, atlandı"); return null; }
                    buf.Write(chunk, 0, n);
                }
                return buf.ToArray();
            }
            default:
                return null;
        }
    }

    public void Dispose()
    {
        Enabled = false;
        _window.Message -= OnMessage;
    }
}
