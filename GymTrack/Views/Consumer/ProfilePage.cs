using System.Globalization;
using GymTrack.Data.Dao;
using GymTrack.Models;
using GymTrack.Services;
using GymTrack.Views.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;

namespace GymTrack.Views.Consumer;

public sealed class ProfilePage : WorkspacePage
{
    private static readonly CultureInfo Portuguese = CultureInfo.GetCultureInfo("pt-BR");

    private readonly IAppNavigator _navigator;
    private readonly AppSession _session;
    private readonly IAccountService _accounts;
    private readonly IBodyMeasurementService _measurements;
    private readonly ISessionDao _sessions;
    private readonly IManualWorkoutLogService _manualLogs;
    private readonly IServiceProvider _services;
    private readonly Label _status = ConsumerUi.Status();
    private readonly ContentView _identity = new();
    private readonly ContentView _chart = new();
    private readonly ContentView _workouts = new();
    private readonly Dictionary<TrainingMetric, Button> _metricButtons = [];
    private IReadOnlyList<SessionSummary> _completedSessions = [];
    private IReadOnlyList<ManualWorkoutLog> _manualWorkouts = [];
    private Task _trainingLoad = Task.CompletedTask;
    private TrainingMetric _selectedMetric;
    private bool _busy;

    public ProfilePage(IAppNavigator navigator, AppSession session, IAccountService accounts,
        IBodyMeasurementService measurements, ISessionDao sessions,
        IManualWorkoutLogService manualLogs, IServiceProvider services)
        : base("Meu perfil", AppSection.Profile, navigator, session)
    {
        _navigator = navigator;
        _session = session;
        _accounts = accounts;
        _measurements = measurements;
        _sessions = sessions;
        _manualLogs = manualLogs;
        _services = services;

        var content = new VerticalStackLayout { Spacing = 17 };
        content.Add(CreateHeader());
        content.Add(_status);
        content.Add(_identity);
        if (_session.IsPersonalMode)
        {
            content.Add(_chart);
            content.Add(CreateMetricSelector());
        }

        content.Add(new VerticalStackLayout
        {
            Spacing = 11,
            Children = { Heading("Painel"), CreatePanel() }
        });

        if (_session.IsPersonalMode)
        {
            content.Add(new VerticalStackLayout
            {
                Spacing = 11,
                Children = { Heading("Treinos"), _workouts }
            });
            RenderTraining();
        }

        RenderIdentity();
        SetBody(content);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (AppDialogs.IsShowingFor(this)) return;
        if (!_session.IsPersonalMode || _session.CurrentUser is not { } user) return;
        _trainingLoad = LoadTrainingAsync(user.Id);
    }

    private async Task LoadTrainingAsync(int userId)
    {
        try
        {
            var sessions = await _sessions.GetSummariesAsync();
            var manual = await _manualLogs.GetForUserAsync(userId);
            if (!_session.IsPersonalMode || _session.CurrentUser?.Id != userId) return;

            _completedSessions = sessions.Where(item => item.FinishedAt.HasValue).ToArray();
            _manualWorkouts = manual.Where(item => !item.IsExample).ToArray();
            RenderIdentity();
            RenderTraining();
        }
        catch
        {
            _status.Text = "Não foi possível carregar seu resumo de treinos.";
        }
    }

