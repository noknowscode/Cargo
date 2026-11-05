using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;

namespace UnoOrbit.Components;

public sealed partial class StarfieldControl : UserControl
{
    private readonly Random _rng = new();
    private bool _created;

    public StarfieldControl()
    {
        this.InitializeComponent();
        this.SizeChanged += OnSizeChanged;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_created) return;

        double w = e.NewSize.Width;
        double h = e.NewSize.Height;
        if (w <= 0 || h <= 0) return;

        _created = true;
        CreateStars(w, h);
    }

    private void CreateStars(double w, double h)
    {
        for (int i = 0; i < 200; i++)
        {
            double size = 0.5 + _rng.NextDouble() * 2.0;
            var star = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(Microsoft.UI.Colors.White),
            };

            Canvas.SetLeft(star, _rng.NextDouble() * w);
            Canvas.SetTop(star, _rng.NextDouble() * h);
            star.Opacity = 0.1 + _rng.NextDouble() * 0.6;

            StarCanvas.Children.Add(star);

            // Twinkle animation using keyframes
            double period = 2 + _rng.NextDouble() * 4;
            double delay = _rng.NextDouble() * 3;

            var anim = new DoubleAnimationUsingKeyFrames
            {
                BeginTime = TimeSpan.FromSeconds(delay),
                RepeatBehavior = RepeatBehavior.Forever,
            };
            anim.KeyFrames.Add(new LinearDoubleKeyFrame
            {
                KeyTime = TimeSpan.Zero,
                Value = 0.1,
            });
            anim.KeyFrames.Add(new LinearDoubleKeyFrame
            {
                KeyTime = TimeSpan.FromSeconds(period / 2),
                Value = 0.7,
            });
            anim.KeyFrames.Add(new LinearDoubleKeyFrame
            {
                KeyTime = TimeSpan.FromSeconds(period),
                Value = 0.1,
            });

            Storyboard.SetTarget(anim, star);
            Storyboard.SetTargetProperty(anim, "Opacity");

            var sb = new Storyboard();
            sb.Children.Add(anim);
            sb.Begin();
        }
    }
}
