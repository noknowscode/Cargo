namespace Cargo.Domain;

/// <summary>
/// The DEBUG verification hooks, CARGO_* environment variables: a headless run opens straight onto a section, a
/// selection, a hover, a size or a theme that a background process cannot reach with synthesized input. Every hook
/// is read here, and a Release build never reads the environment: each one is null there.
/// </summary>
public static class DebugHooks
{
#if DEBUG
    public static string? Hook(string name) => Environment.GetEnvironmentVariable(name);
#else
    public static string? Hook(string name) => null;
#endif
}
