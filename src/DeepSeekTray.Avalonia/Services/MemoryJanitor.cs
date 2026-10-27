using System;
using System.Diagnostics;
using Avalonia.Threading;

namespace DeepSeekTray.Services;

/// <summary>
/// 空闲回收：托盘程序大部分时间没有窗口，定期把工作集还给系统，
/// 并释放长期不用的浮层窗口，避免“开着越久占用越高”的观感。
/// </summary>
internal sealed class MemoryJanitor : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Func<bool> _isIdle;
    private readonly Action _releaseCaches;
    private readonly TimeSpan _minInterval;
    private bool _disposed;

    /// <summary>调试用：DTS_FAST_TRIM=1 时缩短所有阈值。</summary>
    internal static bool FastMode { get; } =
#if DEBUG
        Environment.GetEnvironmentVariable("DTS_FAST_TRIM") == "1";
#else
        false;
#endif

    public MemoryJanitor(Func<bool> isIdle, Action releaseCaches)
    {
        _isIdle = isIdle;
        _releaseCaches = releaseCaches;

        _minInterval = FastMode ? TimeSpan.FromSeconds(10) : TimeSpan.FromMinutes(5);

        _timer = new DispatcherTimer
        {
            Interval = FastMode ? TimeSpan.FromSeconds(3) : TimeSpan.FromSeconds(45),
        };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    private void Tick()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (!_isIdle())
            {
                return;
            }

            _releaseCaches();

            var before = Process.GetCurrentProcess().WorkingSet64;
            GC.Collect(2, GCCollectionMode.Optimized, blocking: false, compacting: false);
            Native.TrimWorkingSet();

            Process.GetCurrentProcess().Refresh();
            var after = Process.GetCurrentProcess().WorkingSet64;

            Log.Info($"空闲回收：工作集 {before / 1048576} MB → {after / 1048576} MB");
        }
        catch (Exception ex)
        {
            Log.Warn("空闲回收失败：" + ex.Message);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Stop();
    }
}
