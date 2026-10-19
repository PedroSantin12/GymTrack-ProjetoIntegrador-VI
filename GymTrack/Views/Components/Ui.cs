using Microsoft.Maui.Controls.Shapes;

namespace GymTrack.Views.Components;

public static class Ui
{
    public static readonly Color Accent = Color.FromArgb("#FF913A");
    public static readonly Color Background = Color.FromArgb("#171717");
    public static readonly Color Surface = Color.FromArgb("#272727");
    public static readonly Color Elevated = Color.FromArgb("#4C4C4C");
    public static readonly Color Muted = Color.FromArgb("#B9B9B9");
    public static readonly Color Foreground = Color.FromArgb("#FAFAFA");

    public static Label Text(string text, int size = 14, Color? color = null,
        FontAttributes attributes = FontAttributes.None) => new()
    {
        Text = text,
        FontSize = size,
        TextColor = color ?? Foreground,
        FontFamily = attributes == FontAttributes.Bold ? "OpenSansSemibold" : "OpenSansRegular",
        FontAttributes = attributes,
        LineBreakMode = LineBreakMode.WordWrap
    };

    public static Border Card(View content, Thickness? padding = null) => new()
    {
        Content = content,
        Padding = padding ?? new Thickness(20),
        BackgroundColor = Surface,
        Stroke = new SolidColorBrush(Color.FromArgb("#4C4C4C")),
        StrokeThickness = 1,
        StrokeShape = new RoundRectangle { CornerRadius = 22 }
    };

    public static Button Button(string text, Func<Task> action, bool secondary = false)
    {
        var button = new Button
        {
            Text = text,
            BackgroundColor = secondary ? Elevated : Accent,
            TextColor = secondary ? Foreground : Background,
            CornerRadius = 14,
            Padding = new Thickness(16, 12),
            MinimumHeightRequest = 50,
            FontFamily = "OpenSansSemibold",
            FontSize = 14
        };
        button.Clicked += async (_, _) =>
        {
            if (!button.IsEnabled) return;
            button.IsEnabled = false;
            try
            {
                await action();
            }
            catch (Exception)
            {
                await ShowErrorAsync("Não foi possível concluir", "Tente novamente em instantes.");
            }
            finally
            {
                button.IsEnabled = true;
            }
        };
        return button;
    }

    public static async Task ShowErrorAsync(string title, string message)
    {
        try
        {
            if (Application.Current?.Windows.FirstOrDefault()?.Page is { } page)
                await page.DisplayAlertAsync(title, message, "Entendi");
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(exception);
        }
    }

    public static Label Tag(string text) => new()
    {
        Text = text.ToUpperInvariant(),
        TextColor = Accent,
        FontSize = 11,
        CharacterSpacing = 1.5,
        FontFamily = "OpenSansSemibold"
    };
}
