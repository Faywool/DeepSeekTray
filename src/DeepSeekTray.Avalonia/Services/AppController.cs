using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;
using DeepSeekTray.Localization;
using DeepSeekTray.Models;
using DeepSeekTray.Views;

namespace DeepSeekTray.Services;

/// <summary>托盘、轮询、预警、今日消耗、设置应用。</summary>
public sealed class AppController : IDisposable
{
    private const double HoverRadius = 48;

    private readonly SettingsStore _store = new();
    private readonly TrayIconHost _tray = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly DispatcherTimer _hoverDelay;
    private readonly DispatcherTimer _hoverWatch;

    private AppSettings _settings;
    private InfoPopupWindow? _popup;
    private ThemeVariant? _popupVariant;
    private TrayMenuWindow? _menuWindow;
    private readonly MemoryJanitor _memoryJanitor;
    private DateTime _lastActivityUtc = DateTime.UtcNow;
    private SettingsWindow? _settingsWindow;
    private CancellationTokenSource? _pollCts;
    private DateTimeOffset? _lastRefreshAt;
    private DateTime _suppressHoverUntil = DateTime.MinValue;
    private bool _disposed;

    public DeepSeekApiClient Api { get; } = new();

    public AppSettings Settings => _settings;

    public AppController()
    {
        _settings = _store.Load();
        EnsureIntegrity(_settings);

        _tray.Hovered += OnTrayHovered;
        _tray.LeftClicked += ShowSettings;
        _tray.RightClicked += ShowTrayMenu;

        _hoverDelay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _hoverDelay.Tick += (_, _) =>
        {
            _hoverDelay.Stop();
            ShowPopup();
        };

        _hoverWatch = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(160) };
        _hoverWatch.Tick += (_, _) => HidePopupIfLeft();

