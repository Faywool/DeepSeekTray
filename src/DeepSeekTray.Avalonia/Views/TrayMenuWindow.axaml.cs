using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using DeepSeekTray.Services;

namespace DeepSeekTray.Views;

public sealed record TrayMenuItem(string Glyph, string Text, Action Invoke, bool Danger = false);

public partial class TrayMenuWindow : Window
{
    private bool _closed;

    public TrayMenuWindow(ThemeVariant variant, string title, string subtitle, IReadOnlyList<TrayMenuItem> items)
    {
        RequestedThemeVariant = variant;
        InitializeComponent();

        TitleText.Text = title;
        SubtitleText.Text = subtitle;

        for (var i = 0; i < items.Count; i++)
        {
            // 危险项（退出）前面加一条分隔线，和普通操作分开
            if (i > 0 && items[i].Danger)
            {
                ItemsHost.Children.Add(new Border
                {
                    Height = 1,
                    Margin = new Thickness(6, 5),
                    Background = Res("Line"),
                });
            }

            ItemsHost.Children.Add(BuildItem(items[i]));
        }

        Deactivated += (_, _) => SafeClose();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                SafeClose();
            }
        };
    }

    private static IBrush Res(string key)
        => Application.Current is { } app
           && app.TryGetResource(key, app.ActualThemeVariant, out var value)
           && value is IBrush brush
            ? brush
            : Brushes.Gray;

    private Control BuildItem(TrayMenuItem item)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("24,*"),
        };

        var glyph = new TextBlock
        {
            Text = item.Glyph,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var label = new TextBlock
        {
            Text = item.Text,
            FontSize = 12.5,
            VerticalAlignment = VerticalAlignment.Center,
        };

        Grid.SetColumn(glyph, 0);
        Grid.SetColumn(label, 1);
        grid.Children.Add(glyph);
        grid.Children.Add(label);

        var button = new Button
        {
            Content = grid,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };

        button.Classes.Add("menu");
        if (item.Danger)
        {
            button.Classes.Add("danger");
        }

        button.Click += (_, _) =>
        {
            SafeClose();
            item.Invoke();
        };

        return button;
    }

    /// <summary>关闭要在鼠标位置弹出，且不能压到屏幕边上。</summary>
    public void ShowAtCursor()
    {
        Show();
        UpdateLayout();

        if (!Native.GetCursorPos(out var cursor))
        {
            return;
        }

        var scaling = RenderScaling <= 0 ? 1.0 : RenderScaling;
        var width = (int)Math.Ceiling(Bounds.Width * scaling);
        var height = (int)Math.Ceiling(Bounds.Height * scaling);

        var screen = Screens.ScreenFromPoint(new PixelPoint(cursor.X, cursor.Y)) ?? Screens.Primary;
        var area = screen?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);

        var x = cursor.X;
        var y = cursor.Y;

        if (x + width > area.Right)
        {
            x = cursor.X - width;
        }

        if (y + height > area.Bottom)
        {
            y = cursor.Y - height;
        }

        x = Math.Clamp(x, area.X, Math.Max(area.X, area.Right - width));
        y = Math.Clamp(y, area.Y, Math.Max(area.Y, area.Bottom - height));

        Position = new PixelPoint(x, y);
        Activate();
        Native.SetForegroundWindow(this.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero);
    }

    private void SafeClose()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;

        try
        {
            Close();
        }
        catch (InvalidOperationException)
        {
            // 已在关闭流程里
        }
    }
}
