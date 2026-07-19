using System.Windows;
using System.Windows.Media;
using WinNASTools.Core.Localization;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfContentControl = System.Windows.Controls.ContentControl;
using WpfExpander = System.Windows.Controls.Expander;
using WpfGroupBox = System.Windows.Controls.GroupBox;
using WpfLabel = System.Windows.Controls.Label;
using WpfMenuItem = System.Windows.Controls.MenuItem;
using WpfRadioButton = System.Windows.Controls.RadioButton;
using WpfTabItem = System.Windows.Controls.TabItem;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfToolTip = System.Windows.Controls.ToolTip;

namespace WinNASTools.App;

/// <summary>启动时把 XAML 里登记过的简中文案换成当前语言。</summary>
internal static class UiLocalizer
{
    public static void Apply(DependencyObject root)
    {
        if (root is Window w && !string.IsNullOrEmpty(w.Title) && Loc.TryTranslateZh(w.Title, out var title))
            w.Title = title;

        Walk(root);
    }

    private static void Walk(DependencyObject? node)
    {
        if (node is null) return;

        switch (node)
        {
            case WpfTextBlock tb when Loc.TryTranslateZh(tb.Text, out var t):
                tb.Text = t;
                break;
            case WpfTextBox tx when Loc.TryTranslateZh(tx.Text, out var t):
                tx.Text = t;
                break;
            case WpfButton btn when btn.Content is string s && Loc.TryTranslateZh(s, out var t):
                btn.Content = t;
                break;
            case WpfCheckBox cb when cb.Content is string s && Loc.TryTranslateZh(s, out var t):
                cb.Content = t;
                break;
            case WpfRadioButton rb when rb.Content is string s && Loc.TryTranslateZh(s, out var t):
                rb.Content = t;
                break;
            case WpfLabel lb when lb.Content is string s && Loc.TryTranslateZh(s, out var t):
                lb.Content = t;
                break;
            case WpfGroupBox gb when gb.Header is string s && Loc.TryTranslateZh(s, out var t):
                gb.Header = t;
                break;
            case WpfTabItem tab when tab.Header is string s && Loc.TryTranslateZh(s, out var t):
                tab.Header = t;
                break;
            case WpfExpander ex when ex.Header is string s && Loc.TryTranslateZh(s, out var t):
                ex.Header = t;
                break;
            case WpfMenuItem mi when mi.Header is string s && Loc.TryTranslateZh(s, out var t):
                mi.Header = t;
                break;
            case WpfContentControl cc when cc.Content is string s && Loc.TryTranslateZh(s, out var t):
                cc.Content = t;
                break;
        }

        if (node is FrameworkElement fe)
        {
            if (fe.ToolTip is string tip && Loc.TryTranslateZh(tip, out var tt))
                fe.ToolTip = tt;
            else if (fe.ToolTip is WpfToolTip { Content: string tip2 } tipCtrl && Loc.TryTranslateZh(tip2, out var tt2))
                tipCtrl.Content = tt2;
        }

        var count = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < count; i++)
            Walk(VisualTreeHelper.GetChild(node, i));

        foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
            Walk(child);
    }
}
