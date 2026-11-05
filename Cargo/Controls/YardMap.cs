namespace Cargo.Controls;

/// <summary>
/// The container yard from above. Every slot is a stack of up to five tiers: darker tops
/// are taller stacks, and a slot that has just been worked keeps an amber ring for a minute.
/// The RTGs and the yard tractors run off a shared timer, not repeating storyboards. The map
/// only draws: the slots are <see cref="YardGrid.Slots"/>, and the store works the stacks.
/// </summary>
public sealed partial class YardMap : SceneHost
{
    private readonly PortState _state;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly TranslateTransform _rtgA = new();
    private readonly TranslateTransform _rtgB = new();
    private readonly TranslateTransform _truckA = new();
    private readonly TranslateTransform _truckB = new();

    private readonly DateTimeOffset _origin = DateTimeOffset.Now;

    // Each slot's elements and the state they were drawn for, so a yard move or a click
    // redraws only the slots that changed rather than all ~300 of them.
    private readonly Dictionary<string, (List<UIElement> Elements, int Height, bool Selected, bool Moved)> _drawn = new();
    private IReadOnlyList<(string Key, YardZoneDef Zone, string Slot, int Base, double X, double Y)>? _slots;

    public YardMap(PortState state) : base(1100, 560)
    {
        _state = state;

        _timer.Tick += (_, _) => Animate();
        this.RunWhileShown(_timer);

        // The keyboard's way to the stacks a pointer clicks: tab to the map, the arrows step through a block's slots
        // and the tiers panel follows; Escape clears
        IsTabStop = true;
        UseSystemFocusVisuals = true;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(this,
            "Yard map. Arrow keys move between the stacks of a block and show the selected stack's tiers; Escape clears.");
        KeyDown += OnKeyDown;

        Build();
        this.RebuildWhenVisible(state, UpdateSlots);
        this.RebuildOnYardMove(state, UpdateSlots);
        this.RebuildOnTheme(Build);
    }

    private void Build()
    {
        Scene.Children.Clear();

        Scene.Place(Draw.Rect(0, 0, 1100, 560, Tokens.Brush("LandBrush")));

        // Haul roads
        Scene.Place(Draw.Rect(0, 270, 1100, 22, Tokens.Brush("RoadBrush")));
        Scene.Place(Draw.Rule(0, 281, 1100, 281, Tokens.Brush("BackgroundBrush"), 1.5, dash: Draw.Dash(9, 8)));
        Scene.Place(Draw.Rect(540, 0, 22, 560, Tokens.Brush("RoadBrush")));

        AddTruck(_truckA, "CargoHazardColor");
        AddTruck(_truckB, "CargoStandardColor");

        // Blocks
        foreach (var zone in PortData.YardZones)
        {
            var tint = Tokens.Color(zone.TintToken);
            var box = Draw.Rect(zone.X, zone.Y, zone.W, zone.H, Tokens.Of(tint, 0.14), 10, Tokens.Of(tint, 0.35), 1);
            if (zone.Dashed)
            {
                box.StrokeDashArray = Draw.Dash(6, 4);
            }

            Scene.Place(box);
            Scene.Place(Draw.Text(zone.Name, zone.X + 14, zone.Y + 8, 13, Tokens.Brush("OnSurfaceBrush"), "BodyStrong"));
            Scene.Place(Draw.Text(zone.Sub, zone.X + 14, zone.Y + 26, 12, Tokens.Brush("OnSurfaceVariantBrush")));
        }

        AddRtg(_rtgA, 52);
        AddRtg(_rtgB, 602);

        _slots = YardGrid.Slots;
        _drawn.Clear();
        UpdateSlots();
    }

    /// <summary>Redraws only the slots whose height, selection or "just moved" ring changed.</summary>
    private void UpdateSlots()
    {
        if (_slots is null)
        {
            return;
        }

        foreach (var (key, zone, _, baseHeight, x, y) in _slots)
        {
            var height = _state.Yard.Height(key, baseHeight);
            var selected = _state.Yard.SelectedStack == key;
            var moved = _state.Yard.RecentlyMoved(key);

            if (_drawn.TryGetValue(key, out var drawn)
                && drawn.Height == height && drawn.Selected == selected && drawn.Moved == moved)
            {
                continue;
            }

            if (drawn.Elements is not null)
            {
                foreach (var element in drawn.Elements)
                {
                    Scene.Children.Remove(element);
                }
            }

            _drawn[key] = (DrawSlot(key, zone, x, y, height, selected, moved), height, selected, moved);
        }
    }

