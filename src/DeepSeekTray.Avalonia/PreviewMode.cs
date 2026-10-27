#if DEBUG
#pragma warning disable CS0618 // Avalonia 12 把 Bitmap.Save 标为过时，仅调试截图使用
using System;
using System.IO;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using DeepSeekTray.Localization;
using DeepSeekTray.Models;
using DeepSeekTray.Services;
using DeepSeekTray.Views;

namespace DeepSeekTray;

/// <summary>仅 Debug：--preview &lt;dir&gt; 离屏渲染各窗口，用于无交互环境下的 UI 校验。</summary>
internal static class PreviewMode
{
    public static void Run(string directory)
    {
        Directory.CreateDirectory(directory);

        using var controller = new AppController();
        var demo = BuildDemoSettings();

        Loc.Apply(AppLanguage.Chinese);
        Render(controller, directory, "zh", AppTheme.Light, demo);

        Loc.Apply(AppLanguage.English);
        Render(controller, directory, "en", AppTheme.Dark, demo);
    }

    private static void Render(AppController controller, string directory, string suffix, AppTheme theme, AppSettings demo)
    {
        controller.ApplyTheme(theme);

        var sections = new[] { "accounts", "refresh", "appearance", "about" };

        // 演示设置的主题必须和当前渲染的主题一致，否则关窗时的“取消回滚”会把主题改回系统
        var themed = AppSettings.Clone(demo);
        themed.Theme = theme;

        var settings = new SettingsWindow(controller, themed);
        settings.Show();
        settings.Position = new PixelPoint(-20000, -20000);

        for (var i = 0; i < sections.Length; i++)
        {
            settings.SelectSection(i);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            settings.UpdateLayout();
            ShotOpen(settings, Path.Combine(directory, $"settings-{sections[i]}-{suffix}.png"));
        }

        settings.Close();

        var variant = Application.Current?.ActualThemeVariant ?? Avalonia.Styling.ThemeVariant.Default;
        var popup = new InfoPopupWindow(variant);
        popup.Render(Rows(showActive: true), Loc.F("Popup.UpdatedFmt", "17:18"), Loc.T("Popup.Total"), "-¥3.42");
        var single = new InfoPopupWindow(variant);
        single.Render(Rows(showActive: false).GetRange(0, 1), Loc.F("Popup.UpdatedFmt", "17:18"));
        Shot(single, Path.Combine(directory, $"popup-single-{suffix}.png"));
        Shot(popup, Path.Combine(directory, $"popup-{suffix}.png"));

        var menu = new TrayMenuWindow(variant, Loc.T("Menu.Title"), Loc.F("Menu.SummaryFmt", "-¥3.42", 5), new[]
        {
            new TrayMenuItem("\uE72C", Loc.T("Menu.Refresh"), () => { }),
            new TrayMenuItem("\uE713", Loc.T("Menu.Settings"), () => { }),
            new TrayMenuItem("\uE8A7", Loc.T("Menu.Console"), () => { }),
            new TrayMenuItem("\uE946", Loc.T("Menu.About"), () => { }),
            new TrayMenuItem("\uE7E8", Loc.T("Menu.Exit"), () => { }, Danger: true),
        });
        Shot(menu, Path.Combine(directory, $"menu-{suffix}.png"));

        Shot(new AboutWindow(), Path.Combine(directory, $"about-{suffix}.png"));
    }

    private static void ShotOpen(Window window, string path)
    {
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        var width = Math.Max(1, (int)Math.Ceiling(window.Bounds.Width));
        var height = Math.Max(1, (int)Math.Ceiling(window.Bounds.Height));

        var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        bitmap.Render(window);
        bitmap.Save(path); // 旧重载在 Avalonia 12 标记过时，调试工具无所谓
    }

    private static void Shot(Window window, string path, bool close = true)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Position = new PixelPoint(-20000, -20000);
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        var width = Math.Max(1, (int)Math.Ceiling(window.Bounds.Width));
        var height = Math.Max(1, (int)Math.Ceiling(window.Bounds.Height));

