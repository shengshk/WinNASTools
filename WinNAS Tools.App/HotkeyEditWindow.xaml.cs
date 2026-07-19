using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using WinNASTools.Core;
using Hk = WinNASTools.Core.Services.HotkeySpec;

namespace WinNASTools.App;

public partial class HotkeyEditWindow : Window
{
    private readonly bool _allowClear;

    public string HotkeySpec { get; private set; }

    public HotkeyEditWindow(string current, string? title = null, bool allowClear = false)
    {
        _allowClear = allowClear;
        HotkeySpec = string.IsNullOrWhiteSpace(current)
            ? (allowClear ? "" : "Ctrl+Alt+Shift+L")
            : current.Trim();
        InitializeComponent();
        if (!string.IsNullOrWhiteSpace(title))
            Title = title;
        TxtCurrent.Text = string.IsNullOrWhiteSpace(HotkeySpec) ? "（未设置）" : HotkeySpec;
        BtnClear.Visibility = allowClear ? Visibility.Visible : Visibility.Collapsed;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin
            or Key.None)
            return;

        var mods = ModifierKeys.None;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) mods |= ModifierKeys.Control;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) mods |= ModifierKeys.Alt;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) mods |= ModifierKeys.Shift;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) mods |= ModifierKeys.Windows;
        if (mods == ModifierKeys.None)
        {
            TxtHint.Text = "请至少按住一个修饰键（Ctrl / Alt / Shift）再按主键";
            return;
        }

        var parts = new List<string>();
        if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (mods.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (mods.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (mods.HasFlag(ModifierKeys.Windows)) parts.Add("Win");

        if (!TryMapKey(key, out var keyName))
        {
            TxtHint.Text = "支持字母、数字、F1–F24、方向键、Space/Tab/Enter 等";
            return;
        }

        parts.Add(keyName);
        var spec = string.Join("+", parts);
        if (!Hk.TryParse(spec, out _, out _, out var display))
        {
            TxtHint.Text = "组合无效";
            return;
        }

        HotkeySpec = display;
        TxtCurrent.Text = display;
        TxtHint.Text = "已捕获，点确定保存";
    }

    private static bool TryMapKey(Key key, out string keyName)
    {
        keyName = "";
        if (key is >= Key.F1 and <= Key.F24)
        {
            keyName = "F" + (key - Key.F1 + 1);
            return true;
        }
        if (key is >= Key.A and <= Key.Z)
        {
            keyName = ((char)('A' + (key - Key.A))).ToString();
            return true;
        }
        if (key is >= Key.D0 and <= Key.D9)
        {
            keyName = ((char)('0' + (key - Key.D0))).ToString();
            return true;
        }

        keyName = key switch
        {
            Key.Up => "Up",
            Key.Down => "Down",
            Key.Left => "Left",
            Key.Right => "Right",
            Key.Space => "Space",
            Key.Tab => "Tab",
            Key.Enter => "Enter",
            Key.Escape => "Esc",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.Insert => "Insert",
            Key.Delete => "Delete",
            _ => ""
        };
        return keyName.Length > 0;
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        HotkeySpec = "";
        TxtCurrent.Text = "（未设置）";
        TxtHint.Text = "已清除，点确定保存";
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(HotkeySpec))
        {
            if (!_allowClear)
            {
                System.Windows.MessageBox.Show("请先按下有效的快捷键组合。", AppBranding.Name,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
            Close();
            return;
        }

        if (!Hk.TryParse(HotkeySpec, out _, out _, out _))
        {
            System.Windows.MessageBox.Show("请先按下有效的快捷键组合。", AppBranding.Name,
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
