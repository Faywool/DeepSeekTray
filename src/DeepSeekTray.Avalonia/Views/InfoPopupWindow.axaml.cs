using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using DeepSeekTray.Localization;
using DeepSeekTray.Models;
using DeepSeekTray.Services;

namespace DeepSeekTray.Views;

/// <summary>悬停在托盘图标上时显示的浮层（无激活、置顶、跟随主题）。</summary>
public partial class InfoPopupWindow : Window
{
    /// <summary>英文与数字统一走等宽字体，列宽稳定、观感更“工具”。</summary>
    private static readonly FontFamily Mono = new("Consolas, Cascadia Mono, Microsoft YaHei UI");

    public InfoPopupWindow(ThemeVariant variant)
    {
        RequestedThemeVariant = variant;
        InitializeComponent();
    }

    private static IBrush Res(string key)
    {
        if (Application.Current is { } app
            && app.TryGetResource(key, app.ActualThemeVariant, out var value)
            && value is IBrush brush)
        {
            return brush;
        }

        return Brushes.Gray;
    }

    public void Render(
        IReadOnlyList<PopupRow> rows,
        string meta,
        string? totalLabel = null,
        string? totalValue = null)
    {
        HeaderMeta.Text = meta;
        RowsHost.Children.Clear();
        FooterHost.Children.Clear();

        // 多账号之间加一条发丝线，扫读时更容易分清块
        RowsHost.Spacing = rows.Count > 1 ? 12 : 16;

        for (var i = 0; i < rows.Count; i++)
        {
            if (i > 0)
            {
                RowsHost.Children.Add(new Border
                {
                    Height = 1,
                    Background = Res("Line"),
                });
            }

            RowsHost.Children.Add(BuildRow(rows[i], detailed: rows.Count == 1));
        }

        if (totalLabel is null || totalValue is null)
        {
            return;
        }

        // 多账号才有合计：单账号的“今日”已经在行内，不重复
        FooterHost.Children.Add(new Border
        {
            Height = 1,
            Margin = new Thickness(0, 0, 0, 9),
            Background = Res("Line"),
        });

        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        footer.Children.Add(new TextBlock
        {
            Text = totalLabel,
            FontSize = 11.5,
            Foreground = Res("TextFaint"),
            VerticalAlignment = VerticalAlignment.Center,
        });

        var value = new TextBlock
        {
            Text = totalValue,
            FontFamily = Mono,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Foreground = Res("TextMain"),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Grid.SetColumn(value, 1);
        footer.Children.Add(value);

        FooterHost.Children.Add(footer);
    }

    private Control BuildRow(PopupRow row, bool detailed)
    {
        var accent = ColorHelper.Brush(row.AccentHex);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
        };

        // ── 第一行：色点 + 账号名 [+ 默认] / 余额 ──
        var nameRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        nameRow.Children.Add(new Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = accent,
            VerticalAlignment = VerticalAlignment.Center,
        });
        nameRow.Children.Add(new TextBlock
        {
            Text = row.Name,
            FontSize = 13.5,
            FontWeight = FontWeight.SemiBold,
            Foreground = Res("TextMain"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 140,
        });

        if (row.IsActive)
        {
            nameRow.Children.Add(new Border
            {
                Classes = { "chip" },
                Padding = new Thickness(5, 1),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = Loc.T("Popup.Default"),
                    FontSize = 9.5,
                    Foreground = Res("Accent"),
                },
            });
        }

        var amount = new TextBlock
        {
            Text = row.Amount,
            FontFamily = Mono,
            FontSize = 21,
            FontWeight = FontWeight.Bold,
            Foreground = row.IsError ? Res("TextMuted") : accent,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };

        grid.Children.Add(nameRow);
        Grid.SetColumn(amount, 1);
        grid.Children.Add(amount);

        // ── 第二行：今日消耗（标签左 / 数值右，和余额右对齐成一列） ──
        if (!string.IsNullOrEmpty(row.Today))
        {
            var label = new TextBlock
            {
                Text = Loc.T(detailed ? "Popup.Today" : "Popup.TodayShort"),
                FontSize = 11.5,
                Foreground = Res("TextFaint"),
                Margin = new Thickness(16, 6, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };

            var value = new TextBlock
            {
                Text = row.Today,
                FontFamily = Mono,
                FontSize = 14,
                FontWeight = FontWeight.SemiBold,
                Foreground = Res("TextMain"),
                Margin = new Thickness(8, 6, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            };

            Grid.SetRow(label, 1);
            Grid.SetColumn(label, 0);
            Grid.SetRow(value, 1);
            Grid.SetColumn(value, 1);
            grid.Children.Add(label);
            grid.Children.Add(value);
        }

        // ── 第三行：余额构成（赠送为 0 时整行不显示） ──
        if (detailed && !string.IsNullOrEmpty(row.Detail))
        {
            var detail = new TextBlock
            {
                Text = row.Detail,
                FontSize = 11,
                Margin = new Thickness(16, 6, 0, 0),
                Foreground = Res(row.IsError ? "Danger" : "TextFaint"),
                TextTrimming = TextTrimming.CharacterEllipsis,
            };

            Grid.SetRow(detail, 2);
            Grid.SetColumn(detail, 0);
            Grid.SetColumnSpan(detail, 2);
            grid.Children.Add(detail);
        }

        return grid;
    }

    /// <summary>贴在托盘图标上方（放不下就放下方），并夹在工作区内。</summary>
    internal void ShowNearIcon(Native.Rect icon)
    {
        Show();
        UpdateLayout();
        PlaceNearIcon(icon);
    }

    internal void PlaceNearIcon(Native.Rect icon)
    {
        var scaling = RenderScaling <= 0 ? 1.0 : RenderScaling;
        var width = (int)Math.Ceiling(Bounds.Width * scaling);
        var height = (int)Math.Ceiling(Bounds.Height * scaling);

        var screen = Screens.ScreenFromPoint(new PixelPoint(icon.Left, icon.Top)) ?? Screens.Primary;
        var area = screen?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);

        var iconWidth = icon.Right - icon.Left;
        var x = icon.Left + (iconWidth - width) / 2;
        var y = icon.Top - height - 6;

        if (y < area.Y)
        {
            y = icon.Bottom + 6;
        }

        x = Math.Clamp(x, area.X, Math.Max(area.X, area.Right - width));
        y = Math.Clamp(y, area.Y, Math.Max(area.Y, area.Bottom - height));

        Position = new PixelPoint(x, y);
    }

    public bool ContainsScreenPoint(int x, int y)
    {
        if (!IsVisible)
        {
            return false;
        }

        var scaling = RenderScaling <= 0 ? 1.0 : RenderScaling;
        var width = (int)Math.Ceiling(Bounds.Width * scaling);
        var height = (int)Math.Ceiling(Bounds.Height * scaling);

        return x >= Position.X && x <= Position.X + width
               && y >= Position.Y && y <= Position.Y + height;
    }
}
