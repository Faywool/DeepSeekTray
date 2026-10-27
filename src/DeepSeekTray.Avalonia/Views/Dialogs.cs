using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using DeepSeekTray.Localization;

namespace DeepSeekTray.Views;

/// <summary>Avalonia 没有内置 MessageBox / InputBox，这里补最小实现。</summary>
internal static class Dialogs
{
    private static bool IsDark => Application.Current?.ActualThemeVariant == ThemeVariant.Dark;

    private static IBrush Bg => IsDark ? new SolidColorBrush(Color.Parse("#171A21")) : Brushes.White;

    private static IBrush Fg => IsDark ? new SolidColorBrush(Color.Parse("#E8EBF1")) : new SolidColorBrush(Color.Parse("#111827"));

    private static IBrush Muted => IsDark ? new SolidColorBrush(Color.Parse("#9AA3B2")) : new SolidColorBrush(Color.Parse("#6B7280"));

    public static Task ShowMessage(Window owner, string title, string message)
        => Run(owner, title, message, null, Loc.T("Common.Ok"), null, null);

    public static async Task<bool> Confirm(Window owner, string title, string message, string ok = "确定", string cancel = "取消")
        => await Run(owner, title, message, null, ok, cancel, null) == true;

    public static async Task<string?> Prompt(Window owner, string title, string label, string initial = "", string? hint = null)
    {
        var box = new TextBox
        {
            Text = initial,
            Margin = new Thickness(0, 8, 0, 0),
            FontFamily = new FontFamily("Consolas, Microsoft YaHei UI"),
        };

        var confirmed = await Run(owner, title, label, box, "确定", "取消", hint);
        return confirmed == true ? box.Text?.Trim() : null;
    }

    private static async Task<bool?> Run(
        Window owner,
        string title,
        string message,
        Control? extra,
        string ok,
        string? cancel,
        string? hint)
    {
        var window = new Window
        {
            Title = title,
            Width = 470,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
            Background = Bg,
            FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI"),
        };

        var panel = new StackPanel { Margin = new Thickness(22, 18, 22, 18), Spacing = 4 };

        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12.5,
            LineHeight = 20,
            Foreground = Fg,
        });

        if (extra is not null)
        {
            panel.Children.Add(extra);
        }

        if (!string.IsNullOrWhiteSpace(hint))
        {
            panel.Children.Add(new TextBlock
            {
                Text = hint,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0),
                Foreground = Muted,
            });
        }

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 16, 0, 0),
        };

        if (cancel is not null)
        {
            var cancelButton = new Button { Content = cancel, Width = 88 };
            cancelButton.Classes.Add("ghost");
            cancelButton.Click += (_, _) => window.Close(false);
            buttons.Children.Add(cancelButton);
        }

        var okButton = new Button { Content = ok, Width = 96 };
        okButton.Classes.Add("primary");
        okButton.Click += (_, _) => window.Close(true);
        buttons.Children.Add(okButton);

        panel.Children.Add(buttons);
        window.Content = panel;

        return await window.ShowDialog<bool?>(owner);
    }
}
