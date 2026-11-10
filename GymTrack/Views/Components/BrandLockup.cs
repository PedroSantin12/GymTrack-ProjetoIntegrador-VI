namespace GymTrack.Views.Components;

public sealed class BrandLockup : ContentView
{
    private bool _animating;

    public BrandLockup()
    {
        var mark = new ImageButton
        {
            Source = "gymtrack_mark.png",
            WidthRequest = 54,
            HeightRequest = 54,
            Padding = 0,
            CornerRadius = 0,
            BorderWidth = 0,
            BackgroundColor = Colors.Transparent,
            AutomationId = "GymTrackBrand"
        };
        SemanticProperties.SetDescription(mark, "Símbolo GymTrack");
        SemanticProperties.SetHint(mark, "Anima a marca");
        mark.Clicked += async (_, _) =>
        {
            if (_animating) return;
            _animating = true;
            try
            {
                await Task.WhenAll(mark.ScaleToAsync(.88, 90, Easing.CubicOut),
                    mark.RotateToAsync(-12, 90, Easing.CubicOut));
                await Task.WhenAll(mark.ScaleToAsync(1, 190, Easing.SpringOut),
                    mark.RotateToAsync(0, 190, Easing.SpringOut));
            }
            finally
            {
                mark.Scale = 1;
                mark.Rotation = 0;
                _animating = false;
            }
        };

        var wordmark = Ui.Text(string.Empty, 27, Ui.Foreground, FontAttributes.Bold);
        wordmark.VerticalTextAlignment = TextAlignment.Center;
        wordmark.CharacterSpacing = 1.2;
        wordmark.FormattedText = new FormattedString
        {
            Spans =
            {
                new Span { Text = "GYM", TextColor = Ui.Foreground },
                new Span { Text = "TRACK", TextColor = Ui.Accent }
            }
        };
        Content = new HorizontalStackLayout
        {
            HorizontalOptions = LayoutOptions.Center,
            Spacing = 12,
            Children = { mark, wordmark }
        };
    }
}
