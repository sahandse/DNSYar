using DNSYar.Models;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace DNSYar.Services;

public sealed class TrayIconService : IDisposable
{
    private Forms.NotifyIcon? _icon;
    private Forms.ToolStripMenuItem? _statusItem;
    private Forms.ToolStripMenuItem? _smartItem;
    private Forms.ToolStripMenuItem? _quickSwitchItem;
    private bool _suppressSmartEvent;

    public event Action? ShowRequested;
    public event Action? RunSmartNowRequested;
    public event Action? RestoreRequested;
    public event Action? ExitRequested;
    public event Action<bool>? SmartAutoChanged;
    public event Action<DnsProvider>? ProviderRequested;

    public bool IsInitialized => _icon is not null;

    public void Initialize(bool smartEnabled)
    {
        if (_icon is not null) return;

        _icon = new Forms.NotifyIcon
        {
            Icon = Drawing.SystemIcons.Application,
            Text = "DNSYar — Smart DNS Manager",
            Visible = true
        };
        _icon.DoubleClick += (_, _) => ShowRequested?.Invoke();

        var menu = new Forms.ContextMenuStrip { RightToLeft = Forms.RightToLeft.Yes };
        _statusItem = new Forms.ToolStripMenuItem("DNS فعال: —") { Enabled = false };
        menu.Items.Add(_statusItem);
        menu.Items.Add(new Forms.ToolStripSeparator());

        var openItem = new Forms.ToolStripMenuItem("باز کردن DNSYar");
        openItem.Click += (_, _) => ShowRequested?.Invoke();
        menu.Items.Add(openItem);

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

        var testNowItem = new Forms.ToolStripMenuItem("بررسی هوشمند الآن");
        testNowItem.Click += (_, _) => RunSmartNowRequested?.Invoke();
        menu.Items.Add(testNowItem);

        var restoreItem = new Forms.ToolStripMenuItem("بازگردانی DNS قبلی");
        restoreItem.Click += (_, _) => RestoreRequested?.Invoke();
        menu.Items.Add(restoreItem);

        menu.Items.Add(new Forms.ToolStripSeparator());
        var exitItem = new Forms.ToolStripMenuItem("خروج کامل از DNSYar");
        exitItem.Click += (_, _) => ExitRequested?.Invoke();
        menu.Items.Add(exitItem);

        _icon.ContextMenuStrip = menu;
    }

    public void SetVisible(bool visible)
    {
        if (_icon is not null) _icon.Visible = visible;
    }

    public void Update(string activeDnsName, bool smartEnabled, IEnumerable<DnsProvider> providers)
    {
        if (_icon is null) return;
        if (_statusItem is not null) _statusItem.Text = $"DNS فعال: {activeDnsName}";
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
            _quickSwitchItem.DropDownItems.Add(new Forms.ToolStripMenuItem("DNSی موجود نیست") { Enabled = false });
    }

    public void Notify(string title, string message, Forms.ToolTipIcon icon = Forms.ToolTipIcon.Info)
    {
        if (_icon is null || !_icon.Visible) return;
        _icon.BalloonTipTitle = title;
        _icon.BalloonTipText = message;
        _icon.BalloonTipIcon = icon;
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
}