    private View CreateHeader()
    {
        var title = Heading("Meu perfil", 26, SemanticHeadingLevel.Level1);
        title.VerticalTextAlignment = TextAlignment.Center;

        var actions = new HorizontalStackLayout
        {
            Spacing = 2,
            Children =
            {
                IconAction("Editar perfil", "edit", EditProfileAsync),
                IconAction("Configurações", "settings", OpenSettingsAsync)
            }
        };

        var header = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 8
        };
        header.Add(title);
        header.Add(Surface(actions, new Thickness(4, 1), 26), 1);
        return header;
    }

    private void RenderIdentity()
    {
        if (_session.CurrentUser is not { } user) return;

        var nameParts = user.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var initials = nameParts.Length switch
        {
            0 => "?",
            1 => nameParts[0][..1],
            _ => string.Concat(nameParts[0][0], nameParts[^1][0])
        };
        var avatarText = Ui.Text(initials.ToUpper(Portuguese), 25, Ui.Accent, FontAttributes.Bold);
        avatarText.HorizontalTextAlignment = TextAlignment.Center;
        avatarText.VerticalTextAlignment = TextAlignment.Center;
        var avatar = Surface(avatarText, new Thickness(0), 39, Ui.Elevated);
        avatar.WidthRequest = 78;
        avatar.HeightRequest = 78;
        avatar.VerticalOptions = LayoutOptions.Center;
        AutomationProperties.SetIsInAccessibleTree(avatarText, false);

        var details = new VerticalStackLayout { Spacing = 7, VerticalOptions = LayoutOptions.Center };
        details.Add(Heading(user.Name, 20));
        if (_session.IsPersonalMode)
        {
            var dates = TrainingDates().ToArray();
            var today = DateTime.Today;
            var monthCount = dates.Count(date => date.Year == today.Year && date.Month == today.Month);
            var metrics = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star)
                },
                ColumnSpacing = 5
            };
            metrics.Add(IdentityMetric("Treinos", dates.Length.ToString(Portuguese)), 0);
            metrics.Add(IdentityMetric("Este mês", monthCount.ToString(Portuguese)), 1);
            metrics.Add(IdentityMetric("Último", dates.Length == 0 ? "—" : dates.Max().ToString("dd/MM", Portuguese)), 2);
            details.Add(metrics);
        }
        else
        {
            details.Add(Ui.Text("Funcionário", 13, Ui.Muted));
        }

        var row = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 15
        };
        row.Add(avatar);
        row.Add(details, 1);
        _identity.Content = row;
    }

    private static View IdentityMetric(string label, string value) => new VerticalStackLayout
    {
        Spacing = 2,
        Children = { Ui.Text(label, 11, Ui.Muted), Ui.Text(value, 15, Ui.Foreground, FontAttributes.Bold) }
    };

    private View CreateMetricSelector()
    {
        var selector = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 6
        };

        AddMetric(TrainingMetric.Duration, "Duração", 0);
        AddMetric(TrainingMetric.Volume, "Volume", 1);
        AddMetric(TrainingMetric.Repetitions, "Repetições", 2);
        UpdateMetricButtons();
        return selector;

        void AddMetric(TrainingMetric metric, string text, int column)
        {
            var button = new Button { Text = text, FontSize = 12, Padding = new Thickness(4, 0), CornerRadius = 22 };
            button.SetDynamicResource(VisualElement.StyleProperty, "SecondaryButton");
            button.Clicked += (_, _) =>
            {
                _selectedMetric = metric;
                UpdateMetricButtons();
                RenderChart();
            };
            _metricButtons.Add(metric, button);
            selector.Add(button, column);
        }
    }

    private void UpdateMetricButtons()
    {
        foreach (var (metric, button) in _metricButtons)
        {
            var selected = metric == _selectedMetric;
            button.BackgroundColor = selected ? Ui.Accent : Ui.Surface;
            button.TextColor = selected ? Ui.Background : Ui.Foreground;
            SemanticProperties.SetDescription(button, selected ? $"{button.Text}, selecionado" : button.Text);
        }
    }

    private View CreatePanel()
    {
        var panel = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            RowDefinitions = { new RowDefinition(GridLength.Auto) },
            RowSpacing = 8,
            ColumnSpacing = 8
        };

        if (_session.IsPersonalMode)
        {
            panel.Add(PanelAction("Medidas", "measurements", () =>
                Navigation.PushAsync(new BodyMeasurementsPage(_session, _measurements))), 0);
            panel.Add(PanelAction("Calendário", "calendar", OpenCalendarAsync), 1);
        }
        else
        {
            panel.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            panel.Add(PanelAction("Estatísticas", "statistics", () =>
                _navigator.NavigateAsync(AppSection.Statistics)), 0, 0);
            panel.Add(PanelAction("Exercícios", "workout", () =>
                _navigator.NavigateAsync(AppSection.Exercises)), 1, 0);
            panel.Add(PanelAction("Gerenciar contas", "profile", () =>
                _navigator.NavigateAsync(AppSection.Users)), 0, 1);
            panel.Add(PanelAction("Área pessoal", "switch", _navigator.SwitchWorkspaceAsync), 1, 1);
        }

        return panel;
    }

    private IEnumerable<DateTime> TrainingDates() =>
        _completedSessions.Select(item => item.StartedAt.ToLocalTime().Date)
            .Concat(_manualWorkouts.Select(item => item.PerformedOn.Date));

    private void RenderTraining()
    {
        RenderChart();
        var latest = _completedSessions.Select(item => (item.WorkoutName, Date: item.StartedAt.ToLocalTime().Date))
            .Concat(_manualWorkouts.Select(item => (item.WorkoutName, Date: item.PerformedOn.Date)))
            .DefaultIfEmpty()
            .MaxBy(item => item.Date);

        var workouts = new VerticalStackLayout { Spacing = 10 };
        if (latest.Date == default)
        {
            workouts.Add(EmptyCard("Ainda não há treinos registrados", "workout", 100));
        }
        else
        {
            var summary = new VerticalStackLayout { Spacing = 4 };
            summary.Add(Ui.Text("Último treino", 12, Ui.Muted));
            summary.Add(Ui.Text(latest.WorkoutName ?? string.Empty, 16, Ui.Foreground, FontAttributes.Bold));
            summary.Add(Ui.Text(latest.Date.ToString("dd/MM/yyyy", Portuguese), 12, Ui.Muted));
            workouts.Add(Surface(summary, new Thickness(14)));
        }

        var open = Ui.Button("Ver treinos", () => _navigator.NavigateAsync(AppSection.PersonalWorkouts), true);
        open.CornerRadius = 24;
        workouts.Add(open);
        _workouts.Content = workouts;
    }

    private void RenderChart()
    {
        var firstDay = DateTime.Today.AddDays(-6);
        var dailyTotals = _completedSessions
            .GroupBy(item => item.StartedAt.ToLocalTime().Date)
            .ToDictionary(group => group.Key, group => group.Sum(MetricValue));
        var points = Enumerable.Range(0, 7)
            .Select(index => firstDay.AddDays(index))
            .Select(date => (Date: date, Value: dailyTotals.GetValueOrDefault(date)))
            .ToArray();
        var maximum = points.Max(point => point.Value);
        if (maximum <= 0)
        {
            _chart.Content = EmptyCard("Ainda sem dados", "chart", 150);
            return;
        }

        var unit = _selectedMetric switch
        {
            TrainingMetric.Duration => "min",
            TrainingMetric.Volume => "kg",
            _ => "reps"
        };
        var bars = new Grid { ColumnSpacing = 6 };
        for (var index = 0; index < points.Length; index++)
        {
            var (day, value) = points[index];
            var track = new Grid { HeightRequest = 68 };
            track.Add(new Border
            {
                HeightRequest = value > 0 ? Math.Max(3, 68 * value / maximum) : 1,
                BackgroundColor = value > 0 ? Ui.Accent : Ui.Border,
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 4 },
                Margin = new Thickness(4, 0),
                VerticalOptions = LayoutOptions.End
            });
            var label = Ui.Text(day.ToString("dd/MM", Portuguese), 10, Ui.Muted);
            label.HorizontalTextAlignment = TextAlignment.Center;
            var column = new VerticalStackLayout { Spacing = 5, Children = { track, label } };
            SemanticProperties.SetDescription(column, $"{day:dd/MM}, {value.ToString("0.#", Portuguese)} {unit}");
            bars.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            bars.Add(column, index);
        }

        var total = $"{points.Sum(point => point.Value).ToString("0.#", Portuguese)} {unit}";
        var chart = new VerticalStackLayout
        {
            Spacing = 7,
            Children =
            {
                Ui.Text("Últimos 7 dias", 11, Ui.Muted),
                Ui.Text(total, 20, Ui.Foreground, FontAttributes.Bold),
                bars
            }
        };
        _chart.Content = Surface(chart, new Thickness(14, 10));
    }

    private double MetricValue(SessionSummary session) => _selectedMetric switch
    {
        TrainingMetric.Duration => Math.Max(0, (session.FinishedAt!.Value - session.StartedAt).TotalMinutes),
        TrainingMetric.Volume => session.TotalVolume,
        _ => session.TotalRepetitions
    };

    private async Task OpenCalendarAsync()
    {
        await _trainingLoad;
        var page = new ContentPage { Title = "Calendário", BackgroundColor = Ui.Background };
        var calendar = new WorkoutCalendar(() => _ = page.Navigation.PopAsync());
        calendar.SetWorkouts(_completedSessions, _manualWorkouts);
        page.Content = new ScrollView
        {
            Padding = new Thickness(18, 14, 18, 16),
            Content = calendar
        };
        await Navigation.PushAsync(page);
    }

    private Task OpenSettingsAsync()
    {
        if (_session.CurrentUser is not { } user) return Task.CompletedTask;
        var page = new ProfileSettingsPage();
        var account = new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                AccountDetail("Nome", user.Name),
                AccountDetail("E-mail", user.Email),
                AccountDetail("Tipo de conta", _session.IsEmployee ? "Funcionário" : "Aluno")
            }
        };
        var settings = new VerticalStackLayout
        {
            Spacing = 10,
            Children = { Heading("Informações da conta"), Surface(account, new Thickness(15)) }
        };

        if (_session.IsEmployee)
        {
            var workspace = _session.IsManagementMode ? "Abrir área pessoal" : "Voltar à gestão";
            settings.Add(SettingsButton(workspace, _navigator.SwitchWorkspaceAsync));
        }
        if (_session.IsManagementMode)
        {
            settings.Add(SettingsButton("Gerenciar contas", () => _navigator.NavigateAsync(AppSection.Users)));
            settings.Add(SettingsButton("Estatísticas", () => _navigator.NavigateAsync(AppSection.Statistics)));
        }
        settings.Add(SettingsButton("Sobre o GymTrack", () =>
            page.Navigation.PushAsync(_services.GetRequiredService<AboutPage>())));
        var signOut = SettingsButton("Sair da conta", () => SignOutAsync(page));
        signOut.TextColor = Color.FromArgb("#FF7474");
        settings.Add(signOut);
        page.Content = new ScrollView
        {
            Padding = new Thickness(18, 16, 18, 24),
            Content = settings,
            MaximumWidthRequest = 460
        };
        return Navigation.PushAsync(page);
    }

    private static View AccountDetail(string label, string value) => new VerticalStackLayout
    {
        Spacing = 2,
        Children = { Ui.Text(label, 12, Ui.Muted), Ui.Text(value, 15) }
    };

    private static Button SettingsButton(string label, Func<Task> action)
    {
        var button = Ui.Button(label, action, true);
        button.CornerRadius = 24;
        return button;
    }

    private async Task EditProfileAsync()
    {
        if (_busy || _session.CurrentUser is not { } user) return;
        var choice = await this.ChooseAppActionAsync("Editar perfil", "Cancelar", "Alterar nome", "Alterar e-mail");
        if (choice is not ("Alterar nome" or "Alterar e-mail")) return;
        var changeName = choice == "Alterar nome";
        var value = await this.PromptAppValueAsync(choice, changeName ? "Nome" : "E-mail",
            initialValue: changeName ? user.Name : user.Email,
            maxLength: changeName ? 100 : 254,
            keyboard: changeName ? Keyboard.Text : Keyboard.Email);
        if (value is null) return;
        value = value.Trim();
        if (changeName && value.Length < 2)
        {
            _status.Text = "Informe um nome com pelo menos 2 caracteres.";
            return;
        }

        _busy = true;
        try
        {
            var updated = new AppUser
            {
                Id = user.Id, Name = changeName ? value : user.Name,
                Email = changeName ? user.Email : value, Role = user.Role,
                IsActive = user.IsActive, CreatedAt = user.CreatedAt
            };
            await _accounts.UpdateUserAsync(updated);
            _session.Refresh(updated);
            _status.Text = "Perfil atualizado.";
            RenderIdentity();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            _status.Text = exception.Message;
        }
        finally { _busy = false; }
    }

    private async Task SignOutAsync(Page owner)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            if (!await owner.ConfirmAppActionAsync("Sair da conta?", "Você poderá entrar novamente com seu e-mail e sua senha.",
                    "Sair")) return;
            await _navigator.ShowLoginAsync();
        }
        finally { _busy = false; }
    }

    private static View IconAction(string label, string icon, Func<Task> action)
    {
        var button = HitTarget(label, action);
        button.WidthRequest = 44;
        return new Grid
        {
            Children = { Icon(icon, Ui.Foreground, 21), button }
        };
    }

    private static View PanelAction(string label, string icon, Func<Task> action)
    {
        var text = Ui.Text(label, 13, Ui.Foreground, FontAttributes.Bold);
        text.VerticalTextAlignment = TextAlignment.Center;
        var content = new Grid
        {
            Padding = new Thickness(10, 6),
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 7,
            InputTransparent = true
        };
        content.Add(Icon(icon, Ui.Foreground, 21));
        content.Add(text, 1);
        AutomationProperties.SetIsInAccessibleTree(text, false);

        return Surface(new Grid
        {
            MinimumHeightRequest = 52,
            Children = { content, HitTarget(label, action) }
        }, new Thickness(0), 17);
    }

    private static Button HitTarget(string label, Func<Task> action)
    {
        var button = Ui.Button(string.Empty, action);
        button.SetDynamicResource(VisualElement.StyleProperty, "QuietButton");
        button.AutomationId = label.Replace(" ", string.Empty);
        button.CornerRadius = 24;
        SemanticProperties.SetDescription(button, label);
        return button;
    }

    private static View EmptyCard(string message, string icon, double minimumHeight)
    {
        var text = Ui.Text(message, 14, Ui.Muted);
        text.HorizontalTextAlignment = TextAlignment.Center;
        var surface = Surface(new VerticalStackLayout
        {
            Spacing = 10,
            VerticalOptions = LayoutOptions.Center,
            Children = { Icon(icon, Ui.Muted, 34), text }
        }, new Thickness(12));
        surface.MinimumHeightRequest = minimumHeight;
        return surface;
    }

    private static Border Surface(View content, Thickness padding, int radius = 18, Color? color = null)
    {
        var surface = Ui.Card(content, padding);
        surface.BackgroundColor = color ?? Ui.Surface;
        surface.StrokeShape = new RoundRectangle { CornerRadius = radius };
        return surface;
    }

    private static Label Heading(string text, int size = 18, SemanticHeadingLevel level = SemanticHeadingLevel.Level2)
    {
        var heading = Ui.Text(text, size, Ui.Foreground, FontAttributes.Bold);
        SemanticProperties.SetHeadingLevel(heading, level);
        return heading;
    }

    private static View Icon(string name, Color color, int size)
    {
        var path = name switch
        {
            "edit" => "M 4,16 L 15,5 C 16,4 17,4 18,5 L 20,7 C 21,8 21,9 20,10 L 9,21 L 4,22 Z M 13,7 L 18,12",
            "settings" => "M 10,2 L 14,2 L 15,5 L 17,6 L 20,5 L 22,9 L 20,11 L 20,13 L 22,15 L 20,19 L 17,18 L 15,19 L 14,22 L 10,22 L 9,19 L 7,18 L 4,19 L 2,15 L 4,13 L 4,11 L 2,9 L 4,5 L 7,6 L 9,5 Z M 16,12 A 4,4 0 1 1 8,12 A 4,4 0 1 1 16,12",
            "statistics" => "M 3,3 L 3,21 L 22,21 M 7,16 L 12,11 L 15,14 L 21,7 M 16,7 L 21,7 L 21,12",
            "workout" => "M 8,12 L 16,12 M 4,7 L 8,7 L 8,17 L 4,17 Z M 16,7 L 20,7 L 20,17 L 16,17 Z M 2,10 L 4,10 M 20,10 L 22,10 M 2,14 L 4,14 M 20,14 L 22,14",
            "measurements" => "M 14,4 A 2,2 0 1 1 10,4 A 2,2 0 1 1 14,4 M 9,22 L 9,13 L 5,16 L 3,14 L 9,8 L 15,8 L 21,14 L 19,16 L 15,13 L 15,22 M 12,16 L 12,22",
            "calendar" => "M 4,5 L 20,5 L 20,21 L 4,21 Z M 8,2 L 8,8 M 16,2 L 16,8 M 4,10 L 20,10 M 8,14 L 9,14 M 12,14 L 13,14 M 16,14 L 17,14 M 8,17 L 9,17 M 12,17 L 13,17",
            "profile" => "M 16,7 A 4,4 0 1 1 8,7 A 4,4 0 1 1 16,7 M 3,22 C 3,10 21,10 21,22",
            "switch" => "M 3,7 L 20,7 M 16,3 L 20,7 L 16,11 M 21,17 L 4,17 M 8,13 L 4,17 L 8,21",
            _ => "M 3,21 L 3,13 L 8,13 L 8,21 Z M 10,21 L 10,8 L 15,8 L 15,21 Z M 17,21 L 17,3 L 22,3 L 22,21 Z"
        };

        return new Microsoft.Maui.Controls.Shapes.Path
        {
            Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString(path)!,
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
    }

    private enum TrainingMetric { Duration, Volume, Repetitions }
}
