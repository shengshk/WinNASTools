using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using WinNASTools.Core;
using WinNASTools.Core.Localization;
using WinNASTools.Core.Hosting;
using WinNASTools.Core.Services;

namespace WinNASTools.App;

public partial class HostManageWindow : Window
{
    private readonly AppHost _host;
    private readonly Action _onChanged;
    private bool _loading;
    private string? _selectedId;

    public HostManageWindow(AppHost host, Action onChanged)
    {
        InitializeComponent();
        _host = host;
        _onChanged = onChanged;
        RefreshList();
    }

    private void RefreshList()
    {
        _loading = true;
        LstHosts.Items.Clear();
        foreach (var h in _host.Config.Backup.Hosts)
            LstHosts.Items.Add(new HostItem(h.Id, $"{h.Name}  ({h.Kind})"));

        if (!string.IsNullOrEmpty(_selectedId))
        {
            for (var i = 0; i < LstHosts.Items.Count; i++)
            {
                if (LstHosts.Items[i] is HostItem item && item.Id == _selectedId)
                {
                    LstHosts.SelectedIndex = i;
                    break;
                }
            }
        }

        if (LstHosts.SelectedItem is HostItem sel)
            LoadEditor(sel.Id);
        else
            ClearEditor();
        _loading = false;
    }

    private void LoadEditor(string id)
    {
        var h = _host.Config.Backup.Hosts.FirstOrDefault(x => x.Id == id);
        if (h is null) { ClearEditor(); return; }

        _loading = true;
        _selectedId = id;
        PanelEdit.IsEnabled = true;
        TxtName.Text = h.Name;
        TxtAddress.Text = h.PathOrUrl;
        TxtUser.Text = h.UserName;
        TxtDomain.Text = h.Domain;
        PwdPass.Password = "";
        foreach (ComboBoxItem item in CmbKind.Items)
        {
            if (string.Equals(item.Tag?.ToString(), h.Kind, StringComparison.OrdinalIgnoreCase))
            {
                CmbKind.SelectedItem = item;
                break;
            }
        }
        _loading = false;
    }

    private void ClearEditor()
    {
        _selectedId = null;
        PanelEdit.IsEnabled = false;
        TxtName.Text = "";
        TxtAddress.Text = "";
        TxtUser.Text = "";
        TxtDomain.Text = "";
        PwdPass.Password = "";
    }

    private void LstHosts_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (LstHosts.SelectedItem is HostItem item)
            LoadEditor(item.Id);
        else
            ClearEditor();
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var cfg = Clone(_host.Config);
        var h = new BackupHostConfig
        {
            Name = $"主机 {cfg.Backup.Hosts.Count + 1}",
            Kind = "Smb"
        };
        cfg.Backup.Hosts.Add(h);
        _selectedId = h.Id;
        _host.ReloadConfig(cfg);
        RefreshList();
        _onChanged();
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedId)) return;
        var id = _selectedId;
        var cfg = Clone(_host.Config);
        cfg.Backup.Hosts.RemoveAll(h => h.Id == id);
        foreach (var t in cfg.Backup.Tasks)
        {
            if (t.Source.HostId == id) { t.Source.HostId = null; t.Source.Kind = "Local"; }
            if (t.Target.HostId == id) { t.Target.HostId = null; t.Target.Kind = "Local"; }
        }
        _selectedId = null;
        _host.ReloadConfig(cfg);
        RefreshList();
        _onChanged();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedId)) return;
        var cfg = Clone(_host.Config);
        var h = cfg.Backup.Hosts.FirstOrDefault(x => x.Id == _selectedId);
        if (h is null) return;

        h.Name = string.IsNullOrWhiteSpace(TxtName.Text) ? "主机" : TxtName.Text.Trim();
        h.Kind = (CmbKind.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Smb";
        h.PathOrUrl = TxtAddress.Text?.Trim() ?? "";
        h.UserName = TxtUser.Text?.Trim() ?? "";
        h.Domain = TxtDomain.Text?.Trim() ?? "";
        if (!string.IsNullOrEmpty(PwdPass.Password))
            h.PasswordProtected = SecretProtector.Protect(PwdPass.Password);

        _host.ReloadConfig(cfg);
        PwdPass.Password = "";
        RefreshList();
        _onChanged();
        _host.Log.Info(Loc.T("Log.Ui.HostSaved", h.Name));
    }

    private static AppConfig Clone(AppConfig src)
    {
        var json = JsonSerializer.Serialize(src);
        return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
    }

    private sealed class HostItem
    {
        public string Id { get; }
        public string Display { get; }
        public HostItem(string id, string display) { Id = id; Display = display; }
        public override string ToString() => Display;
    }
}
