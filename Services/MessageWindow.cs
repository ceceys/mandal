using System.Windows.Interop;

namespace Mandal.Services;

/// <summary>Görünmez, yalnızca mesaj alan pencere: kısayol ve pano bildirimleri buraya gelir.</summary>
public sealed class MessageWindow : IDisposable
{
    private readonly HwndSource _source;

    public IntPtr Handle => _source.Handle;

    /// <summary>(msg, wParam, lParam)</summary>
    public event Action<int, IntPtr, IntPtr>? Message;

    public MessageWindow()
    {
        var p = new HwndSourceParameters("MandalMessageWindow")
        {
            Width = 0,
            Height = 0,
            PositionX = 0,
            PositionY = 0,
            WindowStyle = 0,
            ParentWindow = NativeMethods.HWND_MESSAGE,
        };
        _source = new HwndSource(p);
        _source.AddHook(Hook);
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        Message?.Invoke(msg, wParam, lParam);
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        _source.RemoveHook(Hook);
        _source.Dispose();
    }
}
