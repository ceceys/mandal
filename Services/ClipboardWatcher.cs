using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static Mandal.Services.NativeMethods;
using WF = System.Windows.Forms;

namespace Mandal.Services;

/// <summary>Panoya düşen görüntüleri yakalar; kendi kopyalarımızı ve tekrarları ayıklar.</summary>
public sealed class ClipboardWatcher : IDisposable
{
    private readonly MessageWindow _window;
    private readonly string _ownFolder;
    private bool _enabled;
    private DateTime _suppressUntil = DateTime.MinValue;
    private string? _lastHash;
    private int _retries;

    /// <summary>PNG baytları ile tetiklenir.</summary>
    public event Action<byte[]>? ImageArrived;

    public ClipboardWatcher(MessageWindow window, string ownFolder)
    {
        _window = window;
        _ownFolder = Path.GetFullPath(ownFolder).TrimEnd('\\') + "\\";
        _window.Message += OnMessage;
    }

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
        if (data.GetDataPresent(DataFormats.FileDrop) &&
            data.GetData(DataFormats.FileDrop) is string[] files &&
            files.Any(f => f.StartsWith(_ownFolder, StringComparison.OrdinalIgnoreCase)))
            return null;

        // 1) Doğrudan PNG (Snipping Tool, tarayıcılar, çoğu araç bunu da koyar)
        if (data.GetDataPresent("PNG"))
        {
            var obj = data.GetData("PNG");
            if (obj is MemoryStream ms) return ms.ToArray();
            if (obj is Stream s)
            {
                using var buf = new MemoryStream();
                s.CopyTo(buf);
                return buf.ToArray();
            }
        }

        if (!data.GetDataPresent(DataFormats.Bitmap) && !data.GetDataPresent(DataFormats.Dib))
            return null;

        // 2) WinForms DIB okuması (alfa sorunu yaşamaz)
        try
        {
            using var img = WF.Clipboard.GetImage();
            if (img is not null)
            {
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
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bs));
        using var outStream = new MemoryStream();
        enc.Save(outStream);
        return outStream.ToArray();
    }

    public void Dispose()
    {
        Enabled = false;
        _window.Message -= OnMessage;
    }
}
