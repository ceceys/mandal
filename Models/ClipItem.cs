using System.Windows.Media.Imaging;

namespace Mandal.Models;

/// <summary>İpe asılı tek bir alıntı: diskteki PNG + küçük resmi.</summary>
public sealed class ClipItem
{
    public string Path { get; }
    public DateTime Time { get; }
    public int Width { get; }
    public int Height { get; }
    public BitmapSource Thumb { get; }

    /// <summary>İpte hafif eğik dursun diye derece cinsinden sabit küçük açı (-3..+3).</summary>
    public double Tilt { get; }

    public string FileName => System.IO.Path.GetFileName(Path);

    public string Tooltip =>
        $"{Time:dd.MM.yyyy HH:mm:ss}  ·  {Width}×{Height} px\n{FileName}\n\nTıkla: kopyala  ·  Sürükle: taşı  ·  Çift tık: aç";

    private ClipItem(string path, DateTime time, int width, int height, BitmapSource thumb, double tilt)
    {
        Path = path;
        Time = time;
        Width = width;
        Height = height;
        Thumb = thumb;
        Tilt = tilt;
    }

    public static ClipItem Load(string path, int thumbHeightDip, int thumbMaxWidthDip = 260)
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

        var time = File.GetLastWriteTime(path);
        return new ClipItem(path, time, w, h, bi, TiltFor(System.IO.Path.GetFileName(path)));
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
