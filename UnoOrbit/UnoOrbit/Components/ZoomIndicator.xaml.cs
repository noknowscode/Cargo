namespace UnoOrbit.Components;

public sealed partial class ZoomIndicator : UserControl
{
    public event EventHandler? ZoomInClicked;
    public event EventHandler? ZoomOutClicked;

    public ZoomIndicator()
    {
        this.InitializeComponent();
    }

    public void UpdateZoomLevel(float zoom)
    {
        ZoomLevelText.Text = $"{zoom:F1}\u00d7";
    }

    private void OnZoomIn(object sender, RoutedEventArgs e)
    {
        ZoomInClicked?.Invoke(this, EventArgs.Empty);
    }

    private void OnZoomOut(object sender, RoutedEventArgs e)
    {
        ZoomOutClicked?.Invoke(this, EventArgs.Empty);
    }
}
