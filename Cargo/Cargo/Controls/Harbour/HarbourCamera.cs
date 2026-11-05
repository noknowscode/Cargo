namespace Cargo.Controls.Harbour;

/// <summary>
/// Orbit camera around a target on the water line, with a real perspective divide. The
/// foreshortening is what makes the scene read as a place rather than an isometric diorama.
/// </summary>
public sealed class HarbourCamera
{
    /// <summary>Camera distance in world units: a moderate lens.</summary>
    public const double Distance = 1000;

    public const double MinPitch = 10 * Math.PI / 180;
    public const double MaxPitch = 88 * Math.PI / 180;
    public const double MinZoom = 0.8;
    public const double MaxZoom = 9;

    public double Yaw { get; set; }
    public double Pitch { get; set; }
    public double Zoom { get; set; } = 1;
    public double TargetX { get; set; }
    public double TargetY { get; set; }

    // Derived per frame by Update().
    private double _cy, _sy, _cp, _sp, _scale, _ox, _oy;

    public V3 Eye { get; private set; }

    /// <summary>Pixels per world unit at the target.</summary>
    public double Scale => _scale;

    public double ViewWidth { get; private set; }

    public double ViewHeight { get; private set; }

    /// <summary>
    /// Height at the top of the view that holds an overlay (the Overview greeting). The scene is
    /// framed in the space below it, so the overlay never sits on the harbour.
    /// </summary>
    public double TopInset { get; set; }

    public void Update(double width, double height)
    {
        ViewWidth = width;
        ViewHeight = height;
        var top = Math.Clamp(TopInset, 0, height * .5);
        height -= top;
        _cy = Math.Cos(Yaw);
        _sy = Math.Sin(Yaw);
        _cp = Math.Cos(Pitch);
        _sp = Math.Sin(Pitch);
        _scale = Math.Min(width / 1150, height / 600) * Zoom;
        _ox = width / 2;
        _oy = top + height / 2 + height * 0.06;
        Eye = new V3(TargetX + Distance * _cp * _sy, TargetY + Distance * _cp * _cy, Distance * _sp);
    }

    public (float X, float Y) Project(double x, double y, double z)
    {
        var dx = x - TargetX;
        var dy = y - TargetY;
        var x1 = dx * _cy - dy * _sy;
        var y1 = dx * _sy + dy * _cy;
        var k = Distance / Math.Max(80, Distance - (y1 * _cp + z * _sp));
        return ((float)(_ox + x1 * _scale * k), (float)(_oy + (y1 * _sp - z * _cp) * _scale * k));
    }

    public (float X, float Y) Project(V3 p) => Project(p.X, p.Y, p.Z);

    /// <summary>Painter's key: larger is closer to the eye.</summary>
    public double Depth(double x, double y, double z)
    {
        var a = x - Eye.X;
        var b = y - Eye.Y;
        var c = z - Eye.Z;
        return -Math.Sqrt(a * a + b * b + c * c);
    }

    public double Depth(V3 p) => Depth(p.X, p.Y, p.Z);

    /// <summary>Back-face test against the eye position.</summary>
    public bool Faces(V3 normal, V3 point) =>
        normal.X * (Eye.X - point.X) + normal.Y * (Eye.Y - point.Y) + normal.Z * (Eye.Z - point.Z) > 0;

    /// <summary>Which way north lies on screen, for the HUD's needle: degrees clockwise from straight up.</summary>
    public double NorthAngle()
    {
        var a = Project(TargetX, TargetY, 0);
        var b = Project(TargetX, TargetY - 100, 0);
        return Math.Atan2(b.Y - a.Y, b.X - a.X) * 180 / Math.PI + 90;
    }

    /// <summary>The HUD's scale bar: the longest round distance no wider than <paramref name="maxWidth"/> px, and its width.</summary>
    public (int Metres, double Width) ScaleBar(double maxWidth = 130)
    {
        var pxPerMetre = Scale / HarbourWorld.MetresPerUnit;
        var metres = new[] { 25, 50, 100, 200, 250, 500, 1000 }.LastOrDefault(m => m * pxPerMetre <= maxWidth, 25);
        return (metres, Math.Max(8, Math.Round(metres * pxPerMetre)));
    }

    /// <summary>How squarely the camera looks toward a horizontal direction (for the sun glint).</summary>
    public double Facing(double dirX, double dirY) => -(_sy * dirX + _cy * dirY);

    public HarbourCameraPose Pose => new(Yaw, Pitch, Zoom, TargetX, TargetY);

    public void Apply(HarbourCameraPose pose)
    {
        Yaw = pose.Yaw;
        Pitch = pose.Pitch;
        Zoom = pose.Zoom;
        TargetX = pose.TargetX;
        TargetY = pose.TargetY;
    }
}

public readonly record struct HarbourCameraPose(double Yaw, double Pitch, double Zoom, double TargetX, double TargetY)
{
    private static double Rad(double d) => d * Math.PI / 180;

    public static HarbourCameraPose Overview => new(Rad(-20), Rad(40), 2.15, 170, 40);

    public static HarbourCameraPose FromSea(HarbourCameraPose pose) => pose with { Yaw = 0, Pitch = Rad(18) };

    public static HarbourCameraPose FromLand(HarbourCameraPose pose) => pose with { Yaw = Math.PI, Pitch = Rad(26) };

    public static HarbourCameraPose Plan(HarbourCameraPose pose) => pose with { Yaw = 0, Pitch = Rad(86) };

    /// <summary>The camera menu's preset by name ("sea", "land", "plan"), turned from <paramref name="current"/>; any other name is the Overview.</summary>
    public static HarbourCameraPose Preset(string? name, HarbourCameraPose current) => name switch
    {
        "sea" => FromSea(current),
        "land" => FromLand(current),
        "plan" => Plan(current),
        _ => Overview
    };

    /// <summary>
    /// A section's strip frames what the section is about: the terminal perimeter on Security, the yard on Cargo, the
    /// vessel worked at <paramref name="vessel"/> elsewhere, or the harbour pitched down when there is none.
    /// </summary>
    public static HarbourCameraPose ForStrip(string section, (double X, double Y)? vessel) => section switch
    {
        "security" => new(Rad(-8), Rad(36), 2.4, 40, -40),
        "cargo" => new(Rad(-6), Rad(42), 3.0, 0, -66),
        _ when vessel is { } at => new(Rad(-14), Rad(26), 4.6, at.X, at.Y + 2),
        _ => Overview with { Pitch = Rad(26) }
    };

    /// <summary>Eases from this pose to <paramref name="to"/>; yaw takes the short way round.</summary>
    public HarbourCameraPose Lerp(HarbourCameraPose to, double t)
    {
        var dYaw = to.Yaw - Yaw;
        dYaw = ((dYaw + Math.PI) % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI) - Math.PI;
        return new(Yaw + dYaw * t, Pitch + (to.Pitch - Pitch) * t, Zoom + (to.Zoom - Zoom) * t,
            TargetX + (to.TargetX - TargetX) * t, TargetY + (to.TargetY - TargetY) * t);
    }
}
