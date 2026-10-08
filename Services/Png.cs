namespace Mandal.Services;

/// <summary>Güvenilmeyen baytların PNG olduğunu imzadan doğrular (çözücüye gitmeden).</summary>
internal static class Png
{
    private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    /// <summary>Panodan gelen verinin üst sınırı; daha büyüğü yok sayılır.</summary>
    public const long MaxBytes = 64L * 1024 * 1024;

    /// <summary>Bitmap yolu için piksel üst sınırı (ör. 10000×10000).</summary>
    public const long MaxPixels = 100_000_000;

    public static bool HasSignature(ReadOnlySpan<byte> data)
        => data.Length >= Signature.Length && data[..Signature.Length].SequenceEqual(Signature);

    public static bool FileHasSignature(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            Span<byte> head = stackalloc byte[8];
            int read = fs.Read(head);
            return read == 8 && HasSignature(head);
        }
        catch
        {
            return false;
        }
    }
}