    private List<UIElement> DrawSlot(string key, YardZoneDef zone, double x, double y, int height, bool selected, bool moved)
    {
        var elements = new List<UIElement>();
        var tint = Tokens.Color(zone.TintToken);

        if (height == 0)
        {
            // A filled (near-transparent) empty slot, so the whole slot takes the click, not just its outline
            var empty = Draw.Rect(x, y, 26, 16, Tokens.Brush("OnSurfaceColor", 0.02), 1.5, Tokens.Brush("ApronLineColor", 0.5), 1, 0.9);
            empty.StrokeDashArray = Draw.Dash(3, 3);
            Bind(empty, key);
            elements.Add(Scene.Place(empty));
        }
        else
        {
            var opacity = height == 5 ? 1 : 0.62 + height * 0.07;
            var top = Draw.Rect(x, y, 26, 16, Tokens.Of(tint, opacity), 1.5);
            Bind(top, key);
            elements.Add(Scene.Place(top));

            // Taller stacks read darker, so height is legible at a glance
            elements.Add(Scene.Place(Unhit(Draw.Rect(x, y, 26, Math.Round((height - 1) * 3.5), Tokens.Brush("OnSurfaceColor", 0.18), 1.5))));
            elements.Add(Scene.Place(Unhit(Draw.Rect(x + 3, y + 9, Math.Round(20d * height / 5), 4, Tokens.Brush("DeckWhiteColor", 0.55), 1))));
        }

        if (selected)
        {
            elements.Add(Scene.Place(Unhit(Draw.Rect(x, y, 26, 16, null, 1.5, Tokens.Brush("OnSurfaceBrush"), 2))));
        }

        if (moved)
        {
            elements.Add(Scene.Place(Unhit(Draw.Rect(x, y, 26, 16, null, 1.5, Tokens.Brush("AmberBrush"), 2))));
        }

        return elements;
    }

    // xaml-lint: allow codebehind - keyboard selection on a code-built map; the slots are not focusable elements
    private void OnKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (_slots is null)
        {
            return;
        }

        if (e.Key == Windows.System.VirtualKey.Escape && _state.Yard.SelectedStack is not null)
        {
            _state.Yard.SelectedStack = null;
            e.Handled = true;
            return;
        }

        var (rows, columns) = e.Key switch
        {
            Windows.System.VirtualKey.Up => (-1, 0),
            Windows.System.VirtualKey.Down => (1, 0),
            Windows.System.VirtualKey.Left => (0, -1),
            Windows.System.VirtualKey.Right => (0, 1),
            _ => (0, 0)
        };

        if (rows != 0 || columns != 0)
        {
            _state.Yard.SelectedStack = YardGrid.Step(_slots.Select(s => s.Key).ToList(), _state.Yard.SelectedStack, rows, columns);
            e.Handled = true;
        }
    }

    /// <summary>Overlays must not swallow the click meant for the slot beneath them.</summary>
    private static T Unhit<T>(T element) where T : UIElement
    {
        element.IsHitTestVisible = false;
        return element;
    }

    private void Bind(UIElement element, string key)
    {
        // The slot for a screen reader: where it is and how high it stands (the map itself takes the arrows)
        var slot = YardGrid.Slots.FirstOrDefault(s => s.Key == key);
        if (slot.Key is not null)
        {
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(element,
                $"Slot {slot.Slot}, {slot.Zone.Name}, {_state.Yard.Height(key, slot.Base)} of 5 tiers");
        }

        element.PointerPressed += (_, _) =>
            _state.Yard.SelectedStack = _state.Yard.SelectedStack == key ? null : key;
    }

    private void AddRtg(TranslateTransform transform, double x)
    {
        var rtg = new Canvas { RenderTransform = transform };
        rtg.Place(Draw.Rect(x, 92, 100, 120, null, 4, Tokens.Brush("PrimaryBrush", 0.9), 5));
        rtg.Place(Draw.Rect(x + 44, 96, 14, 112, Tokens.Brush("PrimaryColor", 0.35)));
        foreach (var (dx, dy) in new[] { (-2d, -4d), (92d, -4d), (-2d, 116d), (92d, 116d) })
        {
            rtg.Place(Draw.Rect(x + dx, 92 + dy, 10, 8, Tokens.Brush("CargoOversizeColor"), 2));
        }

        Scene.Place(rtg);
    }

    private void AddTruck(TranslateTransform transform, string tintToken)
    {
        var truck = new Canvas { RenderTransform = transform };
        truck.Place(Draw.Rect(0, 274, 34, 14, Tokens.Brush(tintToken), 1.5));
        truck.Place(Draw.Rect(35, 275, 10, 12, Tokens.Brush("DeckWhiteColor"), 2, Tokens.Brush("QuayBrush"), 1));
        Scene.Place(truck);
    }

    private void Animate()
    {
        var seconds = (DateTimeOffset.Now - _origin).TotalSeconds;

        // RTGs track along their blocks and come back
        var rtg = Math.Abs((seconds % 18) / 9 - 1);
        _rtgA.X = 330 * (1 - rtg);
        _rtgB.X = 330 * (1 - Math.Abs(((seconds + 7) % 18) / 9 - 1));

        // Tractors run the length of the haul road
        _truckA.X = (seconds % 22) / 22 * 1180 - 80;
        _truckB.X = ((seconds + 11) % 22) / 22 * 1180 - 80;
    }
}
