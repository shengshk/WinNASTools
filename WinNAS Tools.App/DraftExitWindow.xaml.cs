using System.Windows;

namespace WinNASTools.App;

public enum DraftExitChoice
{
    /// <summary>单草稿：跳转继续编辑；多草稿：取消关窗。</summary>
    Stay,
    /// <summary>丢弃全部草稿并允许退出。</summary>
    Discard
}

public partial class DraftExitWindow : Window
{
    public DraftExitChoice Choice { get; private set; } = DraftExitChoice.Stay;

    public DraftExitWindow(string message, bool singleDraft)
    {
        InitializeComponent();
        TxtMessage.Text = message;
        if (singleDraft)
        {
            BtnPrimary.Content = "继续编辑";
            BtnSecondary.Content = "丢弃";
        }
        else
        {
            BtnPrimary.Content = "返回";
            BtnSecondary.Content = "丢弃";
        }
    }

    private void BtnPrimary_Click(object sender, RoutedEventArgs e)
    {
        Choice = DraftExitChoice.Stay;
        DialogResult = true;
    }

    private void BtnSecondary_Click(object sender, RoutedEventArgs e)
    {
        Choice = DraftExitChoice.Discard;
        DialogResult = true;
    }
}
