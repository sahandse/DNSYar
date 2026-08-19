using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.ComponentModel;
using DNSYar.Models;
using DNSYar.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using Microsoft.Win32;
using Windows.Foundation;
using Windows.UI;
using Forms = System.Windows.Forms;

namespace DNSYar;

public sealed partial class MainWindow : Window
{
    private readonly LocalStore _store = new();
    private readonly NetworkDnsManager _network = new();
    private readonly BenchmarkService _benchmark = new();
    private readonly CatalogUpdater _updater = new();
    private readonly SiteProbeService _siteProbe = new();
    private readonly ObservableCollection<DnsProvider> _visibleProviders = new();
    private readonly ObservableCollection<SiteDnsTestResult> _siteResults = new();
    private readonly DispatcherTimer _catalogTimer = new();
    private readonly DispatcherTimer _autoDnsTimer = new();
    private readonly TrayIconService _tray = new();

    private List<DnsProvider> _providers = new();
    private List<ServiceTarget> _targets = new();
    private AppSettings _settings = new();
    private AdapterInfo? _selectedAdapter;
    private DnsProvider? _best;
    private string _filter = "all";
    private string _searchText = "";
    private bool _initializing = true;
    private bool _activationStarted;
    private bool _allowClose;
    private bool _closeRestoreInProgress;
    private bool _catalogUpdateInProgress;
    private bool _smartAutoInProgress;
    private int _smartAutoFailureStreak;
    private bool _closeRequestedFromTray;
    private readonly object _snapshotLock = new();
    private List<DnsSnapshot> _dnsSnapshots = new();

    public MainWindow()
    {
        InitializeComponent();
        DnsList.ItemsSource = _visibleProviders;
        SiteTestList.ItemsSource = _siteResults;
        NavView.SelectedItem = NavView.MenuItems[0];
        Activated += MainWindow_Activated;
        AppWindow.Closing += AppWindow_Closing;
        Closed += MainWindow_Closed;
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 760;
            presenter.PreferredMinimumHeight = 560;
        }
        try { SystemEvents.SessionEnding += SystemEvents_SessionEnding; } catch { }
        _catalogTimer.Interval = TimeSpan.FromMinutes(30);
        _catalogTimer.Tick += CatalogTimer_Tick;
        _autoDnsTimer.Interval = TimeSpan.FromMinutes(10);
        _autoDnsTimer.Tick += AutoDnsTimer_Tick;

