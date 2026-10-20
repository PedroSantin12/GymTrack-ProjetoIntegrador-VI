using Microsoft.Maui.Controls.Shapes;

namespace GymTrack.Views.Components;

public static class Ui
{
    public static readonly Color Accent = Color.FromArgb("#FF913A");
    public static readonly Color Background = Color.FromArgb("#0E0E10");
    public static readonly Color Secondary = Color.FromArgb("#17171A");
    public static readonly Color Surface = Color.FromArgb("#1C1C20");
    public static readonly Color Elevated = Color.FromArgb("#252529");
    public static readonly Color Border = Color.FromArgb("#303036");
    public static readonly Color Muted = Color.FromArgb("#9A9AA3");
    public static readonly Color Foreground = Color.FromArgb("#F5F5F7");

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
        Padding = padding ?? new Thickness(16),
        BackgroundColor = Surface,
        Stroke = new SolidColorBrush(Border),
        StrokeThickness = 1,
        StrokeShape = new RoundRectangle { CornerRadius = 14 }
    };

    public static Button Button(string text, Func<Task> action, bool secondary = false)
    {
        var button = new Button
        {
            Text = text,
            BackgroundColor = secondary ? Surface : Accent,
            TextColor = secondary ? Foreground : Background,
            BorderColor = secondary ? Border : Colors.Transparent,
            BorderWidth = secondary ? 1 : 0,
            CornerRadius = 12,
            Padding = new Thickness(14, 9),
            MinimumHeightRequest = 48,
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
