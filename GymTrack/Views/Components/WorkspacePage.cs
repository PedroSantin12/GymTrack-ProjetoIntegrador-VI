using GymTrack.Services;
using Microsoft.Maui.Controls.Shapes;

namespace GymTrack.Views.Components;

public class WorkspacePage : ContentPage
{
    private readonly Grid _layout;
    private View? _body;

    protected WorkspacePage(string title, string subtitle, AppSection activeSection,
        IAppNavigator navigator, AppSession session)
    {
        Title = title;
        NavigationPage.SetHasNavigationBar(this, false);
        BackgroundColor = Ui.Background;

        var header = new VerticalStackLayout
        {
            Spacing = 3,
            Padding = new Thickness(18, 16, 18, 10),
            Children =
            {
                Ui.Text(session.IsEmployee ? "GYMTRACK  /  GESTÃO" : "GymTrack", 15,
                    Ui.Accent, FontAttributes.Bold),
                Ui.Text(title, 24, Ui.Foreground, FontAttributes.Bold),
                Ui.Text(subtitle, 13, Ui.Muted)
            }
        };

        _layout = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            MaximumWidthRequest = session.IsEmployee ? 760 : 460,
            HorizontalOptions = LayoutOptions.Fill
        };
        _layout.Add(header, 0, 0);
        _layout.Add(CreateNavigation(activeSection, navigator, session), 0, 2);
        Content = _layout;
    }

    protected void SetBody(View body, bool scroll = true)
    {
        View container = scroll ? new ScrollView
        {
            Content = body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Never
        } : body;
        container.Margin = new Thickness(18, 2, 18, 12);
        if (_body is not null) _layout.Remove(_body);
        _layout.Add(container, 0, 1);
        _body = container;
    }

    private static View CreateNavigation(AppSection section, IAppNavigator navigator, AppSession session)
    {
        var items = session.IsEmployee
            ? new[] { (AppSection.AdminDashboard, "Painel", "home"),
                (AppSection.Users, "Usuários", "users"),
                (AppSection.AdminWorkouts, "Treinos", "workout"),
                (AppSection.Profile, "Perfil", "profile") }
            : new[] { (AppSection.PersonalWorkouts, "Treinos", "workout"),
                (AppSection.Progress, "Evolução", "progress"),
                (AppSection.Profile, "Perfil", "profile") };

        var grid = new Grid { ColumnSpacing = 0 };
        for (var index = 0; index < items.Length; index++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var (route, label, icon) = items[index];
            var selected = section == route ||
                section == AppSection.Templates && route == AppSection.PersonalWorkouts ||
                session.IsEmployee && section is AppSection.Statistics or AppSection.Exercises && route == AppSection.AdminDashboard;
            var color = selected ? Ui.Accent : Ui.Muted;
            var caption = Ui.Text(label, 10, color, selected ? FontAttributes.Bold : FontAttributes.None);
            caption.HorizontalTextAlignment = TextAlignment.Center;
            var content = new VerticalStackLayout
            {
                Spacing = 3,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center,
                InputTransparent = true,
                Children = { CreateIcon(icon, color), caption }
            };
            var tile = new Grid { HeightRequest = 58 };
            tile.Add(content);
            if (selected)
                tile.Add(new BoxView
                {
                    Color = Ui.Accent, HeightRequest = 2, WidthRequest = 44,
                    VerticalOptions = LayoutOptions.End, HorizontalOptions = LayoutOptions.Center
                });
            var button = new Button
            {
                Text = string.Empty,
                BackgroundColor = Colors.Transparent,
                BorderWidth = 0,
                CornerRadius = 0,
                Padding = 0,
                MinimumHeightRequest = 58,
                AutomationId = $"Navigate{route}"
            };
            SemanticProperties.SetDescription(button, label + (selected ? ", selecionado" : ""));
            button.Clicked += async (_, _) =>
            {
                if (!button.IsEnabled || section == route) return;
                button.IsEnabled = false;
                try { await navigator.NavigateAsync(route); }
                catch { await Ui.ShowErrorAsync("Navegação indisponível", "Confira seu acesso e tente novamente."); }
                finally { button.IsEnabled = true; }
            };
            tile.Add(button);
            grid.Add(tile, index);
        }

        return new Border
        {
            Padding = new Thickness(4, 3, 4, 4),
            BackgroundColor = Ui.Secondary,
            Stroke = new SolidColorBrush(Ui.Border),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 0 },
            Content = grid
        };
    }

    private static View CreateIcon(string name, Color color)
    {
        var data = name switch
        {
            "home" => "M 3,11 L 12,3 L 21,11 M 5,10 L 5,21 L 10,21 L 10,15 L 14,15 L 14,21 L 19,21 L 19,10",
            "workout" => "M 8,12 L 16,12 M 4,7 L 8,7 L 8,17 L 4,17 Z M 16,7 L 20,7 L 20,17 L 16,17 Z M 2,10 L 4,10 M 20,10 L 22,10 M 2,14 L 4,14 M 20,14 L 22,14",
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
