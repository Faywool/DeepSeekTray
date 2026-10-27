using System;
using Avalonia.Media;

namespace DeepSeekTray.Services;

/// <summary>Avalonia 颜色工具：分级色 / 可读前景色。</summary>
public static class ColorHelper
{
    public static Color Parse(string? hex, Color fallback)
        => !string.IsNullOrWhiteSpace(hex) && Color.TryParse(hex.Trim(), out var color) ? color : fallback;

    public static Color Parse(string? hex) => Parse(hex, Colors.Gray);

    public static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    /// <summary>根据背景亮度选择可读的前景色。</summary>
    public static Color PickReadable(Color background)
    {
        var luminance = background.R * 0.299 + background.G * 0.587 + background.B * 0.114;
        return luminance > 165 ? Color.FromRgb(0x11, 0x18, 0x27) : Colors.White;
    }

    public static IBrush Brush(string? hex) => new SolidColorBrush(Parse(hex));
}
