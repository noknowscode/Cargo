namespace Cargo.Domain;

/// <summary>
/// What waits on the dispatcher, as the Needs-you queue lists it: the inspection hold, the driver check, the late river
/// orders and the arrival whose berth nobody has confirmed, in the design's order; and the actions that settle the rows
/// other than the berth's. The berth's row settles on the berth desk (<see cref="PortState.Confirm"/>), which rebuilds
/// the queue when the arrival waiting on a berth changes. The queue is rebuilt when what needs deciding changes; the
/// clock only moves each row's timing (<see cref="Tick"/>).
/// </summary>
public sealed partial class NeedsYouQueue : ObservableObject
{
    /// <summary>The physical inspection on the held container is booked for 21:15.</summary>
    private const double InspectionDue = 21.25;

    private readonly PortSignals _signals;
    private readonly CrewChat _chat;
    private readonly Func<double> _nowHours;
    private readonly Func<string?> _pendingArrival;
    private readonly Func<string, string?> _berthOf;

    // What the dispatcher has done about the rows other than the berth: each one settles its row
    private readonly HashSet<string> _verifiedDrivers = new();
    private readonly HashSet<string> _etaConfirmed = new();

    /// <summary>
    /// The queue on the store's clock (<paramref name="nowHours"/>), reading the berth desk's waiting arrival
    /// (<paramref name="pendingArrival"/>) and the berth the plan gives a vessel (<paramref name="berthOf"/>); a revised
    /// ETA is sent on the crews' channel (<paramref name="chat"/>).
    /// </summary>
    public NeedsYouQueue(PortSignals signals, CrewChat chat, Func<double> nowHours, Func<string?> pendingArrival,
        Func<string, string?> berthOf)
    {
        _signals = signals;
        _chat = chat;
        _nowHours = nowHours;
        _pendingArrival = pendingArrival;
        _berthOf = berthOf;
    }

    /// <summary>Everything waiting on the dispatcher: the hold, the driver check, the late orders, the berth.</summary>
    public ObservableCollection<Decision> Decisions { get; } = new();

    /// <summary>What needs you, counted as the queue lists it.</summary>
    public int Count => Decisions.Count;

    /// <summary>
    /// Whether a section holds something waiting on the dispatcher, for the header's needs-you dots: Berths the
    /// unconfirmed arrival, Waterways the late orders (their ETAs are confirmed with the crews there), Security the
    /// inspection hold and the driver check.
    /// </summary>
    public bool SectionNeedsYou(string section) => Decisions.Any(d => d.Kind switch
    {
        DecisionKind.BerthConfirm => section == "berths",
        DecisionKind.LateOrders => section == "fleet",
        _ => section == "security"
    });

    // ── The hold ─────────────────────────────────────────────────────────────

    /// <summary>The held container's 21:15 inspection has an inspector: the hold's row is settled (the hold stays until the inspection).</summary>
    [ObservableProperty]
    public partial bool InspectorAssigned { get; set; }

    /// <summary>The held container's re-weigh is booked on weighbridge 1.</summary>
    [ObservableProperty]
    public partial bool ReweighRequested { get; set; }

    /// <summary>Assigns the inspector to the held container's inspection, which settles the hold's row. False when already done.</summary>
    public bool AssignInspector()
    {
        if (InspectorAssigned)
        {
            return false;
        }

        InspectorAssigned = true;
        Settled();
        return true;
    }

    /// <summary>Books the held container's re-weigh. False when already booked.</summary>
    public bool RequestReweigh()
    {
        if (ReweighRequested)
        {
            return false;
        }

        ReweighRequested = true;
        _signals.Structure();
        return true;
    }

    // ── The driver check ─────────────────────────────────────────────────────

    /// <summary>A truck at the barrier still waiting on its driver's ID check.</summary>
    public bool AwaitsDriver(VehicleDef vehicle) =>
        vehicle.Authorization == "Awaiting driver ID" && !_verifiedDrivers.Contains(vehicle.Id);

    /// <summary>The truck's authorization as it stands: verified at the gate once the dispatcher checked the driver.</summary>
    public string AuthorizationOf(VehicleDef vehicle) =>
        _verifiedDrivers.Contains(vehicle.Id) ? "Authorized · driver ID verified" : vehicle.Authorization;

    /// <summary>Verifies the driver of a truck waiting at the barrier, which settles the driver row. False when there is nothing to verify.</summary>
    public bool VerifyDriver(string truckId)
    {
        if (PortData.Vehicles.FirstOrDefault(v => v.Id == truckId) is not { } truck || !AwaitsDriver(truck))
        {
            return false;
        }

        _verifiedDrivers.Add(truckId);
        Settled();
        return true;
    }

