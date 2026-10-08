using System.Text;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace Mandal.Services;

/// <summary>Windows'un yerleşik OCR motoru (Windows.Media.Ocr); ağ yok, kurulu dil paketlerini kullanır.</summary>
public static class Ocr
{
    public static bool IsAvailable
    {
        get
        {
            try { return OcrEngine.AvailableRecognizerLanguages.Count > 0; }
            catch { return false; }
        }
    }

    /// <returns>Tanınan metin; motor yoksa null; metin yoksa boş dize.</returns>
    public static async Task<string?> RecognizeAsync(string pngPath)
    {
        var engine = OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is null && OcrEngine.AvailableRecognizerLanguages.Count > 0)
            engine = OcrEngine.TryCreateFromLanguage(OcrEngine.AvailableRecognizerLanguages[0]);
        if (engine is null) return null;

        using var fs = File.OpenRead(pngPath);
        using var ras = fs.AsRandomAccessStream();
        var decoder = await BitmapDecoder.CreateAsync(ras);

        uint w = decoder.PixelWidth, h = decoder.PixelHeight;
        uint max = OcrEngine.MaxImageDimension;
        var transform = new BitmapTransform { InterpolationMode = BitmapInterpolationMode.Fant };

        double scale = 1;
        if (h < 200 || w < 200) scale = 2;                       // küçük bölgelerde tanıma daha iyi
        if (w * scale > max || h * scale > max) scale = Math.Min((double)max / w, (double)max / h);
        if (Math.Abs(scale - 1) > 0.01)
        {
            transform.ScaledWidth = (uint)Math.Max(1, Math.Round(w * scale));
            transform.ScaledHeight = (uint)Math.Max(1, Math.Round(h * scale));
        }

        using var bitmap = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform,
            ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);

        var result = await engine.RecognizeAsync(bitmap);
        var sb = new StringBuilder();
        foreach (var line in result.Lines)
            sb.AppendLine(line.Text);
        return sb.ToString().TrimEnd();
    }
}
