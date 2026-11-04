namespace Cargo.Domain;

public enum CargoClass
{
    Standard,
    Reefer,
    Hazard,
    Oversize,
    Empty
}

public static class CargoClassInfo
{
    public static string Token(this CargoClass value) => value switch
    {
        CargoClass.Standard => "CargoStandardColor",
        CargoClass.Reefer => "CargoReeferColor",
        CargoClass.Hazard => "CargoHazardColor",
        CargoClass.Oversize => "CargoOversizeColor",
        _ => "CargoEmptyColor"
    };

    public static string Label(this CargoClass value) => value switch
    {
        CargoClass.Standard => "Standard",
        CargoClass.Reefer => "Refrigerated",
        CargoClass.Hazard => "Hazardous",
        CargoClass.Oversize => "Oversized",
        _ => "Empty"
    };
}

public sealed record CargoMix(int Standard, int Reefer, int Hazard, int Oversize, int Empty)
{
    public int this[CargoClass c] => c switch
    {
        CargoClass.Standard => Standard,
        CargoClass.Reefer => Reefer,
        CargoClass.Hazard => Hazard,
        CargoClass.Oversize => Oversize,
        _ => Empty
    };

    public IEnumerable<(CargoClass Class, int Percent)> Parts()
    {
        yield return (CargoClass.Standard, Standard);
        yield return (CargoClass.Reefer, Reefer);
        yield return (CargoClass.Hazard, Hazard);
        yield return (CargoClass.Oversize, Oversize);
        yield return (CargoClass.Empty, Empty);
    }

    /// <summary>Picks a class for cell <paramref name="i"/> so a deck reads like a real stow plan.</summary>
    public CargoClass ClassAt(int i)
    {
        var r = Noise.H(i) * 100;
        var acc = 0d;
        foreach (var (cls, pct) in Parts())
        {
            acc += pct;
            if (r < acc)
            {
                return cls;
            }
        }

        return CargoClass.Empty;
    }
}

/// <summary>Where a vessel is in her call. Alongside (docked or departing) she keeps her berth; the rest can be moved.</summary>
public enum VesselStatus
{
    Docked,
    Arriving,
    Departing,
    AtAnchor
}

/// <summary>What a berth is doing when the shift starts.</summary>
public enum BerthState
{
    Available,
    Occupied,
    Reserved,
    Restricted
}

public static class StatusLabels
{
    /// <summary>"At anchor": the status as the screens write it.</summary>
    public static string Label(this VesselStatus status) => status switch
    {
        VesselStatus.Docked => "Docked",
        VesselStatus.Arriving => "Arriving",
        VesselStatus.Departing => "Departing",
        _ => "At anchor"
    };

    /// <summary>"Restricted": the berth state as the map writes it.</summary>
    public static string Label(this BerthState state) => state switch
    {
        BerthState.Occupied => "Occupied",
        BerthState.Reserved => "Reserved",
        BerthState.Restricted => "Restricted",
        _ => "Available"
    };
}

public sealed record ClearanceCheck(string Mark, string Label, string Time, string Tone);

public sealed partial record Vessel(
    string Id,
    string Name,
    string Operator,
    string Imo,
    VesselStatus Status,
    string StatusLine,
    string AccentToken,
    string? HomeBerth,
    string Eta,
    string Etd,
    int Length,
    double Draft,
    int Containers,
    string Security,
    string Voyage,
    int UnloadPercent,
    int LoadPercent,
    CargoMix Mix,
    int Bays,
    string OpsNote,
    IReadOnlyList<ClearanceCheck> Checks);

public sealed record BerthDef(string Number, BerthState State, string? Occupant, double Depth);

public sealed record TimelineSegment(string Label, double Start, double End, string Kind);

public sealed partial record ContainerDef(
    string Id,
    string Size,
    string Carrier,
    string Route,
    string State,
    int Stage,
    string TintToken,
    string Weight,
    string Dimensions,
    string Vessel,
    string Origin,
    string Destination,
    string YardSlot,
    string Customs,
    string Inspection,
    string Seal,
    string Security,
    string SecurityToken,
    bool Warn = false);

public sealed partial record ContainerDef
{
    /// <summary>The ISO 6346 number as it is painted on the box: owner, serial, check digit.</summary>
    public string DisplayId => $"{Id[..4]} {Id[4..10]} {Id[10..]}";
}

public sealed record CargoItem(string Name, string TintToken, bool Flagged = false);

/// <summary>The operator on shift. Times are hours on the app's clock; past 24 is the next morning.</summary>
public sealed record DutyOperator(string Name, string ShortName, string Initials, string Role, string Badge,
    double ClockedIn, double ShiftEnd)
{
    public string ClockedInText => ShiftClock.Format(ClockedIn);
    public string ShiftEndText => ShiftClock.Format(ShiftEnd);
}

public sealed record ChatLine(string Who, string Text, double Hour);

public sealed partial record RiverVessel(
    string Id,
    string Name,
    string Order,
    double Km,
    double Speed,
    int Direction,
    double Eta,
    int SlipMinutes,
    string Master,
    string Next,
    bool Boxship,
    IReadOnlyList<ChatLine> Thread);

public sealed record GaugeDef(string Name, double Km, double Level, string Tone, string Trend);

public sealed record LockDef(string Name, double Km, string Status, string Tone);

public sealed record NoticeDef(string Title, string Detail, double Km, string Tone);

public sealed record LockingDef(string Time, string Lock, string Vessel, string State);

public sealed partial record YardZoneDef(string Id, string Name, string Sub, double X, double Y, double W, double H, string TintToken, bool Dashed);

public sealed record SecurityZoneDef(string Label, string Sub, double X, double Y, double W, double H, string TintToken, double FillOpacity, bool Dashed);

public sealed record SecurityEventDef(double Hour, string Label, string Tone);

public sealed record SecurityRecordDef(string Label, string Count, string Tone);

public sealed record PersonDef(string Name, string Role, string Credential, IReadOnlyList<int> Zones, string AvatarToken);

public sealed record AccessZoneDef(string Label, double X, double Y, double W, double H, string TintToken);

public sealed partial record VehicleDef(string Id, string Driver, string Container, string Gate, string Authorization, string AuthToken, string Entry, string TintToken);

public sealed record InspectionStepDef(string Label, string Detail, string Kind, double X, double Y);

public sealed record InspectionLogDef(string Time, string Mark, string Text, string Tone);

public sealed record ActivityDef(string Time, string Kind, string Title, string Detail);

public sealed partial record NavSection(string Id, string Label);
