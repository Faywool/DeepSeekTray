using System;
using System.Collections.Generic;
namespace DeepSeekTray.Models;

public enum AppLanguage
{
    System = 0,
    Chinese = 1,
    English = 2,
}

public enum AppTheme
{
    System = 0,
    Light = 1,
    Dark = 2,
}

/// <summary>持久化到 %APPDATA%\DeepSeekTray\settings.json 的根对象。</summary>
public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;

    public List<AccountProfile> Accounts { get; set; } = new();

    /// <summary>托盘图标的默认账号（tooltip 中会加星标）。</summary>
    public Guid? ActiveAccountId { get; set; }

    /// <summary>刷新间隔（分钟），默认 5。</summary>
    public int RefreshIntervalMinutes { get; set; } = 5;

    public bool WarnOnLowBalance { get; set; } = true;

    public decimal AlertBelow { get; set; } = 20m;

    public bool StartWithWindows { get; set; }

    public bool ShowPopupOnHover { get; set; } = true;

    public AppTheme Theme { get; set; } = AppTheme.System;

    public AppLanguage Language { get; set; } = AppLanguage.System;

    public TierSettings Tiers { get; set; } = new();

    public static AppSettings Clone(AppSettings source)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(source);
        return System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }
}

/// <summary>
/// 一个 DeepSeek 账号。余额是账号级的：同账号下所有 Key 查询结果相同，
/// 因此这里用「账号 -> 多个 Key」建模，主 Key 失效时自动切换。
/// </summary>
public sealed class AccountProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "DeepSeek 账号";

    /// <summary>当前用于查询余额的 Key。</summary>
    public Guid? ActiveKeyId { get; set; }

    public List<ApiKeyEntry> Keys { get; set; } = new();

    public string Currency { get; set; } = "CNY";

    public BalanceSnapshot? LastBalance { get; set; }

    /// <summary>今日消耗观测值（按本机采样累计）。</summary>
    public DailyUsage Today { get; set; } = new();

    /// <summary>是否已经就“余额过低”提醒过，避免每轮重复弹通知。</summary>
    public bool LowBalanceNotified { get; set; }
}

/// <summary>
/// “今日消耗”只能靠本机采样推算：官方接口不提供账单。
/// 余额下降计入消耗，余额上升视为充值不计入，跨天自动清零。
/// </summary>
public sealed class DailyUsage
{
    /// <summary>本地日期 yyyy-MM-dd。</summary>
    public string Date { get; set; } = string.Empty;

    public decimal Spent { get; set; }

    public decimal ToppedUp { get; set; }

    /// <summary>上一次观测到的余额，用于算差值。</summary>
    public decimal LastTotal { get; set; }
}

public sealed class ApiKeyEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "Key";

    /// <summary>DPAPI（当前用户）加密后的 Base64。</summary>
    public string ProtectedKey { get; set; } = "";

    /// <summary>脱敏展示，例如 sk-…a1b2。</summary>
    public string Hint { get; set; } = "";

    public DateTimeOffset? LastCheckedAt { get; set; }

    public string? LastError { get; set; }

    /// <summary>接口返回 401/403 时置为 true，之后不再优先使用。</summary>
    public bool IsRevoked { get; set; }
}

public sealed class BalanceSnapshot
{
    public bool IsAvailable { get; set; }

    public string Currency { get; set; } = "CNY";

    public decimal Total { get; set; }

    public decimal Granted { get; set; }

    public decimal ToppedUp { get; set; }

    public DateTimeOffset FetchedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>非空表示这次没有取到有效数据。</summary>
    public string? Error { get; set; }

    public bool HasData => Error is null;

    public static BalanceSnapshot Failed(string error) => new()
    {
        Error = error,
        FetchedAt = DateTimeOffset.Now,
    };
}

/// <summary>三段式余额分级，阈值与颜色均可配置。</summary>
public sealed class TierSettings
{
    /// <summary>余额 &gt;= 该值显示绿色，默认 100。</summary>
    public decimal GreenFrom { get; set; } = 100m;

    /// <summary>余额 &gt;= 该值（且小于绿色阈值）显示黄色，默认 20。</summary>
    public decimal YellowFrom { get; set; } = 20m;

    public string GreenColor { get; set; } = "#1DB954";

    public string YellowColor { get; set; } = "#F5A524";

    public string RedColor { get; set; } = "#E5484D";
}

/// <summary>悬停浮层里的单行数据。</summary>
public sealed class PopupRow
{
    public string Name { get; init; } = "";

    public string Amount { get; init; } = "";

    public string Detail { get; init; } = "";

    public string Time { get; init; } = "";

    /// <summary>“今日 -¥1.23”这类文本，空表示不显示。</summary>
    public string Today { get; init; } = "";

    public string AccentHex { get; init; } = "#9CA3AF";

    public bool IsError { get; init; }

    public bool IsActive { get; init; }
}
