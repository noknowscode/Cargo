namespace UnoOrbit.Rendering;

/// <summary>
/// Nebula clouds - soft colored fog patches in the background.
/// Mix of cool (blue, purple, teal) and warm (pink, magenta, rose) tones
/// for a richer interstellar feel.
/// </summary>
public sealed class NebulaField
{
    public struct Nebula
    {
        public float X;        // 0-1 normalized
        public float Y;        // 0-1 normalized
        public float Radius;   // 0.05 - 0.30 of screen
        public float R, G, B;  // color channels 0-1
        public float Alpha;    // 0.02 - 0.08
        public float DriftX;   // slow drift speed
        public float DriftY;
    }

    public readonly Nebula[] Clouds;

    public NebulaField(int count = 10)
    {
        var rng = new Random(77);
        Clouds = new Nebula[count];

        // Expanded palette: cool blues/purples + warm pinks/magentas
        float[][] palette =
        [
            [0.15f, 0.10f, 0.35f], // deep purple
            [0.08f, 0.15f, 0.30f], // dark blue
            [0.10f, 0.22f, 0.25f], // teal
            [0.20f, 0.10f, 0.25f], // violet
            [0.05f, 0.12f, 0.28f], // navy
            [0.12f, 0.08f, 0.22f], // indigo
            [0.28f, 0.08f, 0.20f], // magenta / rose
            [0.22f, 0.06f, 0.28f], // purple-pink
            [0.10f, 0.18f, 0.32f], // steel blue
            [0.30f, 0.10f, 0.15f], // warm crimson
        ];

        for (int i = 0; i < count; i++)
        {
            var c = palette[i % palette.Length];
            Clouds[i] = new Nebula
            {
                X = 0.05f + (float)rng.NextDouble() * 0.9f,
                Y = 0.05f + (float)rng.NextDouble() * 0.9f,
                Radius = 0.08f + (float)rng.NextDouble() * 0.22f,
                R = c[0],
                G = c[1],
                B = c[2],
                Alpha = 0.03f + (float)rng.NextDouble() * 0.05f,
                DriftX = ((float)rng.NextDouble() - 0.5f) * 0.003f,
                DriftY = ((float)rng.NextDouble() - 0.5f) * 0.002f,
            };
        }
    }
}

/// <summary>
/// Floating dust/debris particles that drift slowly through space.
/// Some particles have subtle color tints for depth.
/// </summary>
public sealed class ParticleField
{
    public struct Particle
    {
        public float X;        // 0-1 normalized
        public float Y;        // 0-1 normalized
        public float Size;     // 0.3 - 1.4 dp
        public float SpeedX;   // drift speed
        public float SpeedY;
        public float Phase;    // for brightness oscillation
        public float Period;   // oscillation period
        public byte R, G, B;   // particle color tint
    }

    public readonly Particle[] Particles;

    public ParticleField(int count = 80)
    {
        var rng = new Random(123);
        Particles = new Particle[count];

        for (int i = 0; i < count; i++)
        {
            // Most particles warm-white, some with subtle blue or pink tints
            byte r, g, b;
            float roll = (float)rng.NextDouble();
            if (roll < 0.6f)
            {
                // Warm white
                r = (byte)(215 + rng.Next(40));
                g = (byte)(210 + rng.Next(35));
                b = (byte)(230 + rng.Next(25));
            }
            else if (roll < 0.8f)
            {
                // Cool blue tint
                r = (byte)(180 + rng.Next(30));
                g = (byte)(200 + rng.Next(30));
                b = 255;
            }
            else
            {
                // Faint pink/warm tint
                r = 255;
                g = (byte)(190 + rng.Next(30));
                b = (byte)(210 + rng.Next(30));
            }

            Particles[i] = new Particle
            {
                X = (float)rng.NextDouble(),
                Y = (float)rng.NextDouble(),
                Size = 0.3f + (float)rng.NextDouble() * 1.1f,
                SpeedX = ((float)rng.NextDouble() - 0.5f) * 0.008f,
                SpeedY = ((float)rng.NextDouble() - 0.5f) * 0.006f,
                Phase = (float)(rng.NextDouble() * MathF.PI * 2f),
                Period = 3f + (float)rng.NextDouble() * 5f,
                R = r,
                G = g,
                B = b,
            };
        }
    }
}
