using System.Windows;
using WinNASTools.Core;
using WinNASTools.Core.Localization;

namespace WinNASTools.App;

public partial class LogRetentionWindow : Window
{
    public int Days { get; private set; }

    public LogRetentionWindow(int current)
    {
        Days = current;
        InitializeComponent();
        TxtDays.Text = current.ToString();
        TxtDays.SelectAll();
        Loaded += (_, _) => TxtDays.Focus();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(TxtDays.Text.Trim(), out var days) || days is < 1 or > 3650)
        {
            System.Windows.MessageBox.Show(Loc.T("Msg.LogRetentionInvalid"), AppBranding.Name,
                MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtDays.Focus();
            TxtDays.SelectAll();
            return;
        }

        Days = days;
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
