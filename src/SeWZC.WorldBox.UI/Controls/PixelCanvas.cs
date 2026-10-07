using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace SeWZC.WorldBox.UI.Controls;

/// <summary>缓存地形像素图案的内存 RGBA 画布。</summary>
/// <param name="width">画布宽度，以像素计。</param>
/// <param name="height">画布高度，以像素计。</param>
internal sealed class PixelCanvas(int width, int height)
{
    /// <summary>画布宽度，以像素计。</summary>
    public int Width { get; } = width;

    /// <summary>画布高度，以像素计。</summary>
    public int Height { get; } = height;

    /// <summary>按行存储的 RGBA 像素字节，每个像素占四个字节。</summary>
    public byte[] Pixels { get; } = new byte[checked(width * height * 4)];

    /// <summary>写入单个 RGBA 像素，忽略画布外的坐标。</summary>
    /// <param name="x">像素横坐标。</param>
    /// <param name="y">像素纵坐标。</param>
    /// <param name="rgba">按 R、G、B、A 从高位到低位编码的颜色。</param>
    public void Pixel(int x, int y, uint rgba)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            return;
        var i = (y * Width + x) * 4;
        Pixels[i] = (byte)(rgba >> 24);
        Pixels[i + 1] = (byte)(rgba >> 16);
        Pixels[i + 2] = (byte)(rgba >> 8);
        Pixels[i + 3] = (byte)rgba;
    }

    /// <summary>以 RGBA 颜色填充矩形，并裁剪到画布边界。</summary>
    /// <param name="x">矩形左上角的像素横坐标。</param>
    /// <param name="y">矩形左上角的像素纵坐标。</param>
    /// <param name="width">矩形宽度，以像素计。</param>
    /// <param name="height">矩形高度，以像素计。</param>
    /// <param name="rgba">按 R、G、B、A 从高位到低位编码的颜色。</param>
    public void Rect(int x, int y, int width, int height, uint rgba)
    {
        var left = Math.Max(0, x);
        var top = Math.Max(0, y);
        var right = Math.Min(Width, x + width);
        var bottom = Math.Min(Height, y + height);
        if (left >= right || top >= bottom)
            return;
        var color = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(rgba) : rgba;
        var pixels = MemoryMarshal.Cast<byte, uint>(Pixels.AsSpan());
        for (var py = top; py < bottom; py++)
            pixels.Slice(py * Width + left, right - left).Fill(color);
    }

    /// <summary>调整 RGB 分量的亮度并保留透明度。</summary>
    /// <param name="rgba">按 R、G、B、A 从高位到低位编码的原始颜色。</param>
    /// <param name="offset">对 RGB 分量增加的亮度，负值表示变暗。</param>
    public static uint Shade(uint rgba, int offset)
    {
        static uint Shift(uint component, int amount)
        {
            return (uint)Math.Clamp((int)component + amount, 0, 255);
        }

        return (Shift(rgba >> 24, offset) << 24) |
               (Shift((rgba >> 16) & 255, offset) << 16) |
               (Shift((rgba >> 8) & 255, offset) << 8) |
               (rgba & 255);
    }

    /// <summary>绘制指定厚度的像素线，画布外的部分被裁剪。</summary>
    /// <param name="x">线段起点的像素横坐标。</param>
    /// <param name="y">线段起点的像素纵坐标。</param>
    /// <param name="endX">线段终点的像素横坐标。</param>
    /// <param name="endY">线段终点的像素纵坐标。</param>
    /// <param name="color">按 R、G、B、A 从高位到低位编码的颜色。</param>
    /// <param name="thickness">线段厚度，以像素计。</param>
    public void Line(int x, int y, int endX, int endY, uint color, int thickness = 1)
    {
        var dx = Math.Abs(endX - x);
        var dy = -Math.Abs(endY - y);
        var sx = x < endX ? 1 : -1;
        var sy = y < endY ? 1 : -1;
        var error = dx + dy;
        while (true)
        {
            Rect(x, y, thickness, thickness, color);
            if (x == endX && y == endY)
                return;
            var twice = error * 2;
            if (twice >= dy)
            {
                error += dy;
                x += sx;
            }

            if (twice <= dx)
            {
                error += dx;
                y += sy;
            }
        }
    }

    /// <summary>根据坐标和扰动种子计算稳定的无符号噪声值。</summary>
    /// <param name="x">参与噪声计算的横向像素坐标。</param>
    /// <param name="y">参与噪声计算的纵向像素坐标。</param>
    /// <param name="salt">改变噪声分布的整数扰动种子。</param>
    public static uint Noise(int x, int y, int salt = 0)
    {
        unchecked
        {
            var h = (uint)(x * 374761393 + y * 668265263 + salt * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177;
            return h ^ (h >> 16);
        }
    }
}
