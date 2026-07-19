using System.Windows;
using System.Windows.Input;
using WinNASTools.Core;
using WinNASTools.Core.Backup;

namespace WinNASTools.App;

/// <summary>在主机根下浏览子目录，返回相对路径（空=根）。</summary>
public partial class RemoteFolderPickerWindow : Window
{
    private readonly BackupEndpointConfig _rootEp;
    private string _current = "";

    public string SelectedRelativePath { get; private set; } = "";

    public RemoteFolderPickerWindow(BackupEndpointConfig hostRootEndpoint, string? initialRelative = null)
    {
        InitializeComponent();
        _rootEp = hostRootEndpoint;
        _current = (initialRelative ?? "").Replace('\\', '/').Trim('/');
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        TxtStatus.Text = "加载中…";
        LstDirs.Items.Clear();
        TxtCurrent.Text = string.IsNullOrEmpty(_current) ? "（主机根）" : _current;
        BtnUp.IsEnabled = !string.IsNullOrEmpty(_current);

        try
        {
            await using var ep = FileEndpointFactory.Create(_rootEp);
            await ep.ConnectAsync(CancellationToken.None);
            var dirs = await ep.ListChildDirectoriesAsync(_current, CancellationToken.None);
            foreach (var d in dirs)
            {
                var name = string.IsNullOrEmpty(_current)
                    ? d
                    : d.StartsWith(_current + "/", StringComparison.OrdinalIgnoreCase)
                        ? d[(_current.Length + 1)..]
                        : d;
                if (string.IsNullOrEmpty(name) || name.Contains('/')) continue;
                LstDirs.Items.Add(new DirItem(d, name));
            }

            TxtStatus.Text = dirs.Count == 0 ? "无子文件夹" : $"{LstDirs.Items.Count} 个文件夹";
        }
        catch (Exception ex)
        {
            TxtStatus.Text = "无法列出：" + ex.Message;
        }
    }

    private async void BtnUp_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_current)) return;
        var i = _current.LastIndexOf('/');
        _current = i < 0 ? "" : _current[..i];
        await RefreshAsync();
    }

    private async void LstDirs_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LstDirs.SelectedItem is not DirItem item) return;
        _current = item.FullRelative;
        await RefreshAsync();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        SelectedRelativePath = _current;
        DialogResult = true;
        Close();
    }

    private sealed class DirItem
    {
        public string FullRelative { get; }
        public string Name { get; }
        public DirItem(string full, string name)
        {
            FullRelative = full;
            Name = name;
        }
        public override string ToString() => Name;
    }
}
