using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace DNSYar.Services;

public sealed record TrayProviderInfo(string Id, string Name, int Score, bool IsActive, double? PingMs);

public sealed record TrayMenuText(
    string ActivePrefix,
    string Open,
    string QuickSwitch,
    string SmartAuto,
    string TestNow,
    string Restore,
    string Exit,
    string Empty,
    bool RightToLeft);

public enum TrayNotifyIcon
{
    None,
    Info,
    Warning,
    Error
}

public sealed class TrayIconService : IDisposable
{
    private Forms.NotifyIcon? _icon;
    private Forms.ToolStripMenuItem? _statusItem;
    private Forms.ToolStripMenuItem? _smartItem;
    private Forms.ToolStripMenuItem? _quickSwitchItem;
    private Forms.ToolStripMenuItem? _openItem;
    private Forms.ToolStripMenuItem? _testNowItem;
    private Forms.ToolStripMenuItem? _restoreItem;
    private Forms.ToolStripMenuItem? _exitItem;
    private bool _suppressSmartEvent;
    private string _activePrefix = "DNS فعال: {0}";
    private string _emptyText = "DNSی موجود نیست";
    private string _lastActiveName = "—";

    public event Action? ShowRequested;
    public event Action? RunSmartNowRequested;
    public event Action? RestoreRequested;
    public event Action? ExitRequested;
    public event Action<bool>? SmartAutoChanged;
    public event Action<TrayProviderInfo>? ProviderRequested;

    public bool IsInitialized => _icon is not null;

    public void Initialize(bool smartEnabled)
    {
        if (_icon is not null) return;

        _icon = new Forms.NotifyIcon
        {
            Icon = TryGetAppIcon() ?? Drawing.SystemIcons.Application,
            Text = "DNSYar — Smart DNS Manager",
            Visible = true
        };
        _icon.DoubleClick += (_, _) => ShowRequested?.Invoke();

        var menu = new Forms.ContextMenuStrip { RightToLeft = Forms.RightToLeft.Yes };
        _statusItem = new Forms.ToolStripMenuItem("DNS فعال: —") { Enabled = false };
        menu.Items.Add(_statusItem);
        menu.Items.Add(new Forms.ToolStripSeparator());

        _openItem = new Forms.ToolStripMenuItem("باز کردن DNSYar");
        _openItem.Click += (_, _) => ShowRequested?.Invoke();
        menu.Items.Add(_openItem);

        _quickSwitchItem = new Forms.ToolStripMenuItem("تغییر سریع DNS");
        menu.Items.Add(_quickSwitchItem);

        _smartItem = new Forms.ToolStripMenuItem("Smart Auto DNS")
        {
            CheckOnClick = true,
            Checked = smartEnabled
        };
        _smartItem.CheckedChanged += (_, _) =>
        {
            if (!_suppressSmartEvent) SmartAutoChanged?.Invoke(_smartItem.Checked);
        };
        menu.Items.Add(_smartItem);

        _testNowItem = new Forms.ToolStripMenuItem("بررسی هوشمند الآن");
        _testNowItem.Click += (_, _) => RunSmartNowRequested?.Invoke();
        menu.Items.Add(_testNowItem);

        _restoreItem = new Forms.ToolStripMenuItem("بازگردانی DNS قبلی");
        _restoreItem.Click += (_, _) => RestoreRequested?.Invoke();
        menu.Items.Add(_restoreItem);

        menu.Items.Add(new Forms.ToolStripSeparator());
        _exitItem = new Forms.ToolStripMenuItem("خروج کامل از DNSYar");
        _exitItem.Click += (_, _) => ExitRequested?.Invoke();
        menu.Items.Add(_exitItem);

        _icon.ContextMenuStrip = menu;
    }

    public void SetVisible(bool visible)
    {
        if (_icon is not null) _icon.Visible = visible;
    }

    public void ApplyMenu(TrayMenuText text)
    {
        _activePrefix = text.ActivePrefix;
        _emptyText = text.Empty;
        if (_icon?.ContextMenuStrip is not null)
            _icon.ContextMenuStrip.RightToLeft = text.RightToLeft ? Forms.RightToLeft.Yes : Forms.RightToLeft.No;
        if (_openItem is not null) _openItem.Text = text.Open;
        if (_quickSwitchItem is not null) _quickSwitchItem.Text = text.QuickSwitch;
        if (_smartItem is not null) _smartItem.Text = text.SmartAuto;
        if (_testNowItem is not null) _testNowItem.Text = text.TestNow;
        if (_restoreItem is not null) _restoreItem.Text = text.Restore;
        if (_exitItem is not null) _exitItem.Text = text.Exit;
        if (_statusItem is not null) _statusItem.Text = string.Format(_activePrefix, _lastActiveName);
    }

    public void Update(string activeDnsName, bool smartEnabled, IEnumerable<TrayProviderInfo> providers)
    {
        if (_icon is null) return;
        _lastActiveName = activeDnsName;
        if (_statusItem is not null) _statusItem.Text = string.Format(_activePrefix, activeDnsName);
        if (_smartItem is not null && _smartItem.Checked != smartEnabled)
        {
            _suppressSmartEvent = true;
            _smartItem.Checked = smartEnabled;
            _suppressSmartEvent = false;
        }

        if (_quickSwitchItem is null) return;
        _quickSwitchItem.DropDownItems.Clear();

        var top = providers
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.Score)
            .ThenBy(x => x.PingMs ?? double.MaxValue)
            .ThenBy(x => x.Name)
            .Take(10)
            .ToList();

        foreach (var provider in top)
        {
            var suffix = provider.Score > 0 ? $" — {provider.Score}/100" : string.Empty;
            var item = new Forms.ToolStripMenuItem($"{(provider.IsActive ? "✓ " : "")}{provider.Name}{suffix}")
            {
                Tag = provider
            };
            item.Click += (_, _) => ProviderRequested?.Invoke(provider);
            _quickSwitchItem.DropDownItems.Add(item);
        }

        if (top.Count == 0)
            _quickSwitchItem.DropDownItems.Add(new Forms.ToolStripMenuItem(_emptyText) { Enabled = false });
    }

    public void Notify(string title, string message, TrayNotifyIcon icon = TrayNotifyIcon.Info)
    {
        if (_icon is null || !_icon.Visible) return;
        _icon.BalloonTipTitle = title;
        _icon.BalloonTipText = message;
        _icon.BalloonTipIcon = icon switch
        {
            TrayNotifyIcon.Error => Forms.ToolTipIcon.Error,
            TrayNotifyIcon.Warning => Forms.ToolTipIcon.Warning,
            TrayNotifyIcon.None => Forms.ToolTipIcon.None,
            _ => Forms.ToolTipIcon.Info
        };
        _icon.ShowBalloonTip(3500);
    }

    public void Dispose()
    {
        if (_icon is null) return;
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
        _icon = null;
    }

    private static Drawing.Icon? TryGetAppIcon()
    {
        try
        {
            var path = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            return string.IsNullOrEmpty(path) ? null : Drawing.Icon.ExtractAssociatedIcon(path);
        }
        catch
        {
            return null;
        }
    }
}
