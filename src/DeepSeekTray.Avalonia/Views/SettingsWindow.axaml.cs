using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using DeepSeekTray.Localization;
using DeepSeekTray.Models;
using DeepSeekTray.Services;

namespace DeepSeekTray.Views;

public partial class SettingsWindow : Window
{
    private readonly AppController _controller = null!;
    private readonly AppSettings _draft = null!;
    private readonly AppTheme _originalTheme;
    private readonly AppLanguage _originalLanguage;

    private Guid? _selectedAccountId;
    private bool _loading;
    private bool _saved;

    public SettingsWindow()
    {
        InitializeComponent();
    }

    public SettingsWindow(AppController controller, AppSettings? initial, int section = 0) : this()
    {
        _loading = true;

        _controller = controller;
        _draft = AppSettings.Clone(initial ?? controller.Settings);
        _originalTheme = _draft.Theme;
        _originalLanguage = _draft.Language;
        _selectedAccountId = _draft.ActiveAccountId ?? _draft.Accounts.FirstOrDefault()?.Id;

        LoadAll(section);
        _loading = false;

        Closing += (_, _) =>
        {
            if (_saved)
            {
                return;
            }

            // 回滚到“已保存”的值，而不是本窗口构建时的草稿值
            var theme = _controller.Settings.Theme;
            var language = _controller.Settings.Language;

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                controller.ApplyTheme(theme);
                Loc.Apply(language);
            });
        };
    }

    /// <summary>语言切换要重建窗口，重建前先标记“不要回滚”，否则旧窗口关闭时会把手动切换撤销。</summary>
    internal void SuppressRevert() => _saved = true;

    private AccountProfile? SelectedAccount => (AccountList.SelectedItem as AccountRow)?.Account;

    private void LoadAll(int section)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2.0.0";
        VersionText.Text = $"v{version}";

        IntervalBox.Text = _draft.RefreshIntervalMinutes.ToString(CultureInfo.InvariantCulture);
        AlertBox.Text = _draft.AlertBelow.ToString("0.##", CultureInfo.InvariantCulture);
        GreenFromBox.Text = _draft.Tiers.GreenFrom.ToString("0.##", CultureInfo.InvariantCulture);
        YellowFromBox.Text = _draft.Tiers.YellowFrom.ToString("0.##", CultureInfo.InvariantCulture);

        WarnCheck.IsChecked = _draft.WarnOnLowBalance;
        AutostartCheck.IsChecked = Autostart.IsEnabled();
        PopupCheck.IsChecked = _draft.ShowPopupOnHover;

        UpdateSwatch(GreenSwatch, _draft.Tiers.GreenColor);
        UpdateSwatch(YellowSwatch, _draft.Tiers.YellowColor);
        UpdateSwatch(RedSwatch, _draft.Tiers.RedColor);

        SelectThemeButton(_draft.Theme);
        SelectLanguageButton(_draft.Language);

        ShowSection(section);
        Nav.SelectedIndex = section;
        ReloadAccounts(_selectedAccountId);
    }

    // ── 导航 ─────────────────────────────────────────────────────────────

    private void Nav_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Nav is null)
        {
            return;
        }

        ShowSection(Nav.SelectedIndex);
    }

    internal void SelectSection(int index) => Nav.SelectedIndex = index;

    private void ShowSection(int index)
    {
        if (SectionAccount is null || SectionRefresh is null || SectionAppearance is null || SectionAbout is null)
        {
            return;
        }

        SectionAccount.IsVisible = index == 0;
        SectionRefresh.IsVisible = index == 1;
        SectionAppearance.IsVisible = index == 2;
        SectionAbout.IsVisible = index == 3;
    }

    // ── 主题 / 语言 ──────────────────────────────────────────────────────

    private void SelectThemeButton(AppTheme theme)
    {
        var buttons = new[] { ThemeSystemButton, ThemeLightButton, ThemeDarkButton };
        var target = theme switch
        {
            AppTheme.Light => ThemeLightButton,
            AppTheme.Dark => ThemeDarkButton,
            _ => ThemeSystemButton,
        };

        foreach (var button in buttons)
        {
            button.Classes.Set("active", ReferenceEquals(button, target));
        }
    }

    private AppTheme SelectedTheme()
    {
        if (ThemeDarkButton.Classes.Contains("active"))
        {
            return AppTheme.Dark;
        }

        return ThemeLightButton.Classes.Contains("active") ? AppTheme.Light : AppTheme.System;
    }

    private void Theme_Click(object? sender, RoutedEventArgs e)
    {
        if (_loading || sender is not Button button)
        {
            return;
        }

        var theme = (button.Tag as string) switch
        {
            "light" => AppTheme.Light,
            "dark" => AppTheme.Dark,
            _ => AppTheme.System,
        };

        SelectThemeButton(theme);
        _controller.ApplyTheme(theme);
    }

    private void SelectLanguageButton(AppLanguage language)
    {
        var buttons = new[] { LangSystemButton, LangZhButton, LangEnButton };
        var target = language switch
        {
            AppLanguage.Chinese => LangZhButton,
            AppLanguage.English => LangEnButton,
            _ => LangSystemButton,
        };

        foreach (var button in buttons)
        {
            button.Classes.Set("active", ReferenceEquals(button, target));
        }
    }

    private AppLanguage SelectedLanguage()
    {
        if (LangZhButton.Classes.Contains("active"))
        {
            return AppLanguage.Chinese;
        }

        return LangEnButton.Classes.Contains("active") ? AppLanguage.English : AppLanguage.System;
    }

    private void Language_Click(object? sender, RoutedEventArgs e)
    {
        if (_loading || sender is not Button button)
        {
            return;
        }

        var language = (button.Tag as string) switch
        {
            "zh" => AppLanguage.Chinese,
            "en" => AppLanguage.English,
            _ => AppLanguage.System,
        };

        if (language == _draft.Language)
        {
            return;
        }

        _draft.Language = language;
        Loc.Apply(language);

        // 文案是加载期解析的，换语言要把窗口重建一遍
        _controller.ReopenSettings(_draft, 2, this);
    }

    // ── 列表 ─────────────────────────────────────────────────────────────

    private void ReloadAccounts(Guid? selectId)
    {
        var rows = new List<AccountRow>();

        foreach (var account in _draft.Accounts)
        {
            var (subtitle, dot) = DescribeAccount(account);
            var title = account.Id == _draft.ActiveAccountId
                ? $"{account.Name}  ·  {Loc.T("Status.Default")}"
                : account.Name;
            rows.Add(new AccountRow { Account = account, Title = title, Subtitle = subtitle, Dot = dot });
        }

        AccountList.ItemsSource = rows;

        var selected = rows.FirstOrDefault(r => r.Account.Id == selectId) ?? rows.FirstOrDefault();
        AccountList.SelectedItem = selected;
        _selectedAccountId = selected?.Account.Id;

        ReloadKeys();
    }

    private (string Text, IBrush Dot) DescribeAccount(AccountProfile account)
    {
        var data = account.LastBalance;
        if (data is null)
        {
            return (Loc.T("Status.NotFetched"), Brushes.Gray);
        }

        if (!data.HasData)
        {
            return (data.Error ?? Loc.T("Status.Failed"), new SolidColorBrush(Color.Parse("#EF5350")));
        }

        var level = BalanceTier.Classify(data.Total, _draft.Tiers);
        var hex = BalanceTier.HexOf(level, _draft.Tiers);
        var today = account.Today?.Spent ?? 0m;
        var text = Loc.F("Status.TodayFmt", AppController.Money(data.Total, data.Currency),
            AppController.Money(today, data.Currency));
        return (text, ColorHelper.Brush(hex));
    }

    private void ReloadKeys()
    {
        var account = SelectedAccount;
        var rows = new List<KeyRow>();

        if (account is not null)
        {
            foreach (var key in account.Keys)
            {
                var state = key.IsRevoked
                    ? Loc.T("Status.Revoked")
                    : key.Id == account.ActiveKeyId
                        ? Loc.T("Status.Active")
                        : Loc.T("Status.Backup");

                var subtitle = $"{key.Hint} · {state}";
                if (!string.IsNullOrWhiteSpace(key.LastError))
                {
                    subtitle += " · " + key.LastError;
                }

                var dot = key.IsRevoked
                    ? new SolidColorBrush(Color.Parse("#EF5350"))
                    : key.Id == account.ActiveKeyId
                        ? new SolidColorBrush(Color.Parse("#2FBF71"))
                        : new SolidColorBrush(Color.Parse("#9CA3AF"));

                rows.Add(new KeyRow { Key = key, Title = key.Name, Subtitle = subtitle, Dot = dot });
            }
        }

        KeyList.ItemsSource = rows;
        KeyList.SelectedIndex = rows.Count > 0 ? 0 : -1;
    }

    private void AccountList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectedAccountId = SelectedAccount?.Id;
        ReloadKeys();
    }

    // ── 账号操作 ─────────────────────────────────────────────────────────

    private async void AddAccount_Click(object? sender, RoutedEventArgs e)
    {
        var name = await Dialogs.Prompt(
            this,
            Loc.T("Dlg.AddAccount"),
            Loc.T("Dlg.NameLabel"),
            Loc.F("Dlg.AccountDefaultFmt", _draft.Accounts.Count + 1));

        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var key = await Dialogs.Prompt(
            this,
            Loc.T("Dlg.AddAccount"),
            Loc.T("Dlg.KeyLabel"),
            string.Empty,
            Loc.T("Dlg.KeyHint"));

        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var plain = key.Trim();
        var snapshot = await ValidateAsync(plain);
        if (snapshot is null)
        {
            return;
        }

        var twin = FindTwin(snapshot);
        if (twin is not null)
        {
            var merge = await Dialogs.Confirm(
                this,
                Loc.T("Dlg.SameAccountTitle"),
                Loc.F("Dlg.SameAccountTextFmt", twin.Name,
                    AppController.Money(snapshot.Total, snapshot.Currency)),
                Loc.T("Dlg.AddToAccount"),
                Loc.T("Dlg.KeepSeparate"));

            if (merge)
            {
                var entry = CreateKey(plain, $"Key {twin.Keys.Count + 1}");
                twin.Keys.Add(entry);
                if (twin.ActiveKeyId is null)
                {
                    twin.ActiveKeyId = entry.Id;
                }

                twin.LastBalance = snapshot;
                ReloadAccounts(twin.Id);
                return;
            }
        }

        var account = new AccountProfile
        {
            Name = name.Trim(),
            Currency = snapshot.Currency,
            LastBalance = snapshot,
        };

        var firstKey = CreateKey(plain, "Key 1");
        account.Keys.Add(firstKey);
        account.ActiveKeyId = firstKey.Id;

        _draft.Accounts.Add(account);
        if (_draft.ActiveAccountId is null)
        {
            _draft.ActiveAccountId = account.Id;
        }

        ReloadAccounts(account.Id);
    }

    private async void AddKey_Click(object? sender, RoutedEventArgs e)
    {
        var account = SelectedAccount;
        if (account is null)
        {
            await Dialogs.ShowMessage(this, Loc.T("Common.Notice"), Loc.T("Dlg.SelectAccountFirst"));
            return;
        }

        var key = await Dialogs.Prompt(
            this,
            Loc.T("Dlg.AddKey"),
            Loc.F("Dlg.AddKeyLabelFmt", account.Name),
            string.Empty,
            Loc.T("Dlg.AddKeyHint"));

        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var plain = key.Trim();
        var snapshot = await ValidateAsync(plain);
        if (snapshot is null)
        {
            return;
        }

        if (account.LastBalance is { HasData: true } current && current.Total != snapshot.Total)
        {
            var goOn = await Dialogs.Confirm(
                this,
                Loc.T("Dlg.MismatchTitle"),
                Loc.F("Dlg.MismatchTextFmt", AppController.Money(snapshot.Total, snapshot.Currency)));

            if (!goOn)
            {
                return;
            }
        }

        var entry = CreateKey(plain, $"Key {account.Keys.Count + 1}");
        account.Keys.Add(entry);
        if (account.ActiveKeyId is null)
        {
            account.ActiveKeyId = entry.Id;
        }

        account.LastBalance = snapshot;
        ReloadAccounts(account.Id);
    }

    private async void RenameAccount_Click(object? sender, RoutedEventArgs e)
    {
        var account = SelectedAccount;
        if (account is null)
        {
            return;
        }

        var name = await Dialogs.Prompt(this, Loc.T("Dlg.Rename"), Loc.T("Dlg.RenameLabel"), account.Name);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        account.Name = name.Trim();
        ReloadAccounts(account.Id);
    }

    private void SetDefaultAccount_Click(object? sender, RoutedEventArgs e)
    {
        var account = SelectedAccount;
        if (account is null)
        {
            return;
        }

        _draft.ActiveAccountId = account.Id;
        ReloadAccounts(account.Id);
    }

    private async void DeleteAccount_Click(object? sender, RoutedEventArgs e)
    {
        var account = SelectedAccount;
        if (account is null)
        {
            return;
        }

        if (!await Dialogs.Confirm(this, Loc.T("Dlg.DeleteAccountTitle"),
                Loc.F("Dlg.DeleteAccountTextFmt", account.Name, account.Keys.Count)))
        {
            return;
        }

        _draft.Accounts.Remove(account);
        if (_draft.ActiveAccountId == account.Id)
        {
            _draft.ActiveAccountId = _draft.Accounts.FirstOrDefault()?.Id;
        }

        ReloadAccounts(_draft.Accounts.FirstOrDefault()?.Id);
    }

    // ── Key 操作 ─────────────────────────────────────────────────────────

    private void SetActiveKey_Click(object? sender, RoutedEventArgs e)
    {
        var account = SelectedAccount;
        if (account is null || KeyList.SelectedItem is not KeyRow row)
        {
            return;
        }

        row.Key.IsRevoked = false;
        row.Key.LastError = null;
        account.ActiveKeyId = row.Key.Id;
        ReloadKeys();
    }

    private async void TestKey_Click(object? sender, RoutedEventArgs e)
    {
        if (KeyList.SelectedItem is not KeyRow row)
        {
            await Dialogs.ShowMessage(this, Loc.T("Common.Notice"), Loc.T("Dlg.SelectKeyFirst"));
            return;
        }

        var plain = Dpapi.Unprotect(row.Key.ProtectedKey);
        if (string.IsNullOrWhiteSpace(plain))
        {
            await Dialogs.ShowMessage(this, Loc.T("Common.Notice"), Loc.T("Dlg.KeyUndecryptable"));
            return;
        }

        var snapshot = await ValidateAsync(plain);
        if (snapshot is null)
        {
            return;
        }

        await Dialogs.ShowMessage(
            this,
            Loc.T("Dlg.TestTitle"),
            Loc.F("Dlg.TestOkFmt",
                AppController.Money(snapshot.Total, snapshot.Currency),
                Loc.T(snapshot.IsAvailable ? "Dlg.Available" : "Dlg.Insufficient")));
    }

    private async void DeleteKey_Click(object? sender, RoutedEventArgs e)
    {
        var account = SelectedAccount;
        if (account is null || KeyList.SelectedItem is not KeyRow row)
        {
            return;
        }

        if (account.Keys.Count <= 1)
        {
            await Dialogs.ShowMessage(this, Loc.T("Common.Notice"), Loc.T("Dlg.KeepOneKey"));
            return;
        }

        if (!await Dialogs.Confirm(this, Loc.T("Dlg.DeleteAccountTitle"), Loc.F("Dlg.DeleteKeyTextFmt", row.Key.Name)))
        {
            return;
        }

        account.Keys.Remove(row.Key);
        if (account.ActiveKeyId == row.Key.Id)
        {
            account.ActiveKeyId = account.Keys.FirstOrDefault()?.Id;
        }

        ReloadKeys();
    }

    // ── 颜色 ─────────────────────────────────────────────────────────────

    private async void GreenColor_Click(object? sender, RoutedEventArgs e)
        => await PickColor(_draft.Tiers.GreenColor, hex => { _draft.Tiers.GreenColor = hex; UpdateSwatch(GreenSwatch, hex); });

    private async void YellowColor_Click(object? sender, RoutedEventArgs e)
        => await PickColor(_draft.Tiers.YellowColor, hex => { _draft.Tiers.YellowColor = hex; UpdateSwatch(YellowSwatch, hex); });

    private async void RedColor_Click(object? sender, RoutedEventArgs e)
        => await PickColor(_draft.Tiers.RedColor, hex => { _draft.Tiers.RedColor = hex; UpdateSwatch(RedSwatch, hex); });

    private async Task PickColor(string current, Action<string> apply)
    {
        var input = await Dialogs.Prompt(this, Loc.T("Dlg.ColorTitle"), Loc.T("Dlg.ColorLabel"), current);
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        if (!Color.TryParse(input.Trim(), out var color))
        {
            await Dialogs.ShowMessage(this, Loc.T("Common.Notice"), Loc.T("Dlg.ColorInvalid"));
            return;
        }

        apply(ColorHelper.ToHex(color));
    }

    private static void UpdateSwatch(Button button, string hex)
    {
        var color = ColorHelper.Parse(hex);
        button.Background = new SolidColorBrush(color);
        button.Foreground = new SolidColorBrush(ColorHelper.PickReadable(color));
        button.Content = hex.ToUpperInvariant();
    }

    // ── 保存 / 取消 ──────────────────────────────────────────────────────

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (!int.TryParse(IntervalBox.Text?.Trim(), out var minutes) || minutes < 1 || minutes > 1440)
        {
            await Dialogs.ShowMessage(this, Loc.T("Common.Notice"), Loc.T("Validation.Interval"));
            return;
        }

        if (!TryParseAmount(GreenFromBox.Text, out var green) || !TryParseAmount(YellowFromBox.Text, out var yellow))
        {
            await Dialogs.ShowMessage(this, Loc.T("Common.Notice"), Loc.T("Validation.Numbers"));
            return;
        }

        if (green <= yellow || yellow < 0)
        {
            await Dialogs.ShowMessage(this, Loc.T("Common.Notice"), Loc.T("Validation.Order"));
            return;
        }

        if (!TryParseAmount(AlertBox.Text, out var alert) || alert < 0)
        {
            await Dialogs.ShowMessage(this, Loc.T("Common.Notice"), Loc.T("Validation.Alert"));
            return;
        }

        _draft.RefreshIntervalMinutes = minutes;
        _draft.Tiers.GreenFrom = green;
        _draft.Tiers.YellowFrom = yellow;
        _draft.AlertBelow = alert;
        _draft.WarnOnLowBalance = WarnCheck.IsChecked == true;
        _draft.StartWithWindows = AutostartCheck.IsChecked == true;
        _draft.ShowPopupOnHover = PopupCheck.IsChecked == true;
        _draft.Theme = SelectedTheme();
        _draft.Language = SelectedLanguage();

        if (_draft.Accounts.Count > 0
            && (_draft.ActiveAccountId is null || _draft.Accounts.All(a => a.Id != _draft.ActiveAccountId)))
        {
            _draft.ActiveAccountId = _draft.Accounts[0].Id;
        }

        _saved = true;
        _controller.ApplySettings(_draft);
        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close();

    // ── 辅助 ─────────────────────────────────────────────────────────────

    private async Task<BalanceSnapshot?> ValidateAsync(string apiKey)
    {
        try
        {
            return await _controller.Api.GetBalanceAsync(apiKey, default);
        }
        catch (Exception ex)
        {
            await Dialogs.ShowMessage(this, Loc.T("Dlg.ValidationFailed"), ex.Message);
            return null;
        }
    }

    private static ApiKeyEntry CreateKey(string plainText, string name) => new()
    {
        Name = name,
        ProtectedKey = Dpapi.Protect(plainText),
        Hint = AppController.MaskKey(plainText),
        LastCheckedAt = DateTimeOffset.Now,
    };

    private AccountProfile? FindTwin(BalanceSnapshot snapshot)
        => _draft.Accounts.FirstOrDefault(a =>
            a.LastBalance is { HasData: true } data
            && string.Equals(data.Currency, snapshot.Currency, StringComparison.OrdinalIgnoreCase)
            && data.Total == snapshot.Total
            && data.Granted == snapshot.Granted
            && data.ToppedUp == snapshot.ToppedUp);

    private static bool TryParseAmount(string? text, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var cleaned = text.Trim().Replace("¥", string.Empty).Replace("￥", string.Empty).Replace(",", string.Empty);

        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out value)
               || decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.CurrentCulture, out value);
    }

    private void Console_Click(object? sender, RoutedEventArgs e)
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

    private void About_Click(object? sender, RoutedEventArgs e)
        => new AboutWindow().ShowDialog(this);

    private sealed class AccountRow
    {
        public required AccountProfile Account { get; init; }

        public required string Title { get; init; }

        public required string Subtitle { get; init; }

        public required IBrush Dot { get; init; }
    }

    private sealed class KeyRow
    {
        public required ApiKeyEntry Key { get; init; }

        public required string Title { get; init; }

        public required string Subtitle { get; init; }

        public required IBrush Dot { get; init; }
    }
}
