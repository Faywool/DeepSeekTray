using System;
using Avalonia;

namespace DeepSeekTray;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            // 托盘小工具：窗口内容静态、无动画，用软件渲染省掉 ANGLE/GL 的显存与映射开销
            .With(new Win32PlatformOptions
            {
                RenderingMode = new[] { Win32RenderingMode.Software },
            })
            .UsePlatformDetect()
            .LogToTrace();
}
