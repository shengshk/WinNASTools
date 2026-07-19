using System.Runtime.InteropServices;
using System.Text;
using WinNASTools.Core.Native;

namespace WinNASTools.Core.Services;

/// <summary>热键字符串解析与模拟按键（供离开热键注册与媒体备用快捷键共用）。</summary>
public static class HotkeySpec
{
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;

    private const uint KeyeventfKeyup = 0x0002;
    private const uint KeyeventfExtendedkey = 0x0001;
    private const uint InputKeyboard = 1;

    private const byte VkControl = 0x11;
    private const byte VkMenu = 0x12;   // Alt
    private const byte VkShift = 0x10;
    private const byte VkLwin = 0x5B;

    public static bool TryParse(string? spec, out uint modifiers, out uint vk, out string display)
    {
        modifiers = 0;
        vk = 0;
        display = "";
        if (string.IsNullOrWhiteSpace(spec)) return false;

        var parts = spec.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2) return false;

        uint mods = 0;
        uint key = 0;
        foreach (var raw in parts)
        {
            var p = raw.Trim();
            if (p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)
                || p.Equals("Control", StringComparison.OrdinalIgnoreCase))
                mods |= ModControl;
            else if (p.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                mods |= ModAlt;
            else if (p.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                mods |= ModShift;
            else if (p.Equals("Win", StringComparison.OrdinalIgnoreCase)
                     || p.Equals("Windows", StringComparison.OrdinalIgnoreCase))
                mods |= ModWin;
            else if (p.Length == 1)
                key = char.ToUpperInvariant(p[0]);
            else if (p.StartsWith("F", StringComparison.OrdinalIgnoreCase)
                     && int.TryParse(p[1..], out var fn) && fn is >= 1 and <= 24)
                key = (uint)(0x70 + fn - 1);
            else if (TryNamedKey(p, out var named))
                key = named;
            else
                return false;
        }

        if (mods == 0 || key == 0) return false;
        modifiers = mods;
        vk = key;
        display = Format(mods, key);
        return true;
    }

    public static string Format(uint modifiers, uint vk)
    {
        var sb = new StringBuilder();
        if ((modifiers & ModControl) != 0) sb.Append("Ctrl+");
        if ((modifiers & ModAlt) != 0) sb.Append("Alt+");
        if ((modifiers & ModShift) != 0) sb.Append("Shift+");
        if ((modifiers & ModWin) != 0) sb.Append("Win+");
        sb.Append(KeyDisplayName(vk));
        return sb.ToString();
    }

    /// <summary>按热键规格模拟一次完整按下/抬起。</summary>
    public static bool TrySend(string? spec)
    {
        if (!TryParse(spec, out var mods, out var vk, out _))
            return false;

        var keys = new List<(byte Vk, bool Extended)>(4);
        if ((mods & ModControl) != 0) keys.Add((VkControl, false));
        if ((mods & ModAlt) != 0) keys.Add((VkMenu, false));
        if ((mods & ModShift) != 0) keys.Add((VkShift, false));
        if ((mods & ModWin) != 0) keys.Add((VkLwin, false));
        keys.Add(((byte)vk, IsExtendedKey(vk)));

        var inputs = new NativeMethods.Input[keys.Count * 2];
        for (var i = 0; i < keys.Count; i++)
            inputs[i] = KeyInput(keys[i].Vk, keys[i].Extended, keyUp: false);
        for (var i = 0; i < keys.Count; i++)
        {
            var k = keys[keys.Count - 1 - i];
            inputs[keys.Count + i] = KeyInput(k.Vk, k.Extended, keyUp: true);
        }

        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.Input>());
        return sent == (uint)inputs.Length;
    }

    private static bool TryNamedKey(string name, out uint vk)
    {
        vk = name.ToUpperInvariant() switch
        {
            "UP" or "ARROWUP" => 0x26,
            "DOWN" or "ARROWDOWN" => 0x28,
            "LEFT" or "ARROWLEFT" => 0x25,
            "RIGHT" or "ARROWRIGHT" => 0x27,
            "SPACE" => 0x20,
            "TAB" => 0x09,
            "ENTER" or "RETURN" => 0x0D,
            "ESC" or "ESCAPE" => 0x1B,
            "HOME" => 0x24,
            "END" => 0x23,
            "PAGEUP" or "PGUP" => 0x21,
            "PAGEDOWN" or "PGDN" => 0x22,
            "INSERT" or "INS" => 0x2D,
            "DELETE" or "DEL" => 0x2E,
            _ => 0u
        };
        return vk != 0;
    }

    private static string KeyDisplayName(uint vk) => vk switch
    {
        >= 0x70 and <= 0x87 => "F" + (vk - 0x70 + 1),
        0x26 => "Up",
        0x28 => "Down",
        0x25 => "Left",
        0x27 => "Right",
        0x20 => "Space",
        0x09 => "Tab",
        0x0D => "Enter",
        0x1B => "Esc",
        0x24 => "Home",
        0x23 => "End",
        0x21 => "PageUp",
        0x22 => "PageDown",
        0x2D => "Insert",
        0x2E => "Delete",
        _ => ((char)vk).ToString()
    };

    private static bool IsExtendedKey(uint vk) =>
        vk is 0x25 or 0x26 or 0x27 or 0x28 // arrows
            or 0x21 or 0x22 or 0x23 or 0x24 // pgup/pgdn/end/home
            or 0x2D or 0x2E; // insert/delete

    private static NativeMethods.Input KeyInput(byte vk, bool extended, bool keyUp)
    {
        uint flags = keyUp ? KeyeventfKeyup : 0;
        if (extended) flags |= KeyeventfExtendedkey;
        return new NativeMethods.Input
        {
            Type = InputKeyboard,
            Data = new NativeMethods.InputUnion
            {
                Ki = new NativeMethods.KeybdInput
                {
                    WVk = vk,
                    WScan = 0,
                    DwFlags = flags,
                    Time = 0,
                    DwExtraInfo = IntPtr.Zero
                }
            }
        };
    }
}
