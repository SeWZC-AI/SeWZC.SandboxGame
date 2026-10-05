using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace SeWZC.WorldBox.UI.Controls;

/// <summary>A small managed RGBA canvas used to cache original pixel terrain.</summary>
internal sealed class PixelCanvas(int width, int height)
{
    public int Width { get; } = width;
    public int Height { get; } = height;
    public byte[] Pixels { get; } = new byte[checked(width * height * 4)];

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

    public void Rect(int x, int y, int width, int height, uint rgba)
    {
        var left = Math.Max(0, x);
        var top = Math.Max(0, y);
        var right = Math.Min(Width, x + width);
        var bottom = Math.Min(Height, y + height);
        if (left >= right || top >= bottom) return;
        var color = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(rgba) : rgba;
        var pixels = MemoryMarshal.Cast<byte, uint>(Pixels.AsSpan());
        for (var py = top; py < bottom; py++)
            pixels.Slice(py * Width + left, right - left).Fill(color);
    }

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
            if (x == endX && y == endY) return;
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