    // ── The late orders ──────────────────────────────────────────────────────

    /// <summary>A late river order whose revised ETA has not been confirmed with her crew yet.</summary>
    public bool AwaitsEta(RiverVessel vessel) => vessel.SlipMinutes > 0 && !_etaConfirmed.Contains(vessel.Id);

    /// <summary>The message that asks a late crew to confirm their revised ETA to the consignee.</summary>
    public static string RevisedEtaMessage(RiverVessel vessel) =>
        $"Please confirm revised ETA {ShiftClock.Format(vessel.Eta + vessel.SlipMinutes / 60d)} " +
        "to the consignee. Any further slip, call dispatch on VHF 12.";

    /// <summary>
    /// Sends the late crew the revised-ETA message; the order counts as handled, so the late-orders row drops it (and
    /// leaves when both are handled). A vessel on time is just sent the message.
    /// </summary>
    public void ConfirmRevisedEta(string vesselId)
    {
        var vessel = PortData.RiverFleet.First(v => v.Id == vesselId);
        _chat.Send(vesselId, RevisedEtaMessage(vessel));
        if (AwaitsEta(vessel))
        {
            _etaConfirmed.Add(vesselId);
            Settled();
        }
    }

    // ── The rows ─────────────────────────────────────────────────────────────

    /// <summary>A row other than the berth's was settled: the queue, the count, the dots and the log's chips follow.</summary>
    private void Settled()
    {
        Rebuild();
        _signals.Structure();
    }

    /// <summary>
    /// Rebuilds the queue from the data and the berth desk's waiting arrival, in the design's order. Called at start and
    /// when what needs deciding changes; the clock only updates the rows' timings.
    /// </summary>
    public void Rebuild()
    {
        Decisions.Clear();

        if (!InspectorAssigned && PortData.Containers.FirstOrDefault(c => c.Security == "Hold") is { } held)
        {
            Decisions.Add(new Decision
            {
                Id = $"hold-{held.Id}", Kind = DecisionKind.InspectionHold,
                Title = $"Hold on {held.DisplayId}", Sub = $"Inspection due {ShiftClock.Format(InspectionDue)}",
                ActionLabel = "Open inspection", IsPrimary = true, Subject = held.DisplayId
            });
        }

        if (PortData.Vehicles.FirstOrDefault(AwaitsDriver) is { } truck)
        {
            Decisions.Add(new Decision
            {
                Id = $"driver-{truck.Id}", Kind = DecisionKind.DriverCheck,
                Title = $"Driver ID pending at {truck.Gate.Split(" · ")[0]}", Sub = $"{truck.Id} at the barrier",
                ActionLabel = "Verify driver", IsPrimary = false, Subject = truck.Id
            });
        }

        var late = PortData.RiverFleet.Count(AwaitsEta);
        if (late > 0)
        {
            Decisions.Add(new Decision
            {
                Id = "late-orders", Kind = DecisionKind.LateOrders,
                Title = late == 1 ? "1 order late" : $"{late} orders late", Sub = late == 1 ? "In the yard queue" : "Both in the yard queue",
                ActionLabel = "View orders", IsPrimary = false
            });
        }

        if (_pendingArrival() is { } vessel)
        {
            Decisions.Add(new Decision
            {
                Id = $"berth-{vessel}", Kind = DecisionKind.BerthConfirm,
                Title = $"{PortData.Vessel(vessel).Name} needs berth {_berthOf(vessel)} confirmed", Sub = "Pilot aboard",
                ActionLabel = "Confirm berth", IsPrimary = false, VesselId = vessel
            });
        }

        Tick();
        OnPropertyChanged(nameof(Count));
    }

    /// <summary>Updates each row's timing in place: due or overdue, waiting since, arriving in.</summary>
    public void Tick()
    {
        var now = _nowHours();
        foreach (var d in Decisions)
        {
            switch (d.Kind)
            {
                case DecisionKind.InspectionHold:
                    var due = (int)Math.Round((InspectionDue - now) * 60);
                    d.IsLate = due < 0;
                    d.When = due < 0 ? $"{-due} min late" : $"in {due} min";
                    break;
                case DecisionKind.DriverCheck:
                    var since = ShiftClock.ClockHours(PortData.Vehicles.First(v => v.Id == d.Subject).Entry);
                    var waiting = double.IsNaN(since) ? 0 : (int)Math.Round((now - since) * 60);
                    d.IsLate = true;
                    d.When = $"waiting {Math.Max(0, waiting)} min";
                    break;
                case DecisionKind.BerthConfirm when d.VesselId is { } vessel:
                    d.When = $"arrives {ShiftClock.Display(PortData.Vessel(vessel).Eta)}";
                    break;
            }
        }
    }
}
