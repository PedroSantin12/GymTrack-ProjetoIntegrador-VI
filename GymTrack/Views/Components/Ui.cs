using Microsoft.Maui.Controls.Shapes;

namespace GymTrack.Views.Components;

public static class Ui
{
    internal const string PeopleIcon = "M 14,7 A 4,4 0 1 1 6,7 A 4,4 0 1 1 14,7 M 3,21 C 3,12 17,12 17,21 M 18,4 C 23,4 23,11 18,11 M 19,15 C 23,16 23,19 23,21";

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

    internal static View LineIcon(string data, Color color, int size) => new Microsoft.Maui.Controls.Shapes.Path
    {
        Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString(data)!,
        Stroke = new SolidColorBrush(color),
        StrokeThickness = 1.7,
        StrokeLineCap = PenLineCap.Round,
        StrokeLineJoin = PenLineJoin.Round,
        WidthRequest = size,
        HeightRequest = size,
        HorizontalOptions = LayoutOptions.Center,
        VerticalOptions = LayoutOptions.Center,
        Aspect = Stretch.Uniform,
        InputTransparent = true
    };

    internal static Border LinkCard(string title, string description, string icon, Func<Task> action)
    {
        var iconBackground = Card(LineIcon(icon, Accent, 22), new Thickness(7));
        iconBackground.BackgroundColor = Accent.WithAlpha(0.1f);
        iconBackground.StrokeThickness = 0;
        iconBackground.StrokeShape = new RoundRectangle { CornerRadius = 10 };
        iconBackground.VerticalOptions = LayoutOptions.Center;

        var row = new Grid
        {
            Padding = new Thickness(12, 10),
            ColumnSpacing = 11,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            InputTransparent = true
        };
        row.Add(iconBackground);
        row.Add(new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            Spacing = 4,
            Children = { Text(title, 15, attributes: FontAttributes.Bold), Text(description, 12, Muted) }
        }, 1);
        row.Add(LineIcon("M 9,5 L 15,12 L 9,19", Muted, 15), 2);
        AutomationProperties.SetExcludedWithChildren(row, true);

        var button = Button(string.Empty, action);
        button.SetDynamicResource(VisualElement.StyleProperty, "QuietButton");
        SemanticProperties.SetDescription(button, $"{title}, {description}");
        var card = Card(new Grid { MinimumHeightRequest = 66, Children = { button, row } }, new Thickness(0));
        card.StrokeShape = new RoundRectangle { CornerRadius = 17 };
        return card;
    }

    internal static Border OptionField(string title, Label value, Func<Task> action, string automationId)
    {
        var button = Button(string.Empty, action);
        button.AutomationId = automationId;
        button.SetDynamicResource(VisualElement.StyleProperty, "QuietButton");
        button.SetBinding(SemanticProperties.DescriptionProperty, new Binding(
            nameof(Label.Text), source: value, stringFormat: $"{title}, {{0}}"));

        var labels = new VerticalStackLayout
        {
            Spacing = 2,
            Margin = new Thickness(12, 7, 0, 7),
            VerticalOptions = LayoutOptions.Center,
            InputTransparent = true,
            Children = { Text(title, 12, Muted), value }
        };
        AutomationProperties.SetExcludedWithChildren(labels, true);
        var arrow = LineIcon("M 1,1 L 7,7 L 13,1", Muted, 14);
        arrow.Margin = new Thickness(0, 0, 12, 0);
        var row = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 8
        };
        row.Add(button);
        Grid.SetColumnSpan(button, 2);
        row.Add(labels);
        row.Add(arrow, 1);
        return Card(row, new Thickness(0));
    }

    public static Button Button(string text, Func<Task> action, bool secondary = false)
    {
        var button = new Button { Text = text };
        if (secondary)
            button.SetDynamicResource(VisualElement.StyleProperty, "SecondaryButton");
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
            if (GetActivePage() is { } page)
                await page.ShowAppMessageAsync(title, message);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(exception);
        }
    }

    internal static Page? GetActivePage()
    {
        var root = Application.Current?.Windows.FirstOrDefault()?.Page;
        return root is null ? null : GetVisiblePage(root.Navigation.ModalStack.LastOrDefault() ?? root);
    }

    private static Page GetVisiblePage(Page page) => page switch
    {
        NavigationPage { CurrentPage: { } current } => GetVisiblePage(current),
        Shell { CurrentPage: { } current } => GetVisiblePage(current),
        FlyoutPage { Detail: { } detail } => GetVisiblePage(detail),
        TabbedPage { CurrentPage: { } current } => GetVisiblePage(current),
        _ => page
    };

    public static Label Tag(string text) => new()
    {
        Text = text.ToUpperInvariant(),
        TextColor = Accent,
        FontSize = 11,
        CharacterSpacing = 1.5,
        FontFamily = "OpenSansSemibold"
    };
}
