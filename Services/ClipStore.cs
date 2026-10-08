using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using Mandal.Models;

namespace Mandal.Services;

/// <summary>Alıntı klasörü ile ipteki öğeler arasındaki köprü.</summary>
public sealed class ClipStore
{
    public ObservableCollection<ClipItem> Items { get; } = new();
    public string Folder { get; }
    private readonly int _thumbHeight;

    public ClipStore(string folder, int thumbHeight)
    {
        Folder = folder;
        _thumbHeight = thumbHeight;
    }

    public void Load()
    {
        Directory.CreateDirectory(Folder);
        Items.Clear();
        var files = Directory.EnumerateFiles(Folder, "*.png")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTime);

        foreach (var f in files)
        {
            try { Items.Add(ClipItem.Load(f.FullName, _thumbHeight)); }
            catch (Exception ex) { Log.Write(ex, $"Yüklenemedi: {f.Name}"); }
        }
    }

    public ClipItem Add(Bitmap bitmap)
    {
        Directory.CreateDirectory(Folder);
        var path = NewPath();
        bitmap.Save(path, ImageFormat.Png);
        return AddExisting(path);
    }

    public ClipItem AddPng(byte[] png)
    {
        Directory.CreateDirectory(Folder);
        var path = NewPath();
        File.WriteAllBytes(path, png);
        return AddExisting(path);
    }

    private ClipItem AddExisting(string path)
    {
        var item = ClipItem.Load(path, _thumbHeight);
        Items.Insert(0, item);
        return item;
    }

    public void Delete(ClipItem item)
    {
        Items.Remove(item);
        try
        {
            if (File.Exists(item.Path)) File.Delete(item.Path);
        }
        catch (Exception ex)
        {
            Log.Write(ex, $"Silinemedi: {item.FileName}");
            throw;
        }
    }

    private string NewPath()
    {
        var stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var path = Path.Combine(Folder, stamp + ".png");
        int i = 2;
        while (File.Exists(path))
            path = Path.Combine(Folder, $"{stamp}_{i++}.png");
        return path;
    }
}