        _memoryJanitor = new MemoryJanitor(IsIdleForTrim, ReleaseIdleCaches);
    }

    public void Start()
    {
        Loc.Apply(_settings.Language);
        ApplyTheme(_settings.Theme);
        UpdateTrayVisuals();
        StartPolling();
        _ = RefreshAllSafeAsync();
        Log.Info("DeepSeek 余额托盘（Avalonia）启动");
    }

    // ── 主题 ─────────────────────────────────────────────────────────────

    public void ApplyTheme(AppTheme theme)
    {
        if (Application.Current is null)
        {
            return;
        }

        Application.Current.RequestedThemeVariant = theme switch
        {
            AppTheme.Light => ThemeVariant.Light,
            AppTheme.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };

        // 浮层里的颜色是代码里取的资源快照，换主题后重建一次
        if (_popup is { IsVisible: true })
        {
            _popup.Hide();
        }
    }

    // ── 刷新 ─────────────────────────────────────────────────────────────

    public async Task RefreshAllSafeAsync()
    {
        try
        {
            await RefreshAllAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // 正常
        }
        catch (Exception ex)
        {
            Log.Error("刷新失败", ex);
        }
    }

    public async Task RefreshAllAsync(CancellationToken cancellationToken)
    {
        if (!await _refreshGate.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            var accounts = _settings.Accounts.ToList();
            var results = new List<(AccountProfile Account, BalanceSnapshot Snapshot)>();

            foreach (var account in accounts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                results.Add((account, await FetchForAccountAsync(account, cancellationToken)));
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var (account, snapshot) in results)
                {
                    account.LastBalance = snapshot;

                    if (snapshot.HasData)
                    {
                        UpdateDailyUsage(account, snapshot);
                    }
                }

                _lastRefreshAt = DateTimeOffset.Now;
                UpdateTrayVisuals();
                RefreshPopupIfVisible();
                CheckAlerts();
                _store.Save(_settings);
            });
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task<BalanceSnapshot> FetchForAccountAsync(AccountProfile account, CancellationToken cancellationToken)
    {
        var candidates = OrderedKeys(account).ToList();
        var usable = candidates.Where(k => !k.IsRevoked).ToList();
        var keys = usable.Count > 0 ? usable : candidates;

        if (keys.Count == 0)
        {
            return BalanceSnapshot.Failed(Loc.T("Err.NoKey"));
        }

        string? lastError = null;

        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var plain = Dpapi.Unprotect(key.ProtectedKey);
            if (string.IsNullOrWhiteSpace(plain))
            {
                key.LastError = Loc.T("Err.KeyUndecryptable");
                lastError = key.LastError;
                continue;
            }

            try
            {
                var snapshot = await Api.GetBalanceAsync(plain, cancellationToken);

                key.LastError = null;
                key.LastCheckedAt = DateTimeOffset.Now;
                key.IsRevoked = false;
                account.ActiveKeyId = key.Id;
                account.Currency = snapshot.Currency;
                return snapshot;
            }
            catch (ApiKeyRejectedException ex)
            {
                key.IsRevoked = true;
                key.LastError = ex.Message;
                key.LastCheckedAt = DateTimeOffset.Now;
                lastError = $"{key.Name}：{ex.Message}";
                Log.Warn($"账号「{account.Name}」的 Key「{key.Name}」被拒绝：{ex.Message}");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (BalanceRequestException ex)
            {
                return BalanceSnapshot.Failed(ex.Message);
            }
            catch (Exception ex)
            {
                Log.Error($"账号「{account.Name}」查询异常", ex);
                return BalanceSnapshot.Failed(ex.Message);
            }
        }

        return BalanceSnapshot.Failed(lastError ?? Loc.T("Err.FetchFailed"));
    }

    private static IEnumerable<ApiKeyEntry> OrderedKeys(AccountProfile account)
    {
        if (account.ActiveKeyId is Guid activeId)
        {
            foreach (var key in account.Keys.Where(k => k.Id == activeId))
            {
                yield return key;
            }
        }

        foreach (var key in account.Keys.Where(k => k.Id != account.ActiveKeyId))
        {
            yield return key;
        }
    }

    private void StartPolling()
    {
        try
        {
            _pollCts?.Cancel();
            _pollCts?.Dispose();
        }
        catch
        {
            // 忽略
        }

        _pollCts = new CancellationTokenSource();
        var token = _pollCts.Token;
        var interval = TimeSpan.FromMinutes(Math.Clamp(_settings.RefreshIntervalMinutes, 1, 1440));

        _ = Task.Run(async () =>
        {
            try
            {
                using var timer = new PeriodicTimer(interval);
                while (await timer.WaitForNextTickAsync(token))
                {
                    await RefreshAllAsync(token);
                }
            }
            catch (OperationCanceledException)
            {
                // 正常退出
            }
            catch (Exception ex)
            {
                Log.Error("轮询循环异常", ex);
            }
        }, token);
    }

    // ── 托盘与浮层 ───────────────────────────────────────────────────────

    private void UpdateTrayVisuals()
    {
        var tier = BalanceTier.Worst(_settings.Accounts
            .Select(a => a.LastBalance is { HasData: true } data
                ? BalanceTier.Classify(data.Total, _settings.Tiers)
                : TierLevel.Unknown));

        var color = tier == TierLevel.Unknown
            ? Avalonia.Media.Color.FromRgb(0x9C, 0xA3, 0xAF)
            : ColorHelper.Parse(BalanceTier.HexOf(tier, _settings.Tiers));

        _tray.SetIcon(color);
    }

    private void Touch() => _lastActivityUtc = DateTime.UtcNow;

    private bool IsIdleForTrim()
    {
        if (_popup is { IsVisible: true }
            || _settingsWindow is { IsVisible: true }
            || _menuWindow is { IsVisible: true })
        {
            return false;
        }

        // 真实交互后至少静置一分钟才回收，避免刚移开鼠标就被裁工作集
        var idleThreshold = MemoryJanitor.FastMode ? TimeSpan.FromSeconds(5) : TimeSpan.FromSeconds(60);
        return DateTime.UtcNow - _lastActivityUtc >= idleThreshold;
    }

    private void ReleaseIdleCaches()
    {
        // 浮层长期不用就销毁，下一次悬停再重建（约 30ms），换取常驻内存下降
        if (_popup is { IsVisible: false })
        {
            try
            {
                _popup.Close();
            }
            catch (InvalidOperationException)
            {
                // 已经在关闭
            }

            _popup = null;
            _popupVariant = null;
        }
    }

    private void OnTrayHovered()
    {
        Touch();
        if (DateTime.UtcNow < _suppressHoverUntil || _disposed || !_settings.ShowPopupOnHover)
        {
            return;
        }

        if (_popup is { IsVisible: true })
        {
            return;
        }

        _hoverDelay.Stop();
        _hoverDelay.Start();
    }

    private void ShowPopup()
    {
        Touch();

        if (_disposed || !_settings.ShowPopupOnHover)
        {
            return;
        }

        try
        {
            var variant = Application.Current?.ActualThemeVariant ?? ThemeVariant.Default;

            if (_popup is null || _popupVariant != variant)
            {
                _popup = new InfoPopupWindow(variant);
                _popupVariant = variant;
                _popup.Closed += (_, _) => _popup = null;
            }

            RenderPopup();
            _popup.ShowNearIcon(GetIconRect());
            _hoverWatch.Start();
        }
        catch (Exception ex)
        {
            Log.Error("显示悬停浮层失败", ex);
        }
    }

    private Native.Rect GetIconRect()
    {
        if (_tray.TryGetIconRect(out var rect))
        {
            return rect;
        }

        Native.GetCursorPos(out var cursor);
        return new Native.Rect
        {
            Left = cursor.X - 12,
            Top = cursor.Y - 12,
            Right = cursor.X + 12,
            Bottom = cursor.Y + 12,
        };
    }

    private void RefreshPopupIfVisible()
    {
        if (_popup is not { IsVisible: true })
        {
            return;
        }

        RenderPopup();
    }

    private void HidePopupIfLeft()
    {
        if (_disposed)
        {
            _hoverWatch.Stop();
            return;
        }

        try
        {
            if (_popup is null || !_popup.IsVisible)
            {
                _hoverWatch.Stop();
                return;
            }

            Native.GetCursorPos(out var cursor);

            if (_popup.ContainsScreenPoint(cursor.X, cursor.Y))
            {
                return;
            }

            if (_tray.TryGetIconRect(out var icon)
                && cursor.X >= icon.Left - 4 && cursor.X <= icon.Right + 4
                && cursor.Y >= icon.Top - 4 && cursor.Y <= icon.Bottom + 4)
            {
                return;
            }

            HidePopup();
        }
        catch (Exception ex)
        {
            Log.Warn("判断浮层收起时出错：" + ex.Message);
            _hoverWatch.Stop();
        }
    }

    internal void HidePopup()
    {
        _hoverDelay.Stop();
        _hoverWatch.Stop();
        _suppressHoverUntil = DateTime.UtcNow.AddMilliseconds(400);

        try
        {
            _popup?.Hide();
        }
        catch (InvalidOperationException)
        {
            // 正在关闭
        }
    }

    private List<PopupRow> BuildRows()
    {
        var rows = new List<PopupRow>();

        foreach (var account in _settings.Accounts)
        {
            var data = account.LastBalance;

            if (data is null || !data.HasData)
            {
                rows.Add(new PopupRow
                {
                    Name = account.Name,
                    Amount = "—",
                    Detail = data?.Error ?? Loc.T("Popup.NotFetched"),
                    Time = data is null ? string.Empty : data.FetchedAt.ToString("HH:mm:ss"),
                    AccentHex = "#B0B5BF",
                    IsError = true,
                });
                continue;
            }

            var level = BalanceTier.Classify(data.Total, _settings.Tiers);
            rows.Add(new PopupRow
            {
                Name = account.Name,
                Amount = Money(data.Total, data.Currency),
                Detail = data.Granted > 0m
                    ? data.ToppedUp > 0m
                        ? $"赠送 {Money(data.Granted, data.Currency)} · 充值 {Money(data.ToppedUp, data.Currency)}"
                        : $"赠送 {Money(data.Granted, data.Currency)}"
                    : string.Empty,
                Time = data.FetchedAt.ToString("HH:mm:ss"),
                Today = $"-{Money(account.Today?.Spent ?? 0m, data.Currency)}",
                AccentHex = BalanceTier.HexOf(level, _settings.Tiers),
                IsActive = _settings.Accounts.Count > 1 && account.Id == _settings.ActiveAccountId,
            });
        }

        return rows;
    }

    private void RenderPopup()
    {
        if (_popup is null)
        {
            return;
        }

        var meta = _lastRefreshAt is DateTimeOffset last ? Loc.F("Popup.UpdatedFmt", last.ToString("HH:mm")) : string.Empty;
        string? totalLabel = null;
        string? totalValue = null;

        // 只有一个账号时，行内已经有“今日”，再给合计就是重复
        if (_settings.Accounts.Count > 1)
        {
            var spent = _settings.Accounts.Sum(a => a.Today?.Spent ?? 0m);
            totalLabel = Loc.T("Popup.Total");
            totalValue = $"-{Money(spent, _settings.Accounts[0].Currency)}";
        }

        _popup.Render(BuildRows(), meta, totalLabel, totalValue);
    }

    private void CheckAlerts()
    {
        if (!_settings.WarnOnLowBalance)
        {
            foreach (var account in _settings.Accounts)
            {
                account.LowBalanceNotified = false;
            }

            return;
        }

        foreach (var account in _settings.Accounts)
        {
            var data = account.LastBalance;
            if (data is null || !data.HasData)
            {
                continue;
            }

            var low = data.Total < _settings.AlertBelow;
            if (low && !account.LowBalanceNotified)
            {
                _tray.Notify(
                    Loc.T("Alert.Title"),
                    Loc.F("Alert.TextFmt", account.Name, Money(data.Total, data.Currency), Money(_settings.AlertBelow, data.Currency)),
                    warning: true);
                account.LowBalanceNotified = true;
            }
            else if (!low)
            {
                account.LowBalanceNotified = false;
            }
        }
    }

    // ── 窗口 ─────────────────────────────────────────────────────────────

    public void ApplySettings(AppSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        EnsureIntegrity(_settings);

        Autostart.Set(_settings.StartWithWindows);
        Loc.Apply(_settings.Language);
        ApplyTheme(_settings.Theme);
        _store.Save(_settings);

        UpdateTrayVisuals();
        RefreshPopupIfVisible();
        StartPolling();
        _ = RefreshAllSafeAsync();

        Log.Info($"设置已更新：间隔 {_settings.RefreshIntervalMinutes} 分钟，账号 {_settings.Accounts.Count} 个");
    }

    public void RequestOpenSettings() => Dispatcher.UIThread.Post(ShowSettings);

    private void ShowSettings()
    {
        if (_disposed)
        {
            return;
        }

        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        try
        {
            Touch();
            HidePopup();
            _settingsWindow = new SettingsWindow(this, null);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
            _settingsWindow.Activate();
        }
        catch (Exception ex)
        {
            Log.Error("打开设置窗口失败", ex);
        }
    }

    /// <summary>换语言后窗口需要按新文案重建；保留用户当前编辑内容与所在分区。</summary>
    public void ReopenSettings(AppSettings draft, int section, SettingsWindow current)
    {
        current.SuppressRevert();
        current.Close();

        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _settingsWindow = new SettingsWindow(this, draft, section);
                _settingsWindow.Closed += (_, _) => _settingsWindow = null;
                _settingsWindow.Show();
                _settingsWindow.Activate();
            }
            catch (Exception ex)
            {
                Log.Error("重建设置窗口失败", ex);
            }
        });
    }

    internal void ShowTrayMenu()
    {
        try
        {
            HidePopup();

            var currency = _settings.Accounts.FirstOrDefault()?.Currency ?? "CNY";
            var spent = _settings.Accounts.Sum(a => a.Today?.Spent ?? 0m);

            var subtitle = _settings.Accounts.Count == 0
                ? Loc.T("Menu.NoAccount")
                : Loc.F("Menu.SummaryFmt", $"-{Money(spent, currency)}", _settings.RefreshIntervalMinutes);

            var variant = Application.Current?.ActualThemeVariant ?? ThemeVariant.Default;
            var menu = new TrayMenuWindow(variant, Loc.T("Menu.Title"), subtitle, new[]
            {
                new TrayMenuItem("\uE72C", Loc.T("Menu.Refresh"), () => _ = RefreshAllSafeAsync()),
                new TrayMenuItem("\uE713", Loc.T("Menu.Settings"), ShowSettings),
                new TrayMenuItem("\uE8A7", Loc.T("Menu.Console"), OpenConsole),
                new TrayMenuItem("\uE946", Loc.T("Menu.About"), ShowAbout),
                new TrayMenuItem("\uE7E8", Loc.T("Menu.Exit"), Exit, Danger: true),
            });

            _menuWindow = menu;
            menu.Closed += (_, _) => _menuWindow = null;
            Touch();
            menu.ShowAtCursor();
        }
        catch (Exception ex)
        {
            Log.Error("显示托盘菜单失败", ex);
        }
    }

    private void ShowAbout()
    {
        try
        {
            HidePopup();

            var about = new AboutWindow();
            if (_settingsWindow is { IsVisible: true } owner)
            {
                _ = about.ShowDialog(owner);
            }
            else
            {
                about.Show();
            }
        }
        catch (InvalidOperationException)
        {
            new AboutWindow().Show();
        }
        catch (Exception ex)
        {
            Log.Error("打开关于窗口失败", ex);
        }
    }

    private void OpenConsole()
    {
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo("https://platform.deepseek.com/api_keys")
                {
                    UseShellExecute = true,
                });
        }
        catch (Exception ex)
        {
            Log.Error("打开控制台失败", ex);
        }
    }

    private void Exit()
    {
        Log.Info("用户从托盘退出");
        (Application.Current?.ApplicationLifetime as
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }

    // ── 工具 ─────────────────────────────────────────────────────────────

    public static string CurrencySymbol(string? currency)
        => string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase) ? "$" : "¥";

    public static string Money(decimal value, string? currency)
        => CurrencySymbol(currency) + value.ToString("#,##0.00", CultureInfo.InvariantCulture);

    public static string MaskKey(string key)
    {
        var trimmed = (key ?? string.Empty).Trim();
        if (trimmed.Length <= 8)
        {
            return new string('*', Math.Max(trimmed.Length, 1));
        }

        return trimmed.Substring(0, 4) + "…" + trimmed.Substring(trimmed.Length - 4);
    }

    private static void UpdateDailyUsage(AccountProfile account, BalanceSnapshot snapshot)
    {
        var usage = account.Today ??= new DailyUsage();
        var today = DateTimeOffset.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (!string.Equals(usage.Date, today, StringComparison.Ordinal))
        {
            usage.Date = today;
            usage.Spent = 0m;
            usage.ToppedUp = 0m;
            usage.LastTotal = snapshot.Total;
            return;
        }

        var delta = usage.LastTotal - snapshot.Total;

        if (delta > 0m)
        {
            usage.Spent += delta;
        }
        else if (delta < 0m)
        {
            usage.ToppedUp += -delta;
        }

        usage.LastTotal = snapshot.Total;
    }

    private static void EnsureIntegrity(AppSettings settings)
    {
        settings.Accounts ??= new List<AccountProfile>();
        settings.Tiers ??= new TierSettings();

        if (settings.RefreshIntervalMinutes < 1)
        {
            settings.RefreshIntervalMinutes = 5;
        }

        if (settings.RefreshIntervalMinutes > 1440)
        {
            settings.RefreshIntervalMinutes = 1440;
        }

        if (settings.Tiers.GreenFrom <= settings.Tiers.YellowFrom)
        {
            settings.Tiers.GreenFrom = 100m;
            settings.Tiers.YellowFrom = 20m;
        }

        foreach (var account in settings.Accounts)
        {
            account.Keys ??= new List<ApiKeyEntry>();
            account.Today ??= new DailyUsage();

            if (account.Keys.Count > 0
                && (account.ActiveKeyId is null || account.Keys.All(k => k.Id != account.ActiveKeyId)))
            {
                account.ActiveKeyId = account.Keys[0].Id;
            }
        }

        if (settings.Accounts.Count > 0
            && (settings.ActiveAccountId is null || settings.Accounts.All(a => a.Id != settings.ActiveAccountId)))
        {
            settings.ActiveAccountId = settings.Accounts[0].Id;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _hoverDelay.Stop();
        _hoverWatch.Stop();

        try
        {
            _pollCts?.Cancel();
            _pollCts?.Dispose();
        }
        catch
        {
            // 忽略
        }

        _store.Save(_settings);
        _memoryJanitor.Dispose();
        _tray.Dispose();
    }
}