        _tray.ShowRequested += () => RootGrid.DispatcherQueue.TryEnqueue(ShowFromTray);
        _tray.RunSmartNowRequested += () => RootGrid.DispatcherQueue.TryEnqueue(async () => await RunSmartAutoCycleAsync(silent: false, forceSelection: true));
        _tray.RestoreRequested += () => RootGrid.DispatcherQueue.TryEnqueue(async () => await RestoreAllFromTrayAsync());
        _tray.ExitRequested += () => RootGrid.DispatcherQueue.TryEnqueue(ExitFromTray);
        _tray.ProviderRequested += provider => RootGrid.DispatcherQueue.TryEnqueue(async () => await ConnectAsync(provider, silent: true, origin: "Quick Switch"));
        _tray.SmartAutoChanged += enabled => RootGrid.DispatcherQueue.TryEnqueue(async () => await SetSmartAutoEnabledAsync(enabled, fromTray: true));
    }


    private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var compact = e.NewSize.Width < 880;
        var veryCompact = e.NewSize.Width < 680;

        if (HomeSplitGrid is not null && AdapterCard is not null && RecommendationCard is not null)
        {
            if (compact)
            {
                HomeSplitGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                HomeSplitGrid.ColumnDefinitions[1].Width = new GridLength(0);
                Grid.SetColumn(RecommendationCard, 0);
                Grid.SetRow(RecommendationCard, 0);
                Grid.SetColumn(AdapterCard, 0);
                Grid.SetRow(AdapterCard, 1);
            }
            else
            {
                HomeSplitGrid.ColumnDefinitions[0].Width = new GridLength(2, GridUnitType.Star);
                HomeSplitGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
                Grid.SetColumn(RecommendationCard, 0);
                Grid.SetRow(RecommendationCard, 0);
                Grid.SetColumn(AdapterCard, 1);
                Grid.SetRow(AdapterCard, 0);
            }
        }

        if (HeroVisual is not null)
            HeroVisual.Visibility = veryCompact ? Visibility.Collapsed : Visibility.Visible;

        if (HeroButtonsPanel is not null)
            HeroButtonsPanel.Orientation = veryCompact ? Orientation.Vertical : Orientation.Horizontal;
    }

    private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (_activationStarted) return;
        _activationStarted = true;
        await InitializeAsync();
        _initializing = false;
    }

    private async Task InitializeAsync()
    {
        try
        {
            _settings = await _store.LoadSettingsAsync();
            if (string.IsNullOrWhiteSpace(_settings.UpdateUrl))
                _settings.UpdateUrl = AppSettings.DefaultGithubUpdateUrl;
            _dnsSnapshots = await _store.LoadDnsSnapshotsAsync();
            _providers = await _store.LoadProvidersAsync();
            _targets = await _store.LoadTargetsAsync();
            UpdateCoverageCounters();

            RootGrid.FontFamily = new FontFamily(_settings.FontFamily);
            SelectFontInCombo(_settings.FontFamily);
            ApplyTheme(_settings.ThemeName);
            AutoUpdateToggle.IsOn = _settings.AutoUpdate;
            RestoreOnExitToggle.IsOn = _settings.RestoreDnsOnExit;
            CrashRecoveryToggle.IsOn = _settings.RecoverDnsAfterUnexpectedExit;
            CrashRecoveryToggle.IsEnabled = _settings.RestoreDnsOnExit;
            SmartAutoToggle.IsOn = _settings.SmartAutoDnsEnabled;
            SelectSmartProfile(_settings.SmartAutoDnsProfile);
            SelectSmartInterval(_settings.SmartAutoDnsIntervalMinutes);
            SelectSmartMinimumScore(_settings.SmartAutoDnsMinimumScore);
            SelectSmartFailures(_settings.SmartAutoDnsFailuresBeforeSwitch);
            TrayEnabledToggle.IsOn = _settings.TrayEnabled;
            CloseToTrayToggle.IsOn = _settings.CloseToTray;
            CloseToTrayToggle.IsEnabled = _settings.TrayEnabled;
            UpdateUrlBox.Text = _settings.UpdateUrl;
            SelectUpdateInterval(_settings.AutoUpdateHours);
            UpdateLastCatalogText();

            // A remaining snapshot means the previous process could not restore it
            // (for example a crash, forced termination or power loss).
            if (_settings.RestoreDnsOnExit && _settings.RecoverDnsAfterUnexpectedExit && _dnsSnapshots.Count > 0)
                await RestoreAllSnapshotsAsync(silent: true);

            var adapters = _network.GetUsableAdapters();
            AdapterCombo.ItemsSource = adapters;
            _selectedAdapter = adapters.FirstOrDefault(a => a.Id == _settings.LastAdapterId) ?? adapters.FirstOrDefault();
            AdapterCombo.SelectedItem = _selectedAdapter;
            RefreshCurrentDns();
            RefreshList();
            InitializeTray();
            ConfigureAutoDnsTimer();
            UpdateSmartAutoUi("آماده");

            if (_settings.AutoUpdate && !string.IsNullOrWhiteSpace(_settings.UpdateUrl))
            {
                var due = _settings.LastCatalogUpdate is null || DateTimeOffset.Now - _settings.LastCatalogUpdate > TimeSpan.FromHours(Math.Max(1, _settings.AutoUpdateHours));
                if (due) _ = TryUpdateCatalogAsync(silent: true);
            }
            _catalogTimer.Start();
        }
        catch (Exception ex)
        {
            ShowInfo("خطا در راه‌اندازی", ex.Message, InfoBarSeverity.Error);
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is not string tag) return;
        HomePage.Visibility = tag == "home" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = tag == "settings" ? Visibility.Visible : Visibility.Collapsed;
        SiteTestPage.Visibility = tag == "site" ? Visibility.Visible : Visibility.Collapsed;
        ListPage.Visibility = tag is "ai" or "dev" or "game" or "all" ? Visibility.Visible : Visibility.Collapsed;

        if (tag is "ai" or "dev" or "game" or "all")
        {
            _filter = tag;
            ListTitle.Text = tag switch
            {
                "ai" => "DNS برای هوش مصنوعی",
                "dev" => "DNS برای برنامه‌نویسی",
                "game" => "DNS برای بازی",
                _ => "همه DNSها"
            };
            ListSubtitle.Text = tag switch
            {
                "ai" => "دسترسی واقعی ChatGPT، Gemini، Claude و APIهای اصلی را با هر DNS مقایسه کن.",
                "dev" => "GitHub، Docker، npm، PyPI، NuGet و ابزارهای توسعه با هر DNS تست می‌شوند.",
                "game" => "پلتفرم‌های اصلی بازی با چند Endpoint تست می‌شوند؛ سبز یعنی مسیر اصلی سرویس قابل دسترسی است.",
                _ => "نتایج کلی براساس سرعت DNS، Ping، Packet Loss و دسترسی واقعی سرویس‌ها مرتب می‌شوند."
            };
            UpdateListCoverageText();
            RefreshList();
        }
    }

    private void RefreshList()
    {
        IEnumerable<DnsProvider> source = _providers;
        if (_filter != "all")
            source = source.Where(p => p.Categories.Contains(_filter, StringComparer.OrdinalIgnoreCase) || p.Categories.Contains("all", StringComparer.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var q = _searchText.Trim();
            source = source.Where(p =>
                p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.Primary.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (p.Secondary?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                p.SourceText.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = source.OrderByDescending(x => x.Score).ThenBy(x => x.PingMs ?? double.MaxValue).ThenBy(x => x.Name).ToList();
        _visibleProviders.Clear();
        foreach (var p in ordered) _visibleProviders.Add(p);
        ProviderCountText.Text = _providers.Count.ToString();
        RefreshActiveFlags();
        UpdateTrayState();
    }

    private void DnsSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchText = DnsSearchBox.Text ?? string.Empty;
        RefreshList();
    }

    private void UpdateCoverageCounters()
    {
        var ai = _targets.Count(x => x.Category.Equals("ai", StringComparison.OrdinalIgnoreCase));
        var dev = _targets.Count(x => x.Category.Equals("dev", StringComparison.OrdinalIgnoreCase));
        var gameTargets = _targets.Where(x => x.Category.Equals("game", StringComparison.OrdinalIgnoreCase)).ToList();
        var gamePlatforms = gameTargets.Select(x => string.IsNullOrWhiteSpace(x.Group) ? x.Name : x.Group!)
            .Distinct(StringComparer.OrdinalIgnoreCase).Count();

        AiTargetCountText.Text = $"{ai} مسیر برای سرویس‌های AI";
        DevTargetCountText.Text = $"{dev} مسیر برای ابزارهای توسعه";
        GameTargetCountText.Text = $"{gamePlatforms} پلتفرم • {gameTargets.Count} Endpoint";
        ProviderCountText.Text = _providers.Count.ToString();
        UpdateListCoverageText();
    }

    private void UpdateListCoverageText()
    {
        if (ListCoverageText is null) return;
        if (_filter == "game")
        {
            var gameTargets = _targets.Where(x => x.Category.Equals("game", StringComparison.OrdinalIgnoreCase)).ToList();
            var groups = gameTargets.Select(x => string.IsNullOrWhiteSpace(x.Group) ? x.Name : x.Group!)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            ListCoverageText.Text = $"{groups.Count} پلتفرم • {gameTargets.Count} Endpoint • " + string.Join(" • ", groups);
        }
        else if (_filter == "ai")
            ListCoverageText.Text = $"{_targets.Count(x => x.Category.Equals("ai", StringComparison.OrdinalIgnoreCase))} Endpoint هوش مصنوعی";
        else if (_filter == "dev")
            ListCoverageText.Text = $"{_targets.Count(x => x.Category.Equals("dev", StringComparison.OrdinalIgnoreCase))} Endpoint برنامه‌نویسی";
        else
            ListCoverageText.Text = $"{_providers.Count} DNS • {_targets.Count} Endpoint در همه دسته‌ها";
    }

    private void RefreshCurrentDns()
    {
        if (_selectedAdapter is null)
        {
            CurrentDnsText.Text = "کارت شبکه پیدا نشد";
            AdapterSummaryText.Text = "—";
            return;
        }
        AdapterSummaryText.Text = _selectedAdapter.Name;
        var dns = _network.GetCurrentDns(_selectedAdapter.Id);
        CurrentDnsText.Text = dns.Count == 0 ? "خودکار / نامشخص" : string.Join(" • ", dns);
        RefreshActiveFlags();
        UpdateTrayState();
    }

    private void RefreshActiveFlags()
    {
        var active = _selectedAdapter is null ? Array.Empty<string>() : _network.GetCurrentDns(_selectedAdapter.Id).ToArray();
        foreach (var p in _providers) p.IsActive = active.Contains(p.Primary, StringComparer.OrdinalIgnoreCase);
    }

    private IReadOnlyList<ServiceTarget> TargetsForFilter(string filter)
    {
        if (filter == "ai") return _targets.Where(t => t.Category == "ai").ToList();
        if (filter == "dev") return _targets.Where(t => t.Category == "dev").ToList();
        if (filter == "game") return _targets.Where(t => t.Category == "game").ToList();
        return _targets;
    }


    private async void TestSite_Click(object sender, RoutedEventArgs e)
    {
        if (!TryNormalizeSiteAddress(SiteAddressBox.Text, out var uri, out var error))
        {
            ShowInfo("آدرس سایت معتبر نیست", error, InfoBarSeverity.Warning);
            return;
        }

        if (_providers.Count == 0)
        {
            ShowInfo("DNS پیدا نشد", "فهرست DNS خالی است.", InfoBarSeverity.Warning);
            return;
        }

        SiteAddressBox.Text = uri!.AbsoluteUri;
        SiteTestButton.IsEnabled = false;
        SiteEmptyState.Visibility = Visibility.Collapsed;
        MainInfoBar.IsOpen = false;
        _siteResults.Clear();
        foreach (var provider in _providers)
            _siteResults.Add(new SiteDnsTestResult(provider));
        UpdateSiteSummary();

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var semaphore = new SemaphoreSlim(4);
            var tasks = _siteResults.Select(async result =>
            {
                await semaphore.WaitAsync(cts.Token);
                try
                {
                    result.Start();
                    var probe = await _siteProbe.ProbeAsync(result.Provider.Primary, uri, cts.Token);
                    result.Complete(probe);
                }
                catch (OperationCanceledException)
                {
                    result.Complete(new SiteProbeResult(false, false, 0, 0, 0, Array.Empty<System.Net.IPAddress>(), null, "تست لغو شد یا زمان آن به پایان رسید"));
                }
                catch (Exception ex)
                {
                    result.Complete(new SiteProbeResult(false, false, 0, 0, 0, Array.Empty<System.Net.IPAddress>(), null, ex.Message));
                }
                finally
                {
                    semaphore.Release();
                    UpdateSiteSummary();
                }
            }).ToArray();

            await Task.WhenAll(tasks);

            var ordered = _siteResults
                .OrderByDescending(x => x.IsSuccess)
                .ThenBy(x => x.HttpMs ?? double.MaxValue)
                .ThenBy(x => x.ProviderName)
                .ToList();
            _siteResults.Clear();
            foreach (var item in ordered) _siteResults.Add(item);
            UpdateSiteSummary();

            var green = ordered.Count(x => x.IsSuccess);
            ShowInfo(
                green > 0 ? "تست سایت کامل شد" : "DNS موفق پیدا نشد",
                green > 0
                    ? $"{green} DNS توانستند {uri.Host} را باز کنند. موارد سبز در بالای لیست قرار گرفتند."
                    : $"هیچ‌کدام از DNSهای فعلی نتوانستند {uri.Host} را با موفقیت باز کنند.",
                green > 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
        }
        finally
        {
            SiteTestButton.IsEnabled = true;
        }
    }

    private async void ConnectSiteResult_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is SiteDnsTestResult result)
            await ConnectAsync(result.Provider);
    }

    private void UpdateSiteSummary()
    {
        var completed = _siteResults.Where(x => x.IsCompleted).ToList();
        var green = completed.Count(x => x.IsSuccess);
        var red = completed.Count - green;
        var fastest = completed
            .Where(x => x.IsSuccess && x.HttpMs.HasValue)
            .OrderBy(x => x.HttpMs)
            .FirstOrDefault();

        SiteTestedCountText.Text = $"{completed.Count}/{_siteResults.Count}";
        SiteGreenCountText.Text = green.ToString();
        SiteRedCountText.Text = red.ToString();
        SiteFastestText.Text = fastest is null ? "—" : $"{fastest.ProviderName} • {fastest.HttpMs:0} ms";
    }

    private static bool TryNormalizeSiteAddress(string? input, out Uri? uri, out string error)
    {
        uri = null;
        error = string.Empty;
        var value = input?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            error = "یک دامنه مثل google.com وارد کن.";
            return false;
        }

        if (!value.Contains("://", StringComparison.Ordinal))
            value = "https://" + value;

        if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || string.IsNullOrWhiteSpace(uri.Host))
        {
            error = "نمونه صحیح: google.com یا https://google.com";
            uri = null;
            return false;
        }

        if (uri.Scheme is not "http" and not "https")
        {
            error = "فقط آدرس‌های HTTP و HTTPS قابل تست هستند.";
            uri = null;
            return false;
        }

        return true;
    }

    private async void TestAll_Click(object sender, RoutedEventArgs e) => await TestProvidersAsync(_providers, _targets, "در حال تست همه DNSها…");
    private async void TestVisible_Click(object sender, RoutedEventArgs e) => await TestProvidersAsync(_visibleProviders.ToList(), TargetsForFilter(_filter), "در حال تست DNSهای این بخش…");

    private async Task TestProvidersAsync(IReadOnlyList<DnsProvider> providers, IReadOnlyList<ServiceTarget> targets, string message)
    {
        if (providers.Count == 0) return;
        SetBusy(true, message);
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var semaphore = new SemaphoreSlim(4);
            var tasks = providers.Select(async p =>
            {
                await semaphore.WaitAsync(cts.Token);
                try { await _benchmark.TestProviderAsync(p, targets, cts.Token); }
                catch (Exception ex) { p.Status = $"خطا: {ex.Message}"; }
                finally { semaphore.Release(); }
            });
            await Task.WhenAll(tasks);

            _best = providers.OrderByDescending(p => p.Score).ThenBy(p => p.PingMs ?? double.MaxValue).FirstOrDefault();
            UpdateBestCard();
            RefreshList();
        }
        finally { SetBusy(false); }
    }

    private async void TestProvider_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not DnsProvider provider) return;
        SetBusy(true, $"در حال تست {provider.Name}…");
        try
        {
            await _benchmark.TestProviderAsync(provider, TargetsForFilter(_filter), CancellationToken.None);
            if (_best is null || provider.Score > _best.Score) _best = provider;
            UpdateBestCard();
            RefreshList();
        }
        finally { SetBusy(false); }
    }

    private async void ConnectProvider_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is DnsProvider provider)
            await ConnectAsync(provider);
    }

    private async void ConnectBest_Click(object sender, RoutedEventArgs e)
    {
        if (_best is null)
        {
            ShowInfo("هنوز بهترین DNS مشخص نشده", "ابتدا تست همه DNSها را اجرا کن.", InfoBarSeverity.Warning);
            return;
        }
        await ConnectAsync(_best);
    }

    private async Task<bool> ConnectAsync(DnsProvider provider, bool silent = false, string? origin = null)
    {
        if (_selectedAdapter is null)
        {
            if (!silent) ShowInfo("کارت شبکه انتخاب نشده", "یک کارت شبکه فعال انتخاب کن.", InfoBarSeverity.Warning);
            return false;
        }
        if (!silent) SetBusy(true, $"در حال اعمال {provider.Name}…");
        try
        {
            await EnsureSnapshotAsync(_selectedAdapter);
            await _network.ApplyAsync(_selectedAdapter, provider);
            RefreshCurrentDns();
            if (!silent)
                ShowInfo("DNS تغییر کرد", $"{provider.Name} روی {_selectedAdapter.Name} فعال شد.", InfoBarSeverity.Success);
            else if (!string.IsNullOrWhiteSpace(origin))
                _tray.Notify("DNSYar", $"{provider.Name} فعال شد • {origin}");
            return true;
        }
        catch (Exception ex)
        {
            if (!silent) ShowInfo("تغییر DNS ناموفق بود", ex.Message, InfoBarSeverity.Error);
            else _tray.Notify("DNSYar — خطا", ex.Message, Forms.ToolTipIcon.Error);
            return false;
        }
        finally { if (!silent) SetBusy(false); }
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedAdapter is null) return;
        var snapshot = GetSnapshot(_selectedAdapter.Id);
        if (snapshot is null)
        {
            ShowInfo("تنظیم قبلی ثبت نشده", "DNSYar برای این کارت شبکه هنوز DNS را تغییر نداده است؛ بنابراین چیزی را حدس نمی‌زند یا به اجبار DHCP نمی‌کند.", InfoBarSeverity.Warning);
            return;
        }

        SetBusy(true, "در حال بازگردانی تنظیم DNS قبلی…");
        try
        {
            await _network.RestoreSnapshotAsync(snapshot);
            RemoveSnapshot(snapshot.AdapterId);
            await PersistSnapshotsAsync();
            RefreshCurrentDns();
            var mode = snapshot.IsDhcp ? "حالت خودکار (DHCP)" : string.Join(" • ", snapshot.Servers);
            ShowInfo("DNS قبلی بازگردانده شد", $"تنظیم قبلی کارت شبکه بازیابی شد: {mode}", InfoBarSeverity.Success);
        }
        catch (Exception ex) { ShowInfo("بازگردانی ناموفق بود", ex.Message, InfoBarSeverity.Error); }
        finally { SetBusy(false); }
    }

    private async void RestoreAll_Click(object sender, RoutedEventArgs e)
    {
        if (SnapshotCount == 0)
        {
            ShowInfo("چیزی برای بازگردانی نیست", "هیچ تنظیم DNS قبلی توسط DNSYar ذخیره نشده است.", InfoBarSeverity.Informational);
            return;
        }
        SetBusy(true, "در حال بازگردانی همه تنظیمات DNS…");
        try { await RestoreAllSnapshotsAsync(silent: false); RefreshCurrentDns(); }
        finally { SetBusy(false); }
    }

    private async void AdapterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedAdapter = AdapterCombo.SelectedItem as AdapterInfo;
        if (_selectedAdapter is null) return;
        _settings.LastAdapterId = _selectedAdapter.Id;
        if (!_initializing) await _store.SaveSettingsAsync(_settings);
        RefreshCurrentDns();
    }

    private int SnapshotCount
    {
        get { lock (_snapshotLock) return _dnsSnapshots.Count; }
    }

    private DnsSnapshot? GetSnapshot(string adapterId)
    {
        lock (_snapshotLock)
            return _dnsSnapshots.FirstOrDefault(x => string.Equals(x.AdapterId, adapterId, StringComparison.OrdinalIgnoreCase));
    }

    private async Task EnsureSnapshotAsync(AdapterInfo adapter)
    {
        if (GetSnapshot(adapter.Id) is not null) return;
        var snapshot = _network.CaptureCurrentState(adapter);
        lock (_snapshotLock)
        {
            if (_dnsSnapshots.All(x => !string.Equals(x.AdapterId, adapter.Id, StringComparison.OrdinalIgnoreCase)))
                _dnsSnapshots.Add(snapshot);
        }
        await PersistSnapshotsAsync();
    }

    private void RemoveSnapshot(string adapterId)
    {
        lock (_snapshotLock)
            _dnsSnapshots.RemoveAll(x => string.Equals(x.AdapterId, adapterId, StringComparison.OrdinalIgnoreCase));
    }

    private List<DnsSnapshot> SnapshotCopy()
    {
        lock (_snapshotLock)
            return _dnsSnapshots.Select(x => new DnsSnapshot
            {
                AdapterId = x.AdapterId,
                AdapterName = x.AdapterName,
                IsDhcp = x.IsDhcp,
                Servers = x.Servers.ToList(),
                CapturedAt = x.CapturedAt
            }).ToList();
    }

    private Task PersistSnapshotsAsync() => _store.SaveDnsSnapshotsAsync(SnapshotCopy());

    private async Task RestoreAllSnapshotsAsync(bool silent)
    {
        var restored = 0;
        var failures = new List<string>();
        foreach (var snapshot in SnapshotCopy())
        {
            try
            {
                await _network.RestoreSnapshotAsync(snapshot);
                RemoveSnapshot(snapshot.AdapterId);
                restored++;
            }
            catch (Exception ex)
            {
                failures.Add($"{snapshot.AdapterName}: {ex.Message}");
            }
        }
        await PersistSnapshotsAsync();

        if (!silent)
        {
            if (failures.Count == 0)
                ShowInfo("بازگردانی کامل شد", $"تنظیم قبلی {restored} کارت شبکه با موفقیت بازیابی شد.", InfoBarSeverity.Success);
            else
                ShowInfo("بازگردانی ناقص بود", $"{restored} مورد بازیابی شد. {failures.Count} مورد خطا داشت: {string.Join(" | ", failures)}", InfoBarSeverity.Warning);
        }
    }

    private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose || _initializing) return;

        // Optional tray behavior: closing the window can mean "hide", not "exit".
        // It is OFF by default so the previous safe-restore behavior remains unchanged.
        if (_settings.TrayEnabled && _settings.CloseToTray && !_closeRequestedFromTray)
        {
            args.Cancel = true;
            AppWindow.Hide();
            _tray.Notify("DNSYar همچنان فعال است", "برنامه در System Tray اجرا می‌شود. برای خروج کامل از منوی Tray استفاده کن.");
            return;
        }

        if (!_settings.RestoreDnsOnExit || SnapshotCount == 0) return;
        args.Cancel = true;
        if (_closeRestoreInProgress) return;

        _closeRestoreInProgress = true;
        try
        {
            await RestoreAllSnapshotsAsync(silent: true);
        }
        finally
        {
            _allowClose = true;
            _closeRestoreInProgress = false;
            Close();
        }
    }

    private void SystemEvents_SessionEnding(object? sender, SessionEndingEventArgs e)
    {
        _closeRequestedFromTray = true; // Prevent close-to-tray from hiding the window during Windows shutdown/logoff.
        if (!_settings.RestoreDnsOnExit || SnapshotCount == 0) return;
        var remaining = new List<DnsSnapshot>();
        foreach (var snapshot in SnapshotCopy())
        {
            try { _network.RestoreSnapshot(snapshot); }
            catch { remaining.Add(snapshot); }
        }
        lock (_snapshotLock) _dnsSnapshots = remaining;
        try { _store.SaveDnsSnapshots(remaining); } catch { }
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _catalogTimer.Stop();
        _autoDnsTimer.Stop();
        _tray.Dispose();
        try { SystemEvents.SessionEnding -= SystemEvents_SessionEnding; } catch { }
    }

    private void UpdateBestCard()
    {
        if (_best is null) return;
        BestNameText.Text = _best.Name;
        BestRecommendationText.Text = _best.Recommendation;
        BestPingText.Text = $"Ping: {_best.PingText}";
        BestDnsText.Text = $"DNS: {_best.DnsText}";
        BestServicesText.Text = $"Services: {_best.ServiceText}";
        BestScoreText.Text = _best.ScoreText;
    }

    private void SetBusy(bool busy, string? text = null)
    {
        BusyOverlay.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        if (text is not null) BusyText.Text = text;
    }

    private void ShowInfo(string title, string message, InfoBarSeverity severity)
    {
        MainInfoBar.Title = title;
        MainInfoBar.Message = message;
        MainInfoBar.Severity = severity;
        MainInfoBar.IsOpen = true;
    }

    private async void ManualUpdate_Click(object sender, RoutedEventArgs e) => await TryUpdateCatalogAsync(silent: false);

    private async Task TryUpdateCatalogAsync(bool silent)
    {
        if (_catalogUpdateInProgress)
        {
            if (!silent) ShowInfo("بروزرسانی در حال اجراست", "تا پایان دریافت فعلی نیازی به اجرای دوباره نیست.", InfoBarSeverity.Informational);
            return;
        }
        if (string.IsNullOrWhiteSpace(_settings.UpdateUrl))
        {
            if (!silent) ShowInfo("آدرس بروزرسانی خالی است", "در تنظیمات، لینک فایل GitHub را وارد کن یا روی «منبع پیش‌فرض» بزن.", InfoBarSeverity.Warning);
            return;
        }

        _catalogUpdateInProgress = true;
        if (!silent) SetBusy(true, "در حال دریافت و ادغام DNSهای GitHub…");
        try
        {
            var result = await _updater.DownloadAsync(_settings.UpdateUrl);
            var custom = _providers.Where(x => x.IsCustom).ToList();
            var managed = _providers.Where(x => !x.IsCustom).ToList();
            var added = 0;
            var updated = 0;

            foreach (var incoming in result.Providers)
            {
                var existing = managed.FirstOrDefault(x =>
                    x.Primary.Equals(incoming.Primary, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(incoming.Id) && x.Id.Equals(incoming.Id, StringComparison.OrdinalIgnoreCase)));

                if (existing is null)
                {
                    managed.Add(incoming);
                    added++;
                    continue;
                }

                existing.Name = incoming.Name;
                existing.Secondary = incoming.Secondary;
                existing.Source = incoming.Source;
                if (!string.IsNullOrWhiteSpace(incoming.DoH)) existing.DoH = incoming.DoH;
                if (!string.IsNullOrWhiteSpace(incoming.Description)) existing.Description = incoming.Description;
                existing.Categories = existing.Categories
                    .Concat(incoming.Categories)
                    .Append("all")
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                updated++;
            }

            _providers = managed.Concat(custom).ToList();
            await _store.SaveProvidersAsync(managed);
            _settings.LastCatalogUpdate = DateTimeOffset.Now;
            _settings.UpdateUrl = result.SourceUrl;
            await _store.SaveSettingsAsync(_settings);
            UpdateUrlBox.Text = _settings.UpdateUrl;
            UpdateLastCatalogText();
            UpdateCoverageCounters();
            RefreshList();

            if (!silent)
                ShowInfo("بروزرسانی GitHub انجام شد", $"{added} DNS جدید اضافه شد، {updated} مورد بروزرسانی شد. فرمت منبع: {result.Format}. DNSهای دستی حفظ شدند.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            if (!silent) ShowInfo("بروزرسانی ناموفق بود", ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            _catalogUpdateInProgress = false;
            if (!silent) SetBusy(false);
        }
    }


    private async void AddCustomDns_Click(object sender, RoutedEventArgs e)
    {
        var nameBox = new TextBox { Header = "نام DNS", PlaceholderText = "مثال: DNS شرکت من" };
        var primaryBox = new TextBox { Header = "Primary IPv4", PlaceholderText = "مثال: 1.2.3.4", FlowDirection = FlowDirection.LeftToRight };
        var secondaryBox = new TextBox { Header = "Secondary IPv4 (اختیاری)", PlaceholderText = "مثال: 1.2.3.5", FlowDirection = FlowDirection.LeftToRight };
        var dohBox = new TextBox { Header = "DoH URL (اختیاری)", PlaceholderText = "https://.../dns-query", FlowDirection = FlowDirection.LeftToRight };
        var categoryBox = new ComboBox { Header = "نمایش در بخش", HorizontalAlignment = HorizontalAlignment.Stretch };
        categoryBox.Items.Add(new ComboBoxItem { Content = "همه بخش‌ها", Tag = "all" });
        categoryBox.Items.Add(new ComboBoxItem { Content = "هوش مصنوعی", Tag = "ai" });
        categoryBox.Items.Add(new ComboBoxItem { Content = "برنامه‌نویسی", Tag = "dev" });
        categoryBox.Items.Add(new ComboBoxItem { Content = "بازی", Tag = "game" });
        categoryBox.SelectedIndex = 0;

        var panel = new StackPanel { Spacing = 10, MinWidth = 410 };
        panel.Children.Add(nameBox);
        panel.Children.Add(primaryBox);
        panel.Children.Add(secondaryBox);
        panel.Children.Add(dohBox);
        panel.Children.Add(categoryBox);
        panel.Children.Add(new TextBlock
        {
            Text = "DNS دستی فقط روی همین سیستم ذخیره می‌شود و بروزرسانی GitHub آن را حذف نمی‌کند.",
            Opacity = 0.65,
            TextWrapping = TextWrapping.Wrap
        });

        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = "افزودن DNS دستی",
            Content = panel,
            PrimaryButtonText = "افزودن",
            CloseButtonText = "انصراف",
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var name = nameBox.Text.Trim();
        var primary = primaryBox.Text.Trim();
        var secondary = secondaryBox.Text.Trim();
        var doh = dohBox.Text.Trim();
        var category = (categoryBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "all";

        if (string.IsNullOrWhiteSpace(name))
        {
            ShowInfo("نام DNS خالی است", "برای DNS یک نام وارد کن.", InfoBarSeverity.Warning);
            return;
        }
        if (!IsValidIpv4(primary))
        {
            ShowInfo("Primary نامعتبر است", "یک IPv4 معتبر مثل 10.202.10.202 وارد کن.", InfoBarSeverity.Warning);
            return;
        }
        if (!string.IsNullOrWhiteSpace(secondary) && !IsValidIpv4(secondary))
        {
            ShowInfo("Secondary نامعتبر است", "Secondary را خالی بگذار یا یک IPv4 معتبر وارد کن.", InfoBarSeverity.Warning);
            return;
        }
        if (!string.IsNullOrWhiteSpace(doh) && (!Uri.TryCreate(doh, UriKind.Absolute, out var dohUri) || dohUri.Scheme != "https"))
        {
            ShowInfo("DoH نامعتبر است", "آدرس DoH باید یک URL کامل HTTPS باشد.", InfoBarSeverity.Warning);
            return;
        }
        if (_providers.Any(x => x.Primary.Equals(primary, StringComparison.OrdinalIgnoreCase)))
        {
            ShowInfo("DNS تکراری است", "این Primary از قبل در فهرست وجود دارد.", InfoBarSeverity.Warning);
            return;
        }

        var provider = new DnsProvider
        {
            Id = $"custom-{Guid.NewGuid():N}",
            Name = name,
            Primary = primary,
            Secondary = string.IsNullOrWhiteSpace(secondary) ? null : secondary,
            DoH = string.IsNullOrWhiteSpace(doh) ? null : doh,
            Categories = category == "all" ? new[] { "all" } : new[] { category, "all" },
            Description = "DNS دستی افزوده‌شده توسط کاربر",
            IsCustom = true,
            Source = "دستی"
        };

        _providers.Add(provider);
        await _store.SaveCustomProvidersAsync(_providers.Where(x => x.IsCustom));
        UpdateCoverageCounters();
        RefreshList();
        ShowInfo("DNS اضافه شد", $"{name} ذخیره شد و در تست‌ها و بخش تست سایت استفاده می‌شود.", InfoBarSeverity.Success);
    }

    private static bool IsValidIpv4(string value) =>
        IPAddress.TryParse(value, out var ip) && ip.AddressFamily == AddressFamily.InterNetwork && !ip.Equals(IPAddress.Any);

    private async void ResetGithubSource_Click(object sender, RoutedEventArgs e)
    {
        _settings.UpdateUrl = AppSettings.DefaultGithubUpdateUrl;
        UpdateUrlBox.Text = _settings.UpdateUrl;
        await _store.SaveSettingsAsync(_settings);
        ShowInfo("منبع GitHub بازگردانی شد", "منبع پیش‌فرض DNSYar فعال شد.", InfoBarSeverity.Success);
    }

    private async void UpdateIntervalCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        if (UpdateIntervalCombo.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var hours))
        {
            _settings.AutoUpdateHours = hours;
            await _store.SaveSettingsAsync(_settings);
        }
    }

    private void SelectUpdateInterval(int hours)
    {
        var best = UpdateIntervalCombo.Items.OfType<ComboBoxItem>()
            .OrderBy(x => Math.Abs((int.TryParse(x.Tag?.ToString(), out var h) ? h : 24) - hours))
            .FirstOrDefault();
        UpdateIntervalCombo.SelectedItem = best ?? UpdateIntervalCombo.Items[2];
    }

    private void UpdateLastCatalogText()
    {
        LastUpdateText.Text = _settings.LastCatalogUpdate is null
            ? "آخرین بروزرسانی: هنوز انجام نشده"
            : $"آخرین بروزرسانی: {_settings.LastCatalogUpdate.Value.LocalDateTime:yyyy/MM/dd HH:mm}";
    }

    private async void CatalogTimer_Tick(object? sender, object e)
    {
        if (!_settings.AutoUpdate || string.IsNullOrWhiteSpace(_settings.UpdateUrl)) return;
        var due = _settings.LastCatalogUpdate is null || DateTimeOffset.Now - _settings.LastCatalogUpdate > TimeSpan.FromHours(Math.Max(1, _settings.AutoUpdateHours));
        if (due) await TryUpdateCatalogAsync(silent: true);
    }

    private async void AutoUpdateToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        _settings.AutoUpdate = AutoUpdateToggle.IsOn;
        await _store.SaveSettingsAsync(_settings);
    }

    private async void UpdateUrlBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_initializing) return;
        _settings.UpdateUrl = UpdateUrlBox.Text.Trim();
        await _store.SaveSettingsAsync(_settings);
    }

    private async void RestoreOnExitToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        _settings.RestoreDnsOnExit = RestoreOnExitToggle.IsOn;
        CrashRecoveryToggle.IsEnabled = RestoreOnExitToggle.IsOn;
        await _store.SaveSettingsAsync(_settings);
    }

    private async void CrashRecoveryToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        _settings.RecoverDnsAfterUnexpectedExit = CrashRecoveryToggle.IsOn;
        await _store.SaveSettingsAsync(_settings);
    }

    private void InitializeTray()
    {
        if (!_settings.TrayEnabled) return;
        _tray.Initialize(_settings.SmartAutoDnsEnabled);
        _tray.SetVisible(true);
        UpdateTrayState();
    }

    private void UpdateTrayState()
    {
        if (!_settings.TrayEnabled || !_tray.IsInitialized) return;
        var active = _providers.FirstOrDefault(x => x.IsActive)?.Name ?? "خودکار / ناشناس";
        _tray.Update(active, _settings.SmartAutoDnsEnabled, _providers);
    }

    private void ShowFromTray()
    {
        AppWindow.Show(true);
        Activate();
    }

    private async Task RestoreAllFromTrayAsync()
    {
        if (SnapshotCount == 0)
        {
            _tray.Notify("DNSYar", "تنظیم قبلی ذخیره‌شده‌ای برای بازگردانی وجود ندارد.");
            return;
        }
        await RestoreAllSnapshotsAsync(silent: true);
        RefreshCurrentDns();
        _tray.Notify("DNSYar", "تنظیم DNS قبلی با موفقیت بازگردانده شد.");
    }

    private void ExitFromTray()
    {
        _closeRequestedFromTray = true;
        Close();
    }

    private void ConfigureAutoDnsTimer()
    {
        _autoDnsTimer.Stop();
        _autoDnsTimer.Interval = TimeSpan.FromMinutes(Math.Clamp(_settings.SmartAutoDnsIntervalMinutes, 5, 120));
        if (_settings.SmartAutoDnsEnabled) _autoDnsTimer.Start();
    }

    private async void AutoDnsTimer_Tick(object? sender, object e) => await RunSmartAutoCycleAsync(silent: true, forceSelection: false);

    private IReadOnlyList<ServiceTarget> AutoTargetsForProfile(string profile)
    {
        if (profile == "game")
            return _targets.Where(x => x.Category == "game" && x.IsPrimary).ToList();

        if (profile == "ai")
        {
            var ids = new[] { "chatgpt", "openai-api", "gemini", "gemini-api", "claude", "perplexity" };
            return _targets.Where(x => ids.Contains(x.Id, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        if (profile == "dev")
        {
            var ids = new[] { "github", "github-api", "github-raw", "docker", "npm", "pypi", "nuget" };
            return _targets.Where(x => ids.Contains(x.Id, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        var allIds = new[] { "chatgpt", "gemini", "github", "github-raw", "docker", "npm", "steam-store", "epic-store", "xbox-home", "playstation-home", "riot-home", "discord-home" };
        return _targets.Where(x => allIds.Contains(x.Id, StringComparer.OrdinalIgnoreCase)).ToList();
    }

    private List<DnsProvider> AutoProvidersForProfile(string profile)
    {
        if (profile == "all") return _providers.ToList();
        return _providers.Where(p =>
            p.Categories.Contains(profile, StringComparer.OrdinalIgnoreCase) ||
            p.Categories.Contains("all", StringComparer.OrdinalIgnoreCase)).ToList();
    }

    private async Task RunSmartAutoCycleAsync(bool silent, bool forceSelection)
    {
        if (_smartAutoInProgress || _selectedAdapter is null) return;
        if (!_settings.SmartAutoDnsEnabled && !forceSelection) return;

        _smartAutoInProgress = true;
        if (!silent) SetBusy(true, "Smart Auto DNS در حال بررسی وضعیت و انتخاب بهترین DNS…");
        try
        {
            var profile = _settings.SmartAutoDnsProfile;
            var targets = AutoTargetsForProfile(profile);
            var candidates = AutoProvidersForProfile(profile);
            if (candidates.Count == 0 || targets.Count == 0)
            {
                UpdateSmartAutoUi("برای این پروفایل DNS یا Endpoint کافی وجود ندارد.");
                return;
            }

            var currentDns = _network.GetCurrentDns(_selectedAdapter.Id);
            var active = candidates.FirstOrDefault(p => currentDns.Contains(p.Primary, StringComparer.OrdinalIgnoreCase));

            if (active is not null && !forceSelection)
            {
                await _benchmark.TestProviderAsync(active, targets, CancellationToken.None);
                var rate = active.TotalServices == 0 ? 0 : active.ReachableServices / (double)active.TotalServices;
                var healthy = active.Score >= _settings.SmartAutoDnsMinimumScore && rate >= .60 && active.PacketLoss < 45;
                RefreshList();

                if (healthy)
                {
                    _smartAutoFailureStreak = 0;
                    UpdateSmartAutoUi($"{active.Name} سالم است • امتیاز {active.Score}/100 • تغییر لازم نیست");
                    return;
                }

                _smartAutoFailureStreak++;
                if (_smartAutoFailureStreak < Math.Max(1, _settings.SmartAutoDnsFailuresBeforeSwitch))
                {
                    UpdateSmartAutoUi($"افت کیفیت {active.Name} ثبت شد • {_smartAutoFailureStreak}/{_settings.SmartAutoDnsFailuresBeforeSwitch} تا Failover");
                    return;
                }
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var semaphore = new SemaphoreSlim(4);
            var tasks = candidates.Select(async provider =>
            {
                await semaphore.WaitAsync(cts.Token);
                try { await _benchmark.TestProviderAsync(provider, targets, cts.Token); }
                catch { provider.Status = "خطا در تست خودکار"; }
                finally { semaphore.Release(); }
            }).ToArray();
            await Task.WhenAll(tasks);

            var best = candidates
                .Where(p => p.Score >= _settings.SmartAutoDnsMinimumScore)
                .Where(p => p.TotalServices > 0 && p.ReachableServices / (double)p.TotalServices >= .60)
                .OrderByDescending(p => p.Score)
                .ThenBy(p => p.PacketLoss)
                .ThenBy(p => p.PingMs ?? double.MaxValue)
                .FirstOrDefault();

            _best = candidates.OrderByDescending(p => p.Score).ThenBy(p => p.PingMs ?? double.MaxValue).FirstOrDefault();
            UpdateBestCard();
            RefreshList();

            if (best is null)
            {
                UpdateSmartAutoUi("هیچ DNS سالمی به حداقل امتیاز تعیین‌شده نرسید؛ DNS فعلی تغییر نکرد.");
                if (!silent) ShowInfo("Smart Auto DNS", "DNS مناسبی برای Failover پیدا نشد و تنظیم فعلی دست‌نخورده ماند.", InfoBarSeverity.Warning);
                return;
            }

            if (active is not null && best.Primary.Equals(active.Primary, StringComparison.OrdinalIgnoreCase))
            {
                _smartAutoFailureStreak = 0;
                UpdateSmartAutoUi($"بهترین گزینه همچنان {best.Name} است • {best.Score}/100");
                return;
            }

            var previous = active?.Name ?? "DNS فعلی";
            var applied = await ConnectAsync(best, silent: true, origin: "Smart Auto DNS");
            if (!applied)
            {
                UpdateSmartAutoUi($"{best.Name} انتخاب شد اما اعمال DNS ناموفق بود.");
                return;
            }
            _smartAutoFailureStreak = 0;
            UpdateSmartAutoUi($"Failover انجام شد: {previous} ← {best.Name} • امتیاز {best.Score}/100");
            _tray.Notify("Smart Auto DNS", $"{previous} → {best.Name}  |  امتیاز {best.Score}/100", Forms.ToolTipIcon.Info);
            if (!silent) ShowInfo("Smart Auto DNS", $"بهترین گزینه انتخاب و فعال شد: {best.Name} ({best.Score}/100)", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            UpdateSmartAutoUi($"خطا: {ex.Message}");
            if (!silent) ShowInfo("Smart Auto DNS ناموفق بود", ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            _smartAutoInProgress = false;
            if (!silent) SetBusy(false);
        }
    }

    private void UpdateSmartAutoUi(string status)
    {
        if (SmartAutoStatusText is null) return;
        var profile = ProfileDisplayName(_settings.SmartAutoDnsProfile);
        SmartAutoStatusText.Text = $"{(_settings.SmartAutoDnsEnabled ? "فعال" : "خاموش")} • {profile} • {status}";
        if (HomeSmartAutoText is not null)
            HomeSmartAutoText.Text = _settings.SmartAutoDnsEnabled ? $"فعال • {profile}" : "خاموش";
    }

    private static string ProfileDisplayName(string profile) => profile switch
    {
        "ai" => "هوش مصنوعی",
        "dev" => "برنامه‌نویسی",
        "game" => "بازی",
        _ => "همه‌کاره"
    };

    private async Task SetSmartAutoEnabledAsync(bool enabled, bool fromTray = false)
    {
        _settings.SmartAutoDnsEnabled = enabled;
        if (SmartAutoToggle.IsOn != enabled) SmartAutoToggle.IsOn = enabled;
        ConfigureAutoDnsTimer();
        await _store.SaveSettingsAsync(_settings);
        UpdateSmartAutoUi(enabled ? "پایش دوره‌ای شروع شد" : "پایش متوقف شد");
        UpdateTrayState();
        if (fromTray) _tray.Notify("Smart Auto DNS", enabled ? "پایش و Failover خودکار فعال شد." : "پایش خودکار غیرفعال شد.");
    }

    private async void SmartAutoToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        await SetSmartAutoEnabledAsync(SmartAutoToggle.IsOn);
    }

    private async void SmartProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SmartProfileCombo.SelectedItem is not ComboBoxItem item || item.Tag is null) return;
        _settings.SmartAutoDnsProfile = item.Tag.ToString() ?? "all";
        if (_initializing) return;
        _smartAutoFailureStreak = 0;
        await _store.SaveSettingsAsync(_settings);
        UpdateSmartAutoUi("پروفایل تغییر کرد");
    }

    private async void SmartIntervalCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SmartIntervalCombo.SelectedItem is not ComboBoxItem item || !int.TryParse(item.Tag?.ToString(), out var minutes)) return;
        _settings.SmartAutoDnsIntervalMinutes = minutes;
        if (_initializing) return;
        ConfigureAutoDnsTimer();
        await _store.SaveSettingsAsync(_settings);
    }

    private async void SmartMinimumScoreCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SmartMinimumScoreCombo.SelectedItem is not ComboBoxItem item || !int.TryParse(item.Tag?.ToString(), out var score)) return;
        _settings.SmartAutoDnsMinimumScore = score;
        if (_initializing) return;
        await _store.SaveSettingsAsync(_settings);
    }

    private async void SmartFailuresCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SmartFailuresCombo.SelectedItem is not ComboBoxItem item || !int.TryParse(item.Tag?.ToString(), out var failures)) return;
        _settings.SmartAutoDnsFailuresBeforeSwitch = failures;
        if (_initializing) return;
        _smartAutoFailureStreak = 0;
        await _store.SaveSettingsAsync(_settings);
    }

    private async void SmartRunNow_Click(object sender, RoutedEventArgs e) => await RunSmartAutoCycleAsync(silent: false, forceSelection: true);

    private void SelectSmartProfile(string profile)
    {
        SmartProfileCombo.SelectedItem = SmartProfileCombo.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(x => string.Equals(x.Tag?.ToString(), profile, StringComparison.OrdinalIgnoreCase)) ?? SmartProfileCombo.Items[0];
    }

    private void SelectSmartInterval(int minutes)
    {
        SmartIntervalCombo.SelectedItem = SmartIntervalCombo.Items.OfType<ComboBoxItem>()
            .OrderBy(x => Math.Abs((int.TryParse(x.Tag?.ToString(), out var n) ? n : 10) - minutes)).FirstOrDefault() ?? SmartIntervalCombo.Items[1];
    }

    private void SelectSmartMinimumScore(int score)
    {
        SmartMinimumScoreCombo.SelectedItem = SmartMinimumScoreCombo.Items.OfType<ComboBoxItem>()
            .OrderBy(x => Math.Abs((int.TryParse(x.Tag?.ToString(), out var n) ? n : 65) - score)).FirstOrDefault() ?? SmartMinimumScoreCombo.Items[1];
    }

    private void SelectSmartFailures(int failures)
    {
        SmartFailuresCombo.SelectedItem = SmartFailuresCombo.Items.OfType<ComboBoxItem>()
            .OrderBy(x => Math.Abs((int.TryParse(x.Tag?.ToString(), out var n) ? n : 2) - failures)).FirstOrDefault() ?? SmartFailuresCombo.Items[1];
    }

    private async void TrayEnabledToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        _settings.TrayEnabled = TrayEnabledToggle.IsOn;
        CloseToTrayToggle.IsEnabled = _settings.TrayEnabled;
        if (_settings.TrayEnabled)
        {
            _tray.Initialize(_settings.SmartAutoDnsEnabled);
            _tray.SetVisible(true);
            UpdateTrayState();
        }
        else
        {
            _settings.CloseToTray = false;
            CloseToTrayToggle.IsOn = false;
            _tray.SetVisible(false);
        }
        await _store.SaveSettingsAsync(_settings);
    }

    private async void CloseToTrayToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        _settings.CloseToTray = CloseToTrayToggle.IsOn && _settings.TrayEnabled;
        await _store.SaveSettingsAsync(_settings);
    }

    private async void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not string themeName) return;
        ApplyTheme(themeName);
        if (_initializing) return;
        _settings.ThemeName = themeName;
        await _store.SaveSettingsAsync(_settings);
    }

    private void ApplyTheme(string themeName)
    {
        var preset = themeName switch
        {
            "Aurora" => new ThemePreset("Aurora", true, "#0B0F1A", "#10141F", "#DC10141F"),
            "CyberNeon" => new ThemePreset("Neon", true, "#0A0A10", "#12111A", "#DC12111A"),
            "OceanDepth" => new ThemePreset("Ocean", true, "#071722", "#0B2130", "#DC0B2130"),
            "Graphite3D" => new ThemePreset("Graphite", true, "#111318", "#191C22", "#DC191C22"),
            _ => new ThemePreset("روشن", false, "#F7F8FA", "#EEF1F5", "#F2FFFFFF")
        };

        RootGrid.RequestedTheme = preset.Dark ? ElementTheme.Dark : ElementTheme.Light;
        ThemeBackdrop.Background = Gradient(preset.Background1, preset.Background2);
        NavView.Background = new SolidColorBrush(ParseColor(preset.Navigation));
        ThemeNameText.Text = preset.DisplayName;
    }

    private static LinearGradientBrush Gradient(params string[] colors)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        if (colors.Length == 0) return brush;
        for (var i = 0; i < colors.Length; i++)
        {
            brush.GradientStops.Add(new GradientStop
            {
                Color = ParseColor(colors[i]),
                Offset = colors.Length == 1 ? 0 : (double)i / (colors.Length - 1)
            });
        }
        return brush;
    }

    private static Color ParseColor(string hex)
    {
        var raw = hex.Trim().TrimStart('#');
        if (raw.Length == 6) raw = "FF" + raw;
        if (raw.Length != 8) return ColorHelper.FromArgb(255, 0, 0, 0);
        return ColorHelper.FromArgb(
            Convert.ToByte(raw[..2], 16),
            Convert.ToByte(raw.Substring(2, 2), 16),
            Convert.ToByte(raw.Substring(4, 2), 16),
            Convert.ToByte(raw.Substring(6, 2), 16));
    }

    private sealed record ThemePreset(
        string DisplayName,
        bool Dark,
        string Background1,
        string Background2,
        string Navigation);

    private async void FontCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FontCombo.SelectedItem is not ComboBoxItem item || item.Content is not string family) return;
        RootGrid.FontFamily = new FontFamily(family);
        if (_initializing) return;
        _settings.FontFamily = family;
        await _store.SaveSettingsAsync(_settings);
    }

    private void SelectFontInCombo(string family)
    {
        foreach (var item in FontCombo.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Content?.ToString(), family, StringComparison.OrdinalIgnoreCase))
            {
                FontCombo.SelectedItem = item;
                return;
            }
        }
        FontCombo.SelectedIndex = 0;
    }
}
