using System;
using System.Collections.Generic;
using System.IO;
using SkiaSharp;

namespace DeepSeekTray.Services;

/// <summary>
/// 托盘图标：圆角方形底 + 大号加粗「¥」，按 DPI 生成多尺寸 ICO，
/// 再用 Shell_NotifyIcon 交给系统。
/// </summary>
public static class IconFactory
{
    private static readonly int[] Sizes = { 16, 20, 24, 32, 40, 48 };

    private static readonly Dictionary<string, string> PathCache = new(StringComparer.OrdinalIgnoreCase);

    public static string GetIcoPath(Avalonia.Media.Color color)
    {
        var key = $"{color.R:X2}{color.G:X2}{color.B:X2}";
        lock (PathCache)
        {
            if (PathCache.TryGetValue(key, out var cached) && File.Exists(cached))
            {
                return cached;
            }

            var folder = Path.Combine(Path.GetTempPath(), "DeepSeekTray");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"tray-{key}.ico");
            File.WriteAllBytes(path, BuildIco(color));
            PathCache[key] = path;
            return path;
        }
    }

    private static byte[] BuildIco(Avalonia.Media.Color color)
    {
        var frames = new List<IconPack.Frame>();

        foreach (var size in Sizes)
        {
            frames.Add(new IconPack.Frame(size, RenderFrame(color, size)));
        }

        return IconPack.BuildIco(frames.ToArray());
    }

    private static byte[] RenderFrame(Avalonia.Media.Color color, int size)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        var inset = Math.Max(0.5f, size * 0.03f);
        var radius = size * 0.26f;

        using (var background = new SKPaint
        {
            Color = new SKColor(color.R, color.G, color.B, 255),
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
        })
        {
            canvas.DrawRoundRect(
                new SKRect(inset, inset, size - inset, size - inset),
                radius,
                radius,
                background);
        }

        using var white = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
        };

        if (size <= 16)
        {
            DrawSmallYen(canvas, size);
        }
        else
        {
            using var typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
                                 ?? SKTypeface.Default;
            using var font = new SKFont(typeface, size * 0.78f);
            var metrics = font.Metrics;
            var baseline = size / 2f - (metrics.Ascent + metrics.Descent) / 2f;

            canvas.DrawText("¥", size / 2f, baseline, SKTextAlign.Center, font, white);
        }

        var pixels = bitmap.GetPixelSpan().ToArray();
        return IconPack.ToDibFrame(pixels, size, size, bitmap.RowBytes);
    }

    /// <summary>16px 太小，字体轮廓会糊，改用按像素对齐的几何笔画。</summary>
    private static void DrawSmallYen(SKCanvas canvas, float size)
    {
        var s = size / 16f;

        using var pen = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.7f * s,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
        };

        using var path = new SKPath();
        path.MoveTo(4.5f * s, 3.6f * s);
        path.LineTo(8f * s, 8.7f * s);
        path.LineTo(11.5f * s, 3.6f * s);
        canvas.DrawPath(path, pen);

        canvas.DrawLine(8f * s, 8.7f * s, 8f * s, 13.3f * s, pen);
        canvas.DrawLine(4.7f * s, 9.9f * s, 11.3f * s, 9.9f * s, pen);
        canvas.DrawLine(4.7f * s, 11.5f * s, 11.3f * s, 11.5f * s, pen);
    }
}
