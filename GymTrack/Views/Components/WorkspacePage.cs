using GymTrack.Services;
using Microsoft.Maui.Controls.Shapes;

namespace GymTrack.Views.Components;

public class WorkspacePage : ContentPage
{
    private const int NavigationTileHeight = 56;
    private const int NavigationHeight = 68;
    private const int NavigationBottomMargin = 12;
    internal const int NavigationContentInset = NavigationHeight + NavigationBottomMargin + 12;

    private readonly Grid _layout;
    private View? _body;

    protected WorkspacePage(string title, AppSection activeSection,
        IAppNavigator navigator, AppSession session)
    {
        Title = title;
        NavigationPage.SetHasNavigationBar(this, false);
        BackgroundColor = Ui.Background;

        _layout = CreateLayout(activeSection, navigator, session);
        Content = _layout;
    }

    internal static Grid CreateLayout(AppSection activeSection, IAppNavigator navigator, AppSession session)
    {
        var layout = new Grid
        {
            MaximumWidthRequest = session.IsManagementMode ? 760 : 460,
            HorizontalOptions = LayoutOptions.Fill,
            SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container)
        };
        layout.Add(CreateNavigation(activeSection, navigator, session));
        return layout;
    }

    protected void SetBody(View body, bool scroll = true)
    {
        View container = scroll ? new ScrollView
        {
            Content = body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Never
        } : body;
        container.Margin = new Thickness(18, 14, 18, 0);
        if (container is ScrollView scrollView)
        {
            var padding = scrollView.Padding;
            scrollView.Padding = new Thickness(padding.Left, padding.Top, padding.Right,
                Math.Max(padding.Bottom, NavigationContentInset));
        }
        if (_body is not null) _layout.Remove(_body);
        _layout.Add(container);
        _body = container;
    }

    private static View CreateNavigation(AppSection section, IAppNavigator navigator, AppSession session)
    {
        var items = session.IsManagementMode
            ? new[] { (AppSection.AdminDashboard, "Início", "home"),
                (AppSection.Students, "Alunos", "users"),
                (AppSection.AdminWorkouts, "Treinos", "workout"),
                (AppSection.Exercises, "Exercícios", "exercises"),
                (AppSection.Profile, "Perfil", "profile") }
            : new[] { (AppSection.PersonalWorkouts, "Treinos", "workout"),
                (AppSection.Progress, "Evolução", "progress"),
                (AppSection.Profile, "Perfil", "profile") };

        var selectedRoute = section switch
        {
            AppSection.Templates => AppSection.PersonalWorkouts,
            AppSection.Users or AppSection.Statistics => AppSection.Profile,
            _ => section
        };
        var grid = new Grid { ColumnSpacing = 2 };
        for (var index = 0; index < items.Length; index++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var (route, label, icon) = items[index];
            var selected = selectedRoute == route;
            var color = selected ? Ui.Accent : Ui.Foreground;
            var caption = Ui.Text(label, session.IsManagementMode ? 10 : 11, color,
                selected ? FontAttributes.Bold : FontAttributes.None);
            caption.HorizontalTextAlignment = TextAlignment.Center;
            caption.LineBreakMode = LineBreakMode.TailTruncation;
            caption.MaxLines = 1;
            var content = new VerticalStackLayout
            {
                Spacing = 3,
                VerticalOptions = LayoutOptions.Center,
                InputTransparent = true,
                Children = { CreateIcon(icon, color), caption }
            };
            var button = new Button
            {
                Text = string.Empty,
                HeightRequest = NavigationTileHeight,
                CornerRadius = NavigationTileHeight / 2,
                BackgroundColor = selected ? Color.FromArgb("#3A3A3F") : Colors.Transparent,
                AutomationId = $"Navigate{route}"
            };
            button.SetDynamicResource(VisualElement.StyleProperty, "QuietButton");
            SemanticProperties.SetDescription(button, label + (selected ? ", selecionado" : ""));
            button.Clicked += async (_, _) =>
            {
                if (!button.IsEnabled || section == route) return;
                button.IsEnabled = false;
                try { await navigator.NavigateAsync(route); }
                catch { await Ui.ShowErrorAsync("Navegação indisponível", "Confira seu acesso e tente novamente."); }
                finally { button.IsEnabled = true; }
            };
            grid.Add(new Grid { Children = { button, content } }, index);
        }

        var sideMargin = session.IsManagementMode ? 12 : 32;
        return new Border
        {
            ZIndex = 1,
            HeightRequest = NavigationHeight,
            MaximumWidthRequest = session.IsManagementMode ? 500 : 356,
            Margin = new Thickness(sideMargin, 0, sideMargin, NavigationBottomMargin),
            VerticalOptions = LayoutOptions.End,
            Padding = new Thickness(5),
            BackgroundColor = Ui.Secondary.WithAlpha(0.92f),
            Stroke = new SolidColorBrush(Color.FromArgb("#45454B")),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = NavigationHeight / 2 },
            Shadow = new Shadow { Brush = Colors.Black, Offset = new Point(0, 5), Radius = 14, Opacity = 0.5f },
            Content = grid
        };
    }

    private static View CreateIcon(string name, Color color)
    {
        var data = name switch
        {
            "home" => "M 3,11 L 12,3 L 21,11 M 5,10 L 5,21 L 10,21 L 10,15 L 14,15 L 14,21 L 19,21 L 19,10",
            "workout" => "M 8,12 L 16,12 M 4,7 L 8,7 L 8,17 L 4,17 Z M 16,7 L 20,7 L 20,17 L 16,17 Z M 2,10 L 4,10 M 20,10 L 22,10 M 2,14 L 4,14 M 20,14 L 22,14",
            "exercises" => "M 5,4 L 19,4 L 19,21 L 5,21 Z M 8,8 L 16,8 M 8,12 L 16,12 M 8,16 L 13,16",
            "progress" => "M 4,3 L 4,21 L 22,21 M 8,16 L 12,11 L 16,14 L 21,6",
            "users" => "M 14,7 A 4,4 0 1 1 6,7 A 4,4 0 1 1 14,7 M 3,21 C 3,12 17,12 17,21 M 18,4 C 23,4 23,11 18,11 M 19,15 C 23,16 23,19 23,21",
            _ => "M 16,7 A 4,4 0 1 1 8,7 A 4,4 0 1 1 16,7 M 3,22 C 3,10 21,10 21,22"
        };
        return new Microsoft.Maui.Controls.Shapes.Path
        {
            Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString(data)!,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 1.8,
            StrokeLineCap = PenLineCap.Round,
            WidthRequest = 23,
            HeightRequest = 23,
            HorizontalOptions = LayoutOptions.Center,
            Aspect = Stretch.Uniform
        };
    }
}
