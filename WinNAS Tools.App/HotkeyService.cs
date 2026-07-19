using System.Runtime.InteropServices;
using System.Windows.Forms;
using WinNASTools.Core.Services;

namespace WinNASTools.App;

/// <summary>全局热键一键离开；支持从配置字符串重绑。</summary>
public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 0x4701;
    private const int WmHotkey = 0x0312;
    private const uint ModNorepeat = 0x4000;

    private readonly Action _onLeave;
    private readonly HotkeyNativeWindow _window;
    private bool _registered;
    private string _display = "Ctrl+Alt+Shift+L";

    public HotkeyService(Action onLeave, string? hotkeySpec = null)
    {
        _onLeave = onLeave;
        _window = new HotkeyNativeWindow(OnHotkey);
        _window.CreateHandle(new CreateParams());
        TryRegister(hotkeySpec ?? "Ctrl+Alt+Shift+L");
    }

    public bool IsRegistered => _registered;
    public string DisplayText => _display;

    public bool TryRebind(string hotkeySpec)
    {
        if (!TryParse(hotkeySpec, out var mods, out var vk, out var display))
            return false;

        const int tempId = HotkeyId + 1;
        if (!RegisterHotKey(_window.Handle, tempId, mods | ModNorepeat, vk))
            return false;

        UnregisterCurrent();
        UnregisterHotKey(_window.Handle, tempId);
        _display = display;
        _registered = RegisterHotKey(_window.Handle, HotkeyId, mods | ModNorepeat, vk);
        return _registered;
    }

    private bool TryRegister(string hotkeySpec)
    {
        if (!TryParse(hotkeySpec, out var mods, out var vk, out var display))
        {
            _display = hotkeySpec;
            _registered = false;
            return false;
        }

        _display = display;
        _registered = RegisterHotKey(_window.Handle, HotkeyId, mods | ModNorepeat, vk);
        return _registered;
    }

    private void UnregisterCurrent()
    {
        if (_registered && _window.Handle != IntPtr.Zero)
        {
            UnregisterHotKey(_window.Handle, HotkeyId);
            _registered = false;
        }
    }

    public static bool TryParse(string? spec, out uint modifiers, out uint vk, out string display)
        => HotkeySpec.TryParse(spec, out modifiers, out vk, out display);

    public static string Format(uint modifiers, uint vk) => HotkeySpec.Format(modifiers, vk);

    private void OnHotkey() => _onLeave();

    public void Dispose()
    {
        UnregisterCurrent();
        _window.DestroyHandle();
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private sealed class HotkeyNativeWindow : NativeWindow
    {
        private readonly Action _callback;
        public HotkeyNativeWindow(Action callback) => _callback = callback;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey && m.WParam.ToInt32() == HotkeyId)
                _callback();
            base.WndProc(ref m);
        }
    }
}
