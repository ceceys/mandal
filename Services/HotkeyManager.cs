using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using static Mandal.Services.NativeMethods;

namespace Mandal.Services;

public sealed class HotkeyManager : IDisposable
{
    private readonly MessageWindow _window;
    private readonly Dictionary<int, Action> _actions = new();
    private int _nextId = 1;

    public HotkeyManager(MessageWindow window)
    {
        _window = window;
        _window.Message += OnMessage;
    }

    private void OnMessage(int msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg != WM_HOTKEY) return;
        if (_actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            try { action(); }
            catch (Exception ex) { Log.Write(ex, "Kısayol işlemi"); }
        }
    }

    /// <summary>"Ctrl+Shift+S" gibi bir ifadeyi sistem genelinde kaydeder.</summary>
    public bool Register(string gesture, Action action, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(gesture)) { error = "boş"; return false; }

        if (!TryParse(gesture, out uint mods, out uint vk))
        {
            error = $"'{gesture}' anlaşılamadı";
            return false;
        }

        int id = _nextId++;
        if (!RegisterHotKey(_window.Handle, id, mods | MOD_NOREPEAT, vk))
        {
            int code = Marshal.GetLastWin32Error();
            error = code == 1409
                ? $"{gesture} başka bir program tarafından kullanılıyor"
                : $"{gesture} kaydedilemedi ({new Win32Exception(code).Message})";
            return false;
        }

        _actions[id] = action;
        return true;
    }

    public void UnregisterAll()
    {
        foreach (var id in _actions.Keys)
            UnregisterHotKey(_window.Handle, id);
        _actions.Clear();
    }

    public static bool TryParse(string gesture, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        Key? key = null;

        foreach (var raw in gesture.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl": case "control": case "strg": modifiers |= MOD_CONTROL; continue;
                case "shift": modifiers |= MOD_SHIFT; continue;
                case "alt": modifiers |= MOD_ALT; continue;
                case "win": case "windows": case "super": modifiers |= MOD_WIN; continue;
            }

            if (key is not null) return false; // iki ana tuş olmaz

            key = raw.ToLowerInvariant() switch
            {
                "printscreen" or "prtscn" or "prtsc" or "print" or "snapshot" => Key.Snapshot,
                "space" or "boşluk" or "bosluk" => Key.Space,
                "esc" or "escape" => Key.Escape,
                "enter" or "return" => Key.Return,
                "tab" => Key.Tab,
                "ins" or "insert" => Key.Insert,
                "del" or "delete" => Key.Delete,
                "home" => Key.Home,
                "end" => Key.End,
                "pgup" or "pageup" => Key.PageUp,
                "pgdn" or "pagedown" => Key.PageDown,
                "`" or "tilde" or "oem3" => Key.Oem3,
                _ when raw.Length == 1 && char.IsDigit(raw[0]) => (Key)Enum.Parse(typeof(Key), "D" + raw),
                _ => Enum.TryParse<Key>(raw, true, out var k) ? k : null,
            };

            if (key is null) return false;
        }

        if (key is null) return false;
        virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key.Value);
        return virtualKey != 0;
    }

    public void Dispose()
    {
        UnregisterAll();
        _window.Message -= OnMessage;
    }
}
