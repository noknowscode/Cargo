using Uno.UI.Hosting;

namespace Cargo;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
#if DEBUG
            // Verification runs under Xvfb: CARGO_X11_RENDERER=software draws with Skia's CPU backend instead of
            // Mesa's llvmpipe OpenGL, which rasterises every frame on the CPU at several times the cost
            .UseX11(x11 =>
            {
                if (Cargo.Domain.DebugHooks.Hook("CARGO_X11_RENDERER") == "software")
                {
                    x11.RenderingBackend(X11RenderingBackend.Software);
                }
            })
#else
            .UseX11()
#endif
            .UseLinuxFrameBuffer()
            .UseMacOS()
            .UseWin32()
            .Build();

        host.Run();
    }
}
