namespace Cargo.Presentation;

public sealed partial class Shell : UserControl, IContentControlProvider
{
    public Shell()
    {
        InitializeComponent();

        // The shell is the window content: Uno.Extensions' theme service sets its RequestedTheme (the saved
        // System, Light or Dark choice, System by default), so its theme is every element's theme. The code
        // palette follows whatever the window ends up in, including a device switch under System.
        ActualThemeChanged += (_, _) => Tokens.Apply(ActualTheme);
        Loaded += (_, _) => Tokens.Apply(ActualTheme);
    }

    public ContentControl ContentControl => Host;
}
