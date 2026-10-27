using System;

namespace DeepSeekTray.Services;

/// <summary>
/// 极简 ICO 打包器（DIB 帧）。Skia 画好的像素直接写成 32bpp DIB，
/// 不依赖 System.Drawing，也不引入额外包。
/// </summary>
internal static class IconPack
{
    internal readonly struct Frame
    {
        public Frame(int size, byte[] data)
        {
            Size = size;
            Data = data;
        }

        public int Size { get; }

        public byte[] Data { get; }
    }

    public static byte[] BuildIco(Frame[] frames)
    {
        using var stream = new System.IO.MemoryStream();
        using var writer = new System.IO.BinaryWriter(stream);

        writer.Write((short)0);
        writer.Write((short)1);
        writer.Write((short)frames.Length);

        var offset = 6 + 16 * frames.Length;
        foreach (var frame in frames)
        {
            writer.Write((byte)(frame.Size >= 256 ? 0 : frame.Size));
            writer.Write((byte)(frame.Size >= 256 ? 0 : frame.Size));
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((short)1);
            writer.Write((short)32);
            writer.Write(frame.Data.Length);
            writer.Write(offset);
            offset += frame.Data.Length;
        }

        foreach (var frame in frames)
        {
            writer.Write(frame.Data);
        }

        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>
    /// 把 BGRA（自上而下、stride 连续）像素写成 ICO 的 DIB 帧：
    /// BITMAPINFOHEADER + 自下而上像素 + 全零 AND 掩码。
    /// </summary>
    public static byte[] ToDibFrame(byte[] bgra, int width, int height, int stride)
    {
        using var stream = new System.IO.MemoryStream();
        using var writer = new System.IO.BinaryWriter(stream);

        writer.Write(40);
        writer.Write(width);
        writer.Write(height * 2);
        writer.Write((short)1);
        writer.Write((short)32);
        writer.Write(0);
        writer.Write(width * height * 4);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);

        var row = new byte[width * 4];
        for (var y = height - 1; y >= 0; y--)
        {
            Buffer.BlockCopy(bgra, y * stride, row, 0, row.Length);
            writer.Write(row);
        }

        var maskStride = (width + 31) / 32 * 4;
        writer.Write(new byte[maskStride * height]);

        writer.Flush();
        return stream.ToArray();
    }
}
