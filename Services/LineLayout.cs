using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Mandal.Services;

/// <summary>
/// "Uzayan ip": öğe sayısı arttıkça kartlar küçülür, hepsi ekrana sığar.
/// Kademeler: ≤12 tam boy, ≤16, ≤24, ≤32, ≤40, ≤50, ≤60, üstü en küçük.
/// Görünümler bu nesneye bağlanır; değerler değişince kendiliğinden yenilenir.
/// </summary>
public sealed class LineLayout : INotifyPropertyChanged
{
    public static LineLayout Current { get; } = new();

    private double _thumbHeight = 120;
    private double _thumbMaxWidth = 260;
    private double _textCardWidth = 190;
    private double _textFontSize = 11.5;
    private double _pinScale = 1;
    private Thickness _itemMargin = new(12, 0, 12, 0);

    public double ThumbHeight { get => _thumbHeight; private set => Set(ref _thumbHeight, value); }
    public double ThumbMaxWidth { get => _thumbMaxWidth; private set => Set(ref _thumbMaxWidth, value); }
    public double TextCardWidth { get => _textCardWidth; private set => Set(ref _textCardWidth, value); }
    public double TextFontSize { get => _textFontSize; private set => Set(ref _textFontSize, value); }
    public double PinScale { get => _pinScale; private set => Set(ref _pinScale, value); }
    public Thickness ItemMargin { get => _itemMargin; private set => Set(ref _itemMargin, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static double FactorFor(int count) => count switch
    {
        <= 12 => 1.0,
        <= 16 => 0.86,
        <= 24 => 0.72,
        <= 32 => 0.60,
        <= 40 => 0.50,
        <= 50 => 0.42,
        <= 60 => 0.36,
        _ => 0.30,
    };

    /// <param name="count">İpteki öğe sayısı</param>
    /// <param name="availableWidth">Kaydırma alanının genişliği (DIP)</param>
    /// <param name="baseHeight">Ayarlardaki tam boy küçük resim yüksekliği</param>
    /// <summary>Bu sayının üstünde kartlar daha da küçülmez; fazlası sağa taşar ve oklarla/tekerlekle kaydırılır.</summary>
    public const int MaxShrinkCount = 60;

    public void Update(int count, double availableWidth, int baseHeight)
    {
        count = Math.Min(count, MaxShrinkCount);
        double factor = FactorFor(count);
        double margin = factor >= 0.86 ? 12 : factor >= 0.6 ? 8 : 5;
        const double framePadding = 11; // beyaz çerçeve 4+4 + yuvarlama payı

        double height = Math.Max(24, Math.Round(baseHeight * factor));

        // Genişlik bütçesi: hepsi yan yana sığsın
        double perItem = availableWidth > 0 && count > 0
            ? availableWidth / count - 2 * margin - framePadding
            : 260;
        double maxWidth = Math.Clamp(perItem, 36, Math.Max(80, 260 * Math.Max(factor, 0.6)));

        ThumbHeight = height;
        ThumbMaxWidth = maxWidth;
        TextCardWidth = Math.Clamp(Math.Min(190 * Math.Max(factor, 0.5), perItem), 56, 190);
        TextFontSize = Math.Clamp(11.5 * Math.Max(factor, 0.7), 8, 11.5);
        PinScale = Math.Clamp(factor, 0.55, 1);
        ItemMargin = new Thickness(margin, 0, margin, 0);
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
