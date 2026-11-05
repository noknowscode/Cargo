namespace UnoOrbit.Rendering;

/// <summary>
/// Starfield data generated once, rendered per-frame on the Skia canvas.
/// Each star twinkles using a sin() oscillation instead of XAML animations.
/// Stars have realistic color tints: blue-white (hot), white, warm (cooler).
/// </summary>
public sealed class StarField
{
    public struct Star
    {
        public float X;       // 0-1 normalized
        public float Y;       // 0-1 normalized
        public float Size;    // 0.4-3.5 dp
        public float Period;  // twinkle period in seconds (2-8)
        public float Phase;   // random phase offset (0-2PI)
        public byte R, G, B;  // star color tint
        public float Brightness; // base brightness multiplier (0.6-1.0)
    }

    public readonly Star[] Stars;

    public StarField(int count = 350)
    {
        var rng = new Random(42); // Fixed seed for consistent starfield
        Stars = new Star[count];

        for (int i = 0; i < count; i++)
        {
            // Star color distribution: realistic spectral classes
            float roll = (float)rng.NextDouble();
            byte r, g, b;
            float brightness;

            if (roll < 0.12f)
            {
                // Blue-white hot stars (O/B class) - vivid and bright
                r = (byte)(180 + rng.Next(40));
                g = (byte)(200 + rng.Next(40));
                b = 255;
                brightness = 0.85f + (float)rng.NextDouble() * 0.15f;
            }
            else if (roll < 0.35f)
            {
                // Cool blue-white (A class)
                r = (byte)(220 + rng.Next(35));
                g = (byte)(230 + rng.Next(25));
                b = 255;
                brightness = 0.75f + (float)rng.NextDouble() * 0.2f;
            }
            else if (roll < 0.65f)
            {
                // Pure white / yellow-white (F/G class)
                r = 255;
                g = (byte)(245 + rng.Next(10));
                b = (byte)(230 + rng.Next(25));
                brightness = 0.7f + (float)rng.NextDouble() * 0.2f;
            }
            else if (roll < 0.85f)
            {
                // Warm yellow (K class)
                r = 255;
                g = (byte)(210 + rng.Next(35));
                b = (byte)(170 + rng.Next(40));
                brightness = 0.65f + (float)rng.NextDouble() * 0.2f;
            }
            else
            {
                // Cool orange/red (M class)
                r = 255;
                g = (byte)(175 + rng.Next(40));
                b = (byte)(140 + rng.Next(40));
                brightness = 0.6f + (float)rng.NextDouble() * 0.2f;
            }

            // Most stars are small; a few "bright" key stars stand out
            float size;
            if (i < 15)
            {
                // Bright prominent stars
                size = 1.8f + (float)rng.NextDouble() * 1.7f;
                brightness = MathF.Min(1f, brightness + 0.15f);
            }
            else if (i < 60)
            {
                // Medium stars
                size = 0.8f + (float)rng.NextDouble() * 1.4f;
            }
            else
            {
                // Faint background stars
                size = 0.4f + (float)rng.NextDouble() * 1.2f;
            }

            Stars[i] = new Star
            {
                X = (float)rng.NextDouble(),
                Y = (float)rng.NextDouble(),
                Size = size,
                Period = 2f + (float)rng.NextDouble() * 6f,
                Phase = (float)(rng.NextDouble() * MathF.PI * 2f),
                R = r,
                G = g,
                B = b,
                Brightness = brightness,
            };
        }
    }
}
