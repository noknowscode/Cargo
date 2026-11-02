namespace Cargo.Presentation;

/// <summary>
/// The full-window container inspection. It lives in the shell rather than inside the
/// Cargo page so it covers the whole app the way the design's fixed overlay does.
/// </summary>
public sealed partial class ScannerOverlay : UserControl
{
    public ScannerOverlay()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            // xaml-lint: allow codebehind - the 3D scanner is a code-built panel that reads the store directly
            if (ViewModel is { } vm)
            {
                Scanner3D.State = vm.State;
            }

            Bindings.Update();
        };

        // xaml-lint: allow codebehind - keyboard focus has no binding: an opened inspection takes it, so Tab does not
        // wander into the dimmed page behind
        RegisterPropertyChangedCallback(VisibilityProperty, (_, _) =>
        {
            if (Visibility == Visibility.Visible)
            {
                DispatcherQueue.TryEnqueue(() => CloseButton.Focus(FocusState.Programmatic));
            }
        });
    }

    // xaml-lint: allow codebehind - Escape closes the inspection, as its × does
    private void OnKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape && ViewModel?.CloseCommand.CanExecute(null) == true)
        {
            ViewModel.CloseCommand.Execute(null);
            e.Handled = true;
        }
    }

    public ScannerViewModel? ViewModel => DataContext as ScannerViewModel;
}
