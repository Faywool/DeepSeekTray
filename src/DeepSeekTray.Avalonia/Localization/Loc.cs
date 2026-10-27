using System;
using System.Collections.Generic;
using System.Globalization;
using DeepSeekTray.Models;

namespace DeepSeekTray.Localization;

/// <summary>
/// 极简本地化：所有界面文案集中在一张表里，默认跟随系统语言（非中文一律英文）。
/// XAML 用 {loc:Tr Key} 取值，代码用 Loc.T / Loc.F。
/// </summary>
public static class Loc
{
    public static AppLanguage Current { get; private set; } = AppLanguage.System;

    public static bool IsChinese { get; private set; } = true;

    public static event Action? Changed;

    public static void Apply(AppLanguage language)
    {
        Current = language;

        IsChinese = language switch
        {
            AppLanguage.Chinese => true,
            AppLanguage.English => false,
            _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
                .Equals("zh", StringComparison.OrdinalIgnoreCase),
        };

        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(IsChinese ? "zh-CN" : "en-US");
        }
        catch
        {
            // 忽略
        }

        Changed?.Invoke();
    }

    public static string T(string key)
        => Map.TryGetValue(key, out var pair) ? (IsChinese ? pair.Zh : pair.En) : key;

    public static string F(string key, params object?[] args)
    {
        try
        {
            return string.Format(CultureInfo.InvariantCulture, T(key), args);
        }
        catch (FormatException)
        {
            return T(key);
        }
    }

    private static readonly Dictionary<string, (string Zh, string En)> Map = new(StringComparer.Ordinal)
    {
        // ── 通用 ──
        ["Common.Cancel"] = ("取消", "Cancel"),
        ["Common.Save"] = ("保存", "Save"),
        ["Common.Close"] = ("关闭", "Close"),
        ["Common.Delete"] = ("删除", "Delete"),
        ["Common.Rename"] = ("重命名", "Rename"),
        ["Common.Test"] = ("测试", "Test"),
        ["Common.Notice"] = ("提示", "Notice"),
        ["Common.Ok"] = ("确定", "OK"),

        ["Settings.Title"] = ("设置 · DeepSeek 余额托盘", "Settings · DeepSeek Balance Tray"),

        // ── 左侧导航 ──
        ["Nav.Accounts"] = ("账号与 Key", "Accounts"),
        ["Nav.Refresh"] = ("刷新与预警", "Refresh"),
        ["Nav.Appearance"] = ("外观", "Appearance"),
        ["Nav.About"] = ("关于", "About"),

        // ── 账号与 Key ──
        ["Accounts.Title"] = ("账号与 API Key", "Accounts & API Keys"),
        ["Accounts.Hint"] = (
            "余额是账号级数据：同一账号下多个 Key 共享同一余额；当前 Key 失效（401/403）时会自动切换到下一个。",
            "Balance is per account: every key under one account reads the same number. When the active key fails (401/403) the next one is tried automatically."),
        ["Accounts.Accounts"] = ("账号", "Accounts"),
        ["Accounts.Keys"] = ("该账号下的 Key", "Keys in this account"),
        ["Accounts.Add"] = ("添加账号", "Add account"),
        ["Accounts.SetDefault"] = ("设为默认", "Set default"),
        ["Accounts.AddKey"] = ("添加 Key", "Add key"),
        ["Accounts.SetActive"] = ("设为当前", "Use this key"),
        ["Accounts.Note"] = (
            "创建 / 删除 Key、查看官方账单都只能在 DeepSeek 控制台完成。这里的“今日消耗”是按本机余额采样推算的观测值，不是官方账单。",
            "Creating keys and official billing live in the DeepSeek console. The “today” figure here is estimated from local balance sampling, not an official bill."),

        // ── 刷新与预警 ──
        ["Refresh.Title"] = ("刷新与预警", "Refresh & alerts"),
        ["Refresh.Hint"] = (
            "余额接口不消耗 token，可以放心高频轮询；改完保存后新的间隔立即生效。",
            "The balance endpoint costs no tokens, so polling is cheap. A new interval takes effect as soon as you save."),
        ["Refresh.Interval"] = ("刷新间隔", "Interval"),
        ["Refresh.Minutes"] = ("分钟", "min"),
        ["Refresh.Range"] = ("（1 – 1440）", "(1–1440)"),
        ["Refresh.Alert"] = ("预警阈值", "Alert below"),
        ["Refresh.Below"] = ("低于", "below"),
        ["Refresh.WhenLow"] = ("时弹出系统通知", "→ notify me"),
        ["Refresh.Warn"] = ("余额低于阈值时弹出系统通知", "Show a system notification when balance drops below the threshold"),
        ["Refresh.Autostart"] = ("开机自动启动（托盘运行）", "Start with Windows (tray)"),
        ["Refresh.Popup"] = ("鼠标悬停在托盘图标上时显示余额浮层", "Show the balance popup when hovering the tray icon"),

        // ── 外观 ──
        ["Appearance.Title"] = ("外观", "Appearance"),
        ["Appearance.Hint"] = (
            "主题与语言会立即预览；点「保存」才会写入配置，点「取消」恢复原样。",
            "Theme and language preview immediately. Save writes them to disk, Cancel restores the previous values."),
        ["Appearance.Theme"] = ("主题", "Theme"),
        ["Appearance.System"] = ("跟随系统", "System"),
        ["Appearance.Light"] = ("浅色", "Light"),
        ["Appearance.Dark"] = ("深色", "Dark"),
        ["Appearance.Language"] = ("语言", "Language"),
        ["Appearance.LangZh"] = ("简体中文", "简体中文"),
        ["Appearance.LangEn"] = ("English", "English"),
        ["Appearance.Tiers"] = ("余额三段式颜色", "Balance tiers"),
        ["Appearance.TiersHint"] = (
            "阈值与颜色都可自定义；多个账号时，托盘图标取其中最“紧急”的一档。",
            "Thresholds and colours are configurable. With several accounts the tray icon shows the most urgent tier."),
        ["Appearance.Green"] = ("绿色", "Green"),
        ["Appearance.Yellow"] = ("黄色", "Yellow"),
        ["Appearance.Red"] = ("红色", "Red"),
        ["Appearance.AtLeast"] = ("余额 ≥ ¥", "≥ ¥"),
        ["Appearance.BelowYellow"] = ("低于黄色阈值", "below yellow"),
        ["Appearance.Good"] = ("余额充足", "healthy"),
        ["Appearance.Watch"] = ("需要留意", "watch"),
        ["Appearance.Low"] = ("即将耗尽", "critical"),

        // ── 设置里的关于页 ──
        ["AboutSection.Title"] = ("关于", "About"),
        ["AboutSection.Source"] = ("数据来源", "Data source"),
        ["AboutSection.SourceText"] = (
            "GET https://api.deepseek.com/user/balance —— 只返回 is_available 与 total / granted / topped_up 三个金额，没有累计消费，也没有 Key 列表。",
            "GET https://api.deepseek.com/user/balance — returns only is_available plus total / granted / topped_up. No lifetime spend, no key list."),
        ["AboutSection.Limits"] = ("能力边界", "Limits"),
        ["AboutSection.Limit1"] = ("余额查询、三段配色、定时刷新、余额预警", "Balance, tier colours, scheduling, alerts"),
        ["AboutSection.Limit2"] = ("多账号 / 多 Key 自动切换、浅色深色主题", "Multi-account / multi-key failover, light & dark themes"),
        ["AboutSection.Limit3"] = ("账号密码登录、创建 Key —— 官方无公开接口", "Password login, key creation — no public API"),
        ["AboutSection.Limit4"] = ("今日消耗 —— 本机采样估算，非官方账单", "Today's spend — estimated locally, not a bill"),
        ["AboutSection.OpenConsole"] = ("打开控制台", "Open console"),
        ["AboutSection.OpenAbout"] = ("打开关于窗口", "Open About window"),

        // ── 关于窗口 ──
        ["About.WindowTitle"] = ("关于 · DeepSeek 余额托盘", "About · DeepSeek Balance Tray"),
        ["About.Product"] = ("DeepSeek 余额托盘", "DeepSeek Balance Tray"),
        ["About.VersionFmt"] = ("版本 {0}", "Version {0}"),
        ["About.Done"] = ("已经做到", "What it does"),
        ["About.Done1"] = ("托盘常驻、悬停浮层、右键菜单、单实例", "Tray icon, hover popup, context menu, single instance"),
        ["About.Done2"] = ("多账号 / 多 Key，Key 失效自动切换", "Multiple accounts and keys, automatic failover"),
        ["About.Done3"] = ("三段式阈值与配色可改，图标按 DPI 渲染", "Configurable tiers and colours, DPI-aware icon rendering"),
        ["About.Done4"] = ("刷新间隔、余额预警、开机自启", "Refresh interval, low-balance alerts, autostart"),
        ["About.Done5"] = ("浅色 / 深色主题，跟随系统", "Light and dark themes that follow Windows"),
        ["About.NotDone"] = ("官方没给的，就不硬做", "What the API can't do"),
        ["About.Not1"] = ("账号密码登录：无公开登录 / OAuth 接口", "Password login — no public login or OAuth endpoint"),
        ["About.Not2"] = ("创建 / 列出 API Key：只能去控制台操作", "Creating or listing keys — console only"),
        ["About.Not3"] = ("官方累计消费 / 账单：接口不返回", "Lifetime spend or billing — not exposed by the API"),
        ["About.Not4"] = ("今日消耗：按本机采样差值估算，不代表官方账单", "Today's spend — estimated from local sampling, not a bill"),
        ["About.Disclaimer"] = ("免责声明", "Disclaimer"),
        ["About.D1"] = ("本项目是非官方工具，与 DeepSeek 官方无任何关联，也未获其授权或背书。",
            "This is an unofficial tool. It is not affiliated with, authorised by, or endorsed by DeepSeek."),
        ["About.D2"] = ("余额、今日消耗等数据来自官方公开接口与本机采样，仅供参考；实际金额以官方控制台为准。",
            "Balances and the daily figure come from public endpoints and local sampling, for reference only. The official console is authoritative."),
        ["About.D3"] = ("软件按“现状”提供，不附带任何明示或暗示的担保；因使用本软件产生的 API 费用、数据或其它损失由使用者自行承担。",
            "Provided “as is”, without warranty of any kind. API charges and any other loss from using this tool are your own responsibility."),
        ["About.D4"] = ("API Key 仅以 Windows DPAPI 加密保存在 %APPDATA%\\DeepSeekTray\\settings.json，不上传任何服务器，也不写入日志。",
            "API keys are stored only locally, encrypted with Windows DPAPI in %APPDATA%\\DeepSeekTray\\settings.json. Nothing is uploaded and keys never reach the logs."),
        ["About.D5"] = ("“DeepSeek”名称及相关标识归其权利人所有；本项目仅在说明用途时提及。",
            "“DeepSeek” and its marks belong to their owner. This project mentions the name only to describe what it connects to."),
        ["About.OpenFolder"] = ("打开配置目录", "Open data folder"),

        // ── 托盘菜单 ──
        ["Menu.Title"] = ("DeepSeek 余额托盘", "DeepSeek Balance Tray"),
        ["Menu.NoAccount"] = ("尚未添加账号", "No account yet"),
        ["Menu.SummaryFmt"] = ("今日消耗 {0} · {1} 分钟刷新", "Spent today {0} · refresh every {1} min"),
        ["Menu.Refresh"] = ("立即刷新", "Refresh now"),
        ["Menu.Settings"] = ("设置…", "Settings…"),
        ["Menu.Console"] = ("打开控制台", "Open console"),
        ["Menu.About"] = ("关于", "About"),
        ["Menu.Exit"] = ("退出", "Exit"),

        // ── 浮层 ──
        ["Popup.Header"] = ("DeepSeek 余额", "DeepSeek balance"),
        ["Popup.Today"] = ("今日消耗", "Today"),
        ["Popup.TodayShort"] = ("今日", "Today"),
        ["Popup.Default"] = ("默认", "default"),
        ["Popup.Total"] = ("今日合计", "Total today"),
        ["Popup.UpdatedFmt"] = ("{0} 更新", "updated {0}"),
        ["Popup.NoAccounts"] = ("还没有添加账号", "No accounts yet"),
        ["Popup.NotFetched"] = ("尚未获取余额", "not fetched"),
        ["Popup.NoKey"] = ("没有可用的 API Key", "No usable API key"),
        ["Popup.GrantedFmt"] = ("赠送 {0}", "Granted {0}"),
        ["Popup.ToppedFmt"] = ("充值 {0}", "Topped up {0}"),
        ["Popup.FetchFailed"] = ("获取余额失败", "Failed to fetch balance"),

        // ── 状态 ──
        ["Status.NotFetched"] = ("尚未获取余额", "not fetched"),
        ["Status.Failed"] = ("获取失败", "failed"),
        ["Status.Revoked"] = ("已失效", "revoked"),
        ["Status.Active"] = ("当前使用", "in use"),
        ["Status.Backup"] = ("备用", "backup"),
        ["Status.Default"] = ("默认", "default"),
        ["Status.Unavailable"] = ("余额不足", "insufficient"),
        ["Status.TodayFmt"] = ("{0} · 今日 -{1}", "{0} · today -{1}"),

        // ── 对话框 ──
        ["Dlg.AddAccount"] = ("添加账号", "Add account"),
        ["Dlg.NameLabel"] = ("账号名称（只在本机显示，可以写任意备注）", "Account name (local only, free-form)"),
        ["Dlg.AccountDefaultFmt"] = ("账号 {0}", "Account {0}"),
        ["Dlg.KeyLabel"] = ("粘贴 DeepSeek API Key（sk- 开头）", "Paste your DeepSeek API key (starts with sk-)"),
        ["Dlg.KeyHint"] = ("Key 会用 Windows DPAPI 加密后保存在本机；余额接口本身不消耗 token。",
            "The key is encrypted with Windows DPAPI and stored on this machine. The balance endpoint costs no tokens."),
        ["Dlg.AddKey"] = ("添加 Key", "Add key"),
        ["Dlg.AddKeyLabelFmt"] = ("为账号「{0}」添加一个 API Key", "Add an API key to “{0}”"),
        ["Dlg.AddKeyHint"] = ("同一账号下所有 Key 共享同一个余额；当前 Key 失效时会自动切换到其他 Key。",
            "All keys in one account share the same balance; if the active one fails, another is used."),
        ["Dlg.Rename"] = ("重命名账号", "Rename account"),
        ["Dlg.RenameLabel"] = ("新的账号名称", "New account name"),
        ["Dlg.SameAccountTitle"] = ("疑似同一账号", "Same account?"),
        ["Dlg.SameAccountTextFmt"] = (
            "这个 Key 与账号「{0}」的余额完全一致（{1}），很可能属于同一个 DeepSeek 账号。\n\n确定＝加到该账号下；取消＝仍作为独立账号保存。",
            "This key shows exactly the same balance as “{0}” ({1}), so it probably belongs to the same DeepSeek account.\n\nOK = add it to that account; Cancel = keep it as a separate account."),
        ["Dlg.AddToAccount"] = ("加到该账号", "Merge"),
        ["Dlg.KeepSeparate"] = ("独立账号", "Keep separate"),
        ["Dlg.MismatchTitle"] = ("余额不一致", "Balance mismatch"),
        ["Dlg.MismatchTextFmt"] = ("这个 Key 的余额（{0}）与当前账号记录不一致，可能属于另一个账号。仍要加到这个账号下吗？",
            "This key reports {0}, which differs from the account's current balance — it may belong to another account. Add it anyway?"),
        ["Dlg.DeleteAccountTitle"] = ("确认删除", "Confirm delete"),
        ["Dlg.DeleteAccountTextFmt"] = ("删除账号「{0}」及其 {1} 个 Key？", "Delete account “{0}” and its {1} key(s)?"),
        ["Dlg.DeleteKeyTextFmt"] = ("删除 Key「{0}」？", "Delete key “{0}”?"),
        ["Dlg.TestTitle"] = ("测试结果", "Test result"),
        ["Dlg.TestOkFmt"] = ("Key 有效。\n当前余额：{0}\n可用状态：{1}", "Key is valid.\nBalance: {0}\nStatus: {1}"),
        ["Dlg.Available"] = ("可用", "available"),
        ["Dlg.Insufficient"] = ("余额不足", "insufficient"),
        ["Dlg.SelectKeyFirst"] = ("请先选择一个 Key。", "Select a key first."),
        ["Dlg.SelectAccountFirst"] = ("请先选择或添加一个账号。", "Select or add an account first."),
        ["Dlg.KeyUndecryptable"] = ("这个 Key 无法解密（可能是在其他 Windows 用户下保存的）。",
            "This key can't be decrypted — it was probably saved by another Windows user."),
        ["Dlg.KeepOneKey"] = ("账号至少需要保留一个 Key；如果不想再用这个账号，请直接删除账号。",
            "An account needs at least one key. To stop using it, delete the whole account instead."),
        ["Dlg.ValidationFailed"] = ("校验失败", "Validation failed"),
        ["Dlg.ColorTitle"] = ("颜色", "Colour"),
        ["Dlg.ColorLabel"] = ("输入十六进制颜色（例如 #1DB954）", "Hex colour (e.g. #1DB954)"),
        ["Dlg.ColorInvalid"] = ("颜色格式不对，请用 #RRGGBB。", "Invalid colour — use #RRGGBB."),

        // ── 校验 ──
        ["Validation.Interval"] = ("刷新间隔需要是 1–1440 之间的整数（分钟）。", "Interval must be a whole number between 1 and 1440 minutes."),
        ["Validation.Numbers"] = ("绿色 / 黄色阈值必须是数字。", "Green and yellow thresholds must be numbers."),
        ["Validation.Order"] = ("绿色阈值必须大于黄色阈值，且都不能为负数。", "Green must be greater than yellow, and neither can be negative."),
        ["Validation.Alert"] = ("预警阈值必须是不小于 0 的数字。", "The alert threshold must be zero or greater."),

        // ── 运行时提示 ──
        ["Alert.Title"] = ("余额预警", "Low balance"),
        ["Alert.TextFmt"] = ("{0} 当前余额 {1}，低于预警阈值 {2}。", "{0} balance is {1}, below the alert threshold {2}."),
        ["Err.NoKey"] = ("没有可用的 API Key", "No usable API key"),
        ["Err.FetchFailed"] = ("获取余额失败", "Failed to fetch balance"),
        ["Err.KeyUndecryptable"] = ("密钥无法解密（可能来自其他 Windows 用户）", "Key can't be decrypted (saved by another Windows user?)"),
        ["Err.OpenFailed"] = ("打开失败", "Failed to open"),

        // ── API ──
        ["Api.EmptyKey"] = ("API Key 为空", "API key is empty"),
        ["Api.Timeout"] = ("请求超时（15 秒），请检查网络或代理", "Request timed out after 15s — check your network or proxy"),
        ["Api.Network"] = ("网络请求失败：{0}", "Network error: {0}"),
        ["Api.InvalidKey"] = ("API Key 无效、已删除或没有权限", "API key is invalid, deleted, or not permitted"),
        ["Api.Status"] = ("接口返回 {0}：{1}", "API returned {0}: {1}"),
        ["Api.BadJson"] = ("接口返回的不是预期 JSON：{0}", "Unexpected JSON from the API: {0}"),
        ["Api.NoInfos"] = ("接口响应缺少 balance_infos 数据", "The response has no balance_infos"),
    };
}
