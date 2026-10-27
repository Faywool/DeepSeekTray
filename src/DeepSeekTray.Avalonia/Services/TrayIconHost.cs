using System;
using System.Runtime.InteropServices;

namespace DeepSeekTray.Services;

/// <summary>
/// 原生托盘图标（Shell_NotifyIcon v4）。
/// Avalonia 自带的 TrayIcon 只有 Clicked / 原生菜单 / 原生 tooltip，
/// 没有悬停事件，也没法自绘菜单，所以这里直接用 Win32。
/// </summary>
public sealed class TrayIconHost : IDisposable
{
    private const string ClassName = "DeepSeekTrayTrayWindow";
    private const int IconId = 1;

    private readonly Native.WndProcDelegate _wndProc;
    private IntPtr _hwnd;
    private IntPtr _hicon;
    private string? _iconPath;
    private bool _added;
    private bool _disposed;

    public event Action? Hovered;

    public event Action? HoverClosed;

    public event Action? LeftClicked;

    public event Action? RightClicked;

    public TrayIconHost()
    {
        _wndProc = WndProc;

        var wndClass = new Native.WndClassEx
        {
            cbSize = Marshal.SizeOf<Native.WndClassEx>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = Native.GetModuleHandle(null),
            lpszClassName = ClassName,
        };

        Native.RegisterClassEx(ref wndClass);

        _hwnd = Native.CreateWindowEx(
            0, ClassName, string.Empty, 0, 0, 0, 0, 0,
            Native.HwndMessage, IntPtr.Zero, wndClass.hInstance, IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            Log.Error("创建托盘消息窗口失败：" + Marshal.GetLastWin32Error());
        }
    }

    public void SetIcon(Avalonia.Media.Color color)
    {
        if (_disposed || _hwnd == IntPtr.Zero)
        {
            return;
        }

        var path = IconFactory.GetIcoPath(color);

        if (_hicon != IntPtr.Zero && path == _iconPath)
        {
            return;
        }

        var size = GetIconSize();
        var hicon = Native.LoadImage(IntPtr.Zero, path, Native.ImageIcon, size, size, Native.LrLoadFromFile);

        if (hicon == IntPtr.Zero)
        {
            Log.Warn("加载托盘图标失败：" + Marshal.GetLastWin32Error());
            return;
        }

        if (_hicon != IntPtr.Zero)
        {
            Native.DestroyIcon(_hicon);
        }

        _hicon = hicon;
        _iconPath = path;

        var data = CreateData();
        data.uFlags = Native.NifIcon | Native.NifTip | Native.NifMessage | Native.NifShowTip;
        data.hIcon = _hicon;
        data.szTip = string.Empty;

        if (!_added)
        {
            data.uFlags |= Native.NifIcon;
            if (Native.Shell_NotifyIcon(Native.NimAdd, ref data))
            {
                _added = true;

                var version = CreateData();
                version.uTimeoutOrVersion = Native.NotifyIconVersion4;
                Native.Shell_NotifyIcon(Native.NimSetVersion, ref version);
            }
            else
            {
                Log.Error("添加托盘图标失败：" + Marshal.GetLastWin32Error());
            }
        }
        else
        {
            Native.Shell_NotifyIcon(Native.NimModify, ref data);
        }
    }

    public void Notify(string title, string message, bool warning)
    {
        if (_disposed || !_added)
        {
            return;
        }

        var data = CreateData();
        data.uFlags = Native.NifInfo;
        data.szInfoTitle = Truncate(title, 63);
        data.szInfo = Truncate(message, 255);
        data.dwInfoFlags = warning ? Native.NiifWarning : Native.NiifInfo;
        data.uTimeoutOrVersion = 6000;

        if (Native.Shell_NotifyIcon(Native.NimModify, ref data))
        {
            Log.Info($"气泡通知已发送：{title}");
        }
        else
        {
            Log.Warn("气泡通知失败：" + Marshal.GetLastWin32Error());
        }
    }

    /// <summary>图标在屏幕上的矩形（物理像素），拿不到时返回 false。</summary>
    internal bool TryGetIconRect(out Native.Rect rect)
    {
        rect = default;

        if (_disposed || _hwnd == IntPtr.Zero)
        {
            return false;
        }

        var identifier = new Native.NotifyIconIdentifier
        {
            cbSize = Marshal.SizeOf<Native.NotifyIconIdentifier>(),
            hWnd = _hwnd,
            uID = IconId,
        };

        return Native.Shell_NotifyIconGetRect(ref identifier, out rect) == 0;
    }

    internal int GetIconSize()
    {
        var dpi = _hwnd != IntPtr.Zero ? Native.GetDpiForWindow(_hwnd) : 96u;
        if (dpi == 0)
        {
            dpi = 96;
        }

        return Math.Clamp((int)Math.Round(16 * dpi / 96.0), 16, 48);
    }

    private Native.NotifyIconData CreateData() => new()
    {
        cbSize = Marshal.SizeOf<Native.NotifyIconData>(),
        hWnd = _hwnd,
        uID = IconId,
        uCallbackMessage = Native.WmTrayIcon,
        szTip = string.Empty,
        szInfo = string.Empty,
        szInfoTitle = string.Empty,
    };

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == Native.WmTrayIcon)
        {
            var code = (int)(lParam.ToInt64() & 0xFFFF);

            switch (code)
            {
                case Native.WmMouseMove:
                case Native.NinPopupOpen:
                    Hovered?.Invoke();
                    break;
                case Native.NinPopupClose:
                    HoverClosed?.Invoke();
                    break;
                case Native.NinSelect:
                case Native.NinKeySelect:
                case Native.WmLeftButtonUp:
                    LeftClicked?.Invoke();
                    break;
                case Native.WmContextMenu:
                case Native.WmRightButtonUp:
                    RightClicked?.Invoke();
                    break;
            }

            return IntPtr.Zero;
        }

        return Native.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private static string Truncate(string? text, int max)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text.Length <= max ? text : text.Substring(0, max);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_added)
        {
            var data = CreateData();
            Native.Shell_NotifyIcon(Native.NimDelete, ref data);
            _added = false;
        }

        if (_hicon != IntPtr.Zero)
        {
            Native.DestroyIcon(_hicon);
            _hicon = IntPtr.Zero;
        }

        if (_hwnd != IntPtr.Zero)
        {
            Native.DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
    }
}