        var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        bitmap.Render(window);
        bitmap.Save(path); // 旧重载在 Avalonia 12 标记过时，调试工具无所谓

        if (close)
        {
            window.Close();
        }
    }

    private static List<PopupRow> Rows(bool showActive) => new()
    {
        new PopupRow
        {
            Name = "个人主力",
            Amount = "¥128.60",
            Detail = $"{Loc.F("Popup.GrantedFmt", "¥28.60")} · {Loc.F("Popup.ToppedFmt", "¥100.00")}",
            Time = "15:20:31",
            Today = "-¥2.14",
            AccentHex = "#1DB954",
            IsActive = showActive,
        },
        new PopupRow
        {
            Name = "备用号",
            Amount = "¥42.30",
            Detail = $"{Loc.F("Popup.GrantedFmt", "¥2.30")} · {Loc.F("Popup.ToppedFmt", "¥40.00")}",
            Time = "15:20:31",
            Today = "-¥0.78",
            AccentHex = "#F5A524",
        },
        new PopupRow
        {
            Name = "测试号",
            Amount = "¥8.75",
            Detail = string.Empty,
            Time = "15:20:31",
            Today = "-¥0.50",
            AccentHex = "#E5484D",
        },
    };

    private static AppSettings BuildDemoSettings()
    {
        var settings = new AppSettings { RefreshIntervalMinutes = 5, AlertBelow = 20m, Theme = AppTheme.System };
        var today = DateTimeOffset.Now.ToString("yyyy-MM-dd");

        var primary = new AccountProfile
        {
            Name = "个人主力",
            Currency = "CNY",
            Today = new DailyUsage { Date = today, Spent = 2.14m, LastTotal = 128.60m },
            LastBalance = new BalanceSnapshot
            {
                IsAvailable = true,
                Currency = "CNY",
                Total = 128.60m,
                Granted = 28.60m,
                ToppedUp = 100m,
                FetchedAt = DateTimeOffset.Now,
            },
        };
        var mainKey = new ApiKeyEntry { Name = "主 Key", Hint = "sk-9…1a2b" };
        var backup = new ApiKeyEntry { Name = "Key 2", Hint = "sk-4…8c9d" };
        primary.Keys.Add(mainKey);
        primary.Keys.Add(backup);
        primary.ActiveKeyId = mainKey.Id;

        var secondary = new AccountProfile
        {
            Name = "备用号",
            Currency = "CNY",
            Today = new DailyUsage { Date = today, Spent = 0.78m, LastTotal = 42.30m },
            LastBalance = new BalanceSnapshot
            {
                IsAvailable = true,
                Currency = "CNY",
                Total = 42.30m,
                Granted = 2.30m,
                ToppedUp = 40m,
                FetchedAt = DateTimeOffset.Now,
            },
        };
        var key2 = new ApiKeyEntry { Name = "Key 1", Hint = "sk-7…3f4e" };
        secondary.Keys.Add(key2);
        secondary.ActiveKeyId = key2.Id;

        var testing = new AccountProfile
        {
            Name = "测试号",
            Currency = "CNY",
            Today = new DailyUsage { Date = today, Spent = 0.50m, LastTotal = 8.75m },
            LastBalance = new BalanceSnapshot
            {
                IsAvailable = false,
                Currency = "CNY",
                Total = 8.75m,
                Granted = 0m,
                ToppedUp = 8.75m,
                FetchedAt = DateTimeOffset.Now,
            },
        };
        var key3 = new ApiKeyEntry { Name = "Key 1", Hint = "sk-2…7g8h" };
        testing.Keys.Add(key3);
        testing.ActiveKeyId = key3.Id;

        settings.Accounts.Add(primary);
        settings.Accounts.Add(secondary);
        settings.Accounts.Add(testing);
        settings.ActiveAccountId = primary.Id;
        return settings;
    }
}
#pragma warning restore CS0618
#endif
