#if DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using DeepSeekTray.Models;
using DeepSeekTray.Services;
using DeepSeekTray.Views;

namespace DeepSeekTray;

/// <summary>
/// 仅 Debug：--memtest &lt;log&gt; 反复创建/销毁窗口，采样内存与句柄，
/// 用来判断“长时间运行是否累加”。
/// </summary>
internal static class MemTest
{
    public static async Task RunAsync(string logPath)
    {
        var controller = new AppController();
        var lines = new List<string>();

        void Sample(string tag)
        {
            var process = Process.GetCurrentProcess();
            process.Refresh();

            var ws = process.WorkingSet64 / 1048576.0;
            var priv = process.PrivateMemorySize64 / 1048576.0;
            var managed = GC.GetTotalMemory(false) / 1048576.0;

            lines.Add($"{tag}\tWS={ws:F1}MB\tPriv={priv:F1}MB\tManaged={managed:F2}MB\t" +
                      $"Handles={process.HandleCount}\tThreads={process.Threads.Count}\t" +
                      $"GC={GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)}");
        }

        await Task.Delay(800);
        Sample("start");

        for (var i = 1; i <= 40; i++)
        {
            var popup = new InfoPopupWindow(ThemeVariant.Light)
            {
                Position = new PixelPoint(-20000, -20000),
            };
            popup.Render(DemoRows(), "17:18 更新", "今日合计", "-¥3.42");
            popup.Show();
            popup.UpdateLayout();
            await Task.Delay(50);
            popup.Close();

            if (i % 5 == 0)
            {
                var settings = new SettingsWindow(controller, null)
                {
                    Position = new PixelPoint(-20000, -20000),
                };
                settings.Show();
                settings.UpdateLayout();
                await Task.Delay(60);
                settings.Close();
            }

            if (i % 10 == 0)
            {
                var menu = new TrayMenuWindow(ThemeVariant.Light, "DeepSeek 余额托盘", "今日消耗 -¥3.42 · 5 分钟刷新", new[]
                {
                    new TrayMenuItem("\uE72C", "立即刷新", () => { }),
                    new TrayMenuItem("\uE713", "设置…", () => { }),
                    new TrayMenuItem("\uE7E8", "退出", () => { }, Danger: true),
                })
                {
                    Position = new PixelPoint(-20000, -20000),
                };
                menu.Show();
                menu.UpdateLayout();
                await Task.Delay(60);
                menu.Close();
            }

            await Task.Delay(120);

            if (i % 5 == 0 || i == 1)
            {
                Sample($"iter {i,2}");
            }
        }

        // 强制回收，看工作集里有多少是可回收垃圾
        GC.Collect(2, GCCollectionMode.Forced, true, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, true, true);
        await Task.Delay(500);
        Sample("after force GC");

        File.WriteAllLines(logPath, lines);
        controller.Dispose();

        Dispatcher.UIThread.Post(() => (Application.Current?.ApplicationLifetime as
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown());
    }

    private static List<PopupRow> DemoRows() => new()
    {
        new PopupRow
        {
            Name = "个人主力",
            Amount = "¥128.60",
            Detail = "赠送 ¥28.60 · 充值 ¥100.00",
            Today = "-¥2.14",
            AccentHex = "#1DB954",
            IsActive = true,
        },
        new PopupRow
        {
            Name = "备用号",
            Amount = "¥42.30",
            Today = "-¥0.78",
            AccentHex = "#F5A524",
        },
    };
}
#endif
