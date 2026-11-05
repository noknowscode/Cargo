using UnoOrbit.Models;

namespace UnoOrbit.State;

public class OrbitState
{
    public float Zoom = 1.0f;
    public float TargetZoom = 1.0f;
    public const float MinZoom = 0.6f;
    public const float MaxZoom = 4.5f;

    public float PanX = 0f;
    public float PanY = 0f;
    public float TargetPanX = 0f;
    public float TargetPanY = 0f;

    public int? SelectedPlanet = null;
    public int? HoveredPlanet = null;

    public float Time = 0f;

    public bool HintDismissed = false;

    // Smooth interpolation for selection effects (0 = unselected, 1 = fully selected)
    public float SelectionIntensity = 0f;
    public float TargetSelectionIntensity = 0f;

    public const float SelectedZoom = 3.2f;

    public void SelectPlanet(int index, float centerX, float centerY, float baseRadius)
    {
        SelectedPlanet = index;
        TargetZoom = SelectedZoom;
        TargetSelectionIntensity = 1f;
        HintDismissed = true;

        var planet = Planets.All[index];

        // Pan so that the planet ends up at screen center
        float orbitR = baseRadius * planet.OrbitRadius * SelectedZoom;
        float angle = Time * planet.Speed * 0.5f;
        TargetPanX = -(MathF.Cos(angle) * orbitR);
        TargetPanY = -(MathF.Sin(angle) * orbitR * 0.55f);
    }

    public void DeselectPlanet()
    {
        SelectedPlanet = null;
        TargetZoom = 1.0f;
        TargetSelectionIntensity = 0f;
        TargetPanX = 0f;
        TargetPanY = 0f;
    }

    public static (float X, float Y, float R) GetPlanetPosition(
        PlanetData planet, float time, float zoom,
        float centerX, float centerY, float baseRadius)
    {
        float r = baseRadius * planet.OrbitRadius * zoom;
        float angle = time * planet.Speed * 0.5f;
        return (
            centerX + MathF.Cos(angle) * r,
            centerY + MathF.Sin(angle) * r * 0.55f,
            planet.Size * zoom * 0.7f
        );
    }

    public static int? HitTestPlanet(
        float pointerX, float pointerY,
        OrbitState state, float centerX, float centerY, float baseRadius)
    {
        for (int i = Planets.All.Length - 1; i >= 0; i--)
        {
            var (px, py, pr) = GetPlanetPosition(
                Planets.All[i], state.Time, state.Zoom, centerX, centerY, baseRadius);
            float hitR = MathF.Max(pr + 8f, 15f);
            float dx = pointerX - px;
            float dy = pointerY - py;
            if (dx * dx + dy * dy < hitR * hitR)
                return i;
        }
        return null;
    }
}
