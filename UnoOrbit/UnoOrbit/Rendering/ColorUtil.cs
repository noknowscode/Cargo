using SkiaSharp;

namespace UnoOrbit.Rendering;

public static class ColorUtil
{
    public static SKColor LightenColor(SKColor color, int amount)
    {
        return new SKColor(
            (byte)Math.Min(255, color.Red + amount),
            (byte)Math.Min(255, color.Green + amount),
            (byte)Math.Min(255, color.Blue + amount),
            color.Alpha
        );
    }

    public static SKColor FromHex(string hex)
    {
        return SKColor.Parse(hex);
    }

    public static SKColor WithAlpha(SKColor color, byte alpha)
    {
        return color.WithAlpha(alpha);
    }
}
