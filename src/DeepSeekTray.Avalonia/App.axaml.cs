using System;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using DeepSeekTray.Services;

namespace DeepSeekTray;

public partial class App : Application
{
    private const string MutexName = "DeepSeekTray.SingleInstance";
    private const string SignalName = @"Local\DeepSeekTray.OpenSettings";

    private Mutex? _mutex;
    private EventWaitHandle? _signal;
    private Thread? _signalThread;
    private AppController? _controller;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var args = Environment.GetCommandLineArgs();

            // 自检在 Release 构建里也保留：用来验证发布出来的产物是否正常
            if (args.Any(a => string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase)))
            {
                var passed = SelfTest.Run();
                Dispatcher.UIThread.Post(() => desktop.Shutdown(passed ? 0 : 1), DispatcherPriority.Background);
                base.OnFrameworkInitializationCompleted();
                return;
            }

#if DEBUG
            if (args.Length >= 3 && string.Equals(args[1], "--memtest", StringComparison.OrdinalIgnoreCase))
            {
                _ = MemTest.RunAsync(args[2]);
                base.OnFrameworkInitializationCompleted();
                return;
            }

            if (args.Length >= 3 && string.Equals(args[1], "--preview", StringComparison.OrdinalIgnoreCase))
            {
                PreviewMode.Run(args[2]);
                Dispatcher.UIThread.Post(() => desktop.Shutdown(), DispatcherPriority.Background);
                base.OnFrameworkInitializationCompleted();
                return;
            }
#endif

            _mutex = new Mutex(true, MutexName, out var isFirstInstance);
            if (!isFirstInstance)
            {
                try
                {
                    if (EventWaitHandle.TryOpenExisting(SignalName, out var existing))
                    {
                        existing.Set();
                        existing.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("唤醒已有实例失败：" + ex.Message);
                }

                desktop.Shutdown();
                base.OnFrameworkInitializationCompleted();
                return;
            }

            Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                Log.Error("未处理异常\n" + e.Exception);
                e.Handled = true;
            };

            _controller = new AppController();
            _controller.Start();
            ListenForActivation();

            desktop.Exit += (_, _) =>
            {
                Log.Info("退出");
                _controller?.Dispose();
                _signal?.Dispose();
                _mutex?.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ListenForActivation()
    {
        try
        {
            _signal = new EventWaitHandle(false, EventResetMode.AutoReset, SignalName);
            _signalThread = new Thread(() =>
            {
                try
                {
                    while (_signal.WaitOne())
                    {
                        Dispatcher.UIThread.Post(() => _controller?.RequestOpenSettings());
                    }
                }
                catch (ObjectDisposedException)
                {
                    // 退出时正常
                }
            })
            {
                IsBackground = true,
                Name = "DeepSeekTray.Activation",
            };
            _signalThread.Start();
        }
        catch (Exception ex)
        {
            Log.Error("创建激活信号失败", ex);
        }
    }
}
