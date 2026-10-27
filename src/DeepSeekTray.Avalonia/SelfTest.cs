using System;
using System.IO;
using System.Text;
using DeepSeekTray.Localization;
using DeepSeekTray.Models;
using DeepSeekTray.Services;

namespace DeepSeekTray;

/// <summary>无界面自检：--selftest。覆盖分级、JSON 解析、DPAPI、脱敏、图标生成。</summary>
internal static class SelfTest
{
    private const string SampleJson = """
    {
      "is_available": true,
      "balance_infos": [
        { "currency": "CNY", "total_balance": "110.00", "granted_balance": "10.00", "topped_up_balance": "100.00" }
      ]
    }
    """;

    private const string MultiCurrencyJson = """
    {
      "is_available": false,
      "balance_infos": [
        { "currency": "USD", "total_balance": "3.50", "granted_balance": "0.00", "topped_up_balance": "3.50" },
        { "currency": "CNY", "total_balance": "25.60", "granted_balance": "5.00", "topped_up_balance": "20.60" }
      ]
    }
    """;

    public static bool Run()
    {
        var output = new StringBuilder();
        var failures = 0;

        void Check(string name, bool ok, string detail = "")
        {
            if (!ok)
            {
                failures++;
            }

            output.AppendLine($"[{(ok ? "PASS" : "FAIL")}] {name}{(detail.Length > 0 ? " :: " + detail : string.Empty)}");
        }

        var tiers = new TierSettings();
        Check("余额 100 -> 绿色", BalanceTier.Classify(100m, tiers) == TierLevel.Green);
        Check("余额 20 -> 黄色", BalanceTier.Classify(20m, tiers) == TierLevel.Yellow);
        Check("余额 19.99 -> 红色", BalanceTier.Classify(19.99m, tiers) == TierLevel.Red);
        Check("Worst 取红色", BalanceTier.Worst(new[] { TierLevel.Green, TierLevel.Red }) == TierLevel.Red);
        Check("Worst 全未知", BalanceTier.Worst(new[] { TierLevel.Unknown }) == TierLevel.Unknown);

        var custom = new TierSettings { GreenFrom = 50m, YellowFrom = 10m };
        Check("自定义阈值 50 -> 绿色", BalanceTier.Classify(50m, custom) == TierLevel.Green);

        var snapshot = DeepSeekApiClient.Parse(SampleJson);
        Check("解析 total", snapshot.Total == 110m, snapshot.Total.ToString());
        Check("解析 granted", snapshot.Granted == 10m);
        Check("解析 topped_up", snapshot.ToppedUp == 100m);
        Check("解析 is_available", snapshot.IsAvailable);
        Check("解析 currency", snapshot.Currency == "CNY");

        var multi = DeepSeekApiClient.Parse(MultiCurrencyJson);
        Check("多币种优先 CNY", multi.Currency == "CNY" && multi.Total == 25.60m, $"{multi.Currency}/{multi.Total}");
        Check("is_available=false", !multi.IsAvailable);

        const string secret = "sk-1234567890abcdef1234567890abcdef";
        var protectedKey = Dpapi.Protect(secret);
        Check("DPAPI 非明文", protectedKey != secret && protectedKey.Length > 0);
        Check("DPAPI 往返", Dpapi.Unprotect(protectedKey) == secret);
        Check("DPAPI 非法输入", Dpapi.Unprotect("not-base64!!") is null);

        Check("MaskKey", AppController.MaskKey(secret) == "sk-1…cdef", AppController.MaskKey(secret));
        Check("Money CNY", AppController.Money(1234.5m, "CNY") == "¥1,234.50", AppController.Money(1234.5m, "CNY"));
        Check("Money USD", AppController.Money(3.5m, "USD") == "$3.50");

        var green = ColorHelper.Parse("#1DB954");
        Check("颜色解析", green.R == 0x1D && green.G == 0xB9 && green.B == 0x54);
        Check("颜色回退", ColorHelper.Parse("不是颜色") == Avalonia.Media.Colors.Gray);

        try
        {
            var path = IconFactory.GetIcoPath(green);
            Check("托盘图标 ICO", File.Exists(path) && new FileInfo(path).Length > 1024, $"{new FileInfo(path).Length} bytes");
        }
        catch (Exception ex)
        {
            Check("托盘图标 ICO", false, ex.Message);
        }

        Loc.Apply(AppLanguage.Chinese);
        Check("文案：中文", Loc.T("Menu.Exit") == "退出", Loc.T("Menu.Exit"));
        Loc.Apply(AppLanguage.English);
        Check("文案：英文", Loc.T("Menu.Exit") == "Exit", Loc.T("Menu.Exit"));
        Loc.Apply(AppLanguage.System);

        output.AppendLine();
        output.AppendLine(failures == 0 ? "SELFTEST OK" : $"SELFTEST FAILED（{failures} 项）");

        var text = output.ToString();
        try
        {
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "deepseek-tray-selftest.log"), text, Encoding.UTF8);
        }
        catch
        {
            // 忽略
        }

        try
        {
            Native.AttachConsole(-1);
            var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };
            Console.SetOut(stdout);
            stdout.Write(text);
        }
        catch
        {
            // WinExe 无控制台
        }

        return failures == 0;
    }
}
