using System.Globalization;
using GymTrack.Data.Dao;
using GymTrack.Models;
using GymTrack.Services;
using GymTrack.Views.Components;
using Microsoft.Maui.Controls.Shapes;

namespace GymTrack.Views.Consumer;

public sealed class PersonalWorkoutsPage : WorkspacePage
{
    private readonly IAppNavigator _navigator;
    private readonly AppSession _session;
    private readonly IWorkoutCatalogService _catalog;
    private readonly IWorkoutDao _workouts;
    private readonly IExerciseDao _exercises;
    private readonly ISessionDao _sessions;
    private readonly VerticalStackLayout _content = new() { Spacing = 20 };
    private readonly Label _status = ConsumerUi.Status();
    private readonly IDispatcherTimer _elapsedTimer;
    private Label? _elapsedLabel;
    private DateTime? _activeStartedAt;
    private bool _loading;
    private bool _busy;

    public PersonalWorkoutsPage(IAppNavigator navigator, AppSession session,
        IWorkoutCatalogService catalog, IWorkoutDao workouts, IExerciseDao exercises, ISessionDao sessions)
        : base("Meus treinos", "Pronto para superar seus limites?",
            AppSection.PersonalWorkouts, navigator, session)
    {
        _navigator = navigator;
        _session = session;
        _catalog = catalog;
        _workouts = workouts;
        _exercises = exercises;
        _sessions = sessions;
        _elapsedTimer = Dispatcher.CreateTimer();
        _elapsedTimer.Interval = TimeSpan.FromSeconds(30);
        _elapsedTimer.Tick += (_, _) => UpdateElapsed();
        SetBody(new VerticalStackLayout { Spacing = 8, Children = { _status, _content } });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
        _elapsedTimer.Start();
    }

    protected override void OnDisappearing()
    {
        _elapsedTimer.Stop();
        base.OnDisappearing();
    }

    private async Task LoadAsync()
    {
        if (_loading) return;
        _loading = true;
        try
        {
            if (_session.CurrentUser is not { } user) return;
            _status.Text = string.Empty;
            var workouts = await _catalog.GetPersonalAsync(user.Id);
            var ownIds = workouts.Select(item => item.Id).ToHashSet();
            var completed = (await _sessions.GetSummariesAsync())
                .Where(item => item.FinishedAt is not null)
                .OrderByDescending(item => item.StartedAt).ToList();
            var active = await _sessions.GetActiveAsync();
            if (active is not null && !ownIds.Contains(active.WorkoutId)) active = null;

            var names = (await _exercises.GetAllAsync(includeInactive: true))
                .ToDictionary(item => item.Id, item => item.Name);
            var previews = new Dictionary<int, IReadOnlyList<string>>();
            foreach (var workout in workouts)
            {
                var plans = await _workouts.GetExercisesAsync(workout.Id);
                previews[workout.Id] = plans.Take(3)
                    .Select(plan => names.GetValueOrDefault(plan.ExerciseId, "Exercício indisponível"))
                    .ToArray();
            }

            _content.Clear();
            _activeStartedAt = active?.StartedAt;
            _elapsedLabel = null;
            AddSummary(completed);
            if (active is not null)
                AddActiveSession(active, workouts.First(item => item.Id == active.WorkoutId));
            AddWorkoutList(workouts, previews, active);
        }
        catch
        {
            _status.Text = "Não foi possível carregar seus treinos. Volte a esta página para tentar novamente.";
        }
        finally { _loading = false; }
    }

    private void AddSummary(IReadOnlyList<SessionSummary> completed)
    {
        var monday = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));
        var weekly = completed.Count(item => ToLocal(item.StartedAt).Date >= monday);
        var minutes = (int)Math.Round(completed.Sum(item =>
            Math.Max(0, (item.FinishedAt!.Value - item.StartedAt).TotalMinutes)));
        var time = minutes >= 60 ? $"{minutes / 60}h {minutes % 60:00}min" : $"{minutes} min";
        var last = completed.FirstOrDefault()?.StartedAt;
        var lastText = last is DateTime date
            ? ToLocal(date).ToString("dd MMM", CultureInfo.GetCultureInfo("pt-BR")) : "—";
        var grid = new Grid
        {
            ColumnSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            }
        };
        grid.Add(SummaryMetric(weekly.ToString(CultureInfo.CurrentCulture), "Na semana"), 0);
        grid.Add(SummaryMetric(time, "Tempo total"), 1);
        grid.Add(SummaryMetric(lastText, "Último treino"), 2);
        _content.Add(Ui.Card(grid, new Thickness(12, 14)));
    }

    private static View SummaryMetric(string value, string caption)
    {
        var column = new VerticalStackLayout { Spacing = 4 };
        column.Add(Ui.Text(value, 17, Ui.Foreground, FontAttributes.Bold));
        column.Add(Ui.Text(caption, 11, Ui.Muted));
        return column;
    }

    private void AddActiveSession(WorkoutSession active, WorkoutCatalogItem workout)
    {
        var content = new VerticalStackLayout { Spacing = 8 };
        content.Add(Ui.Text("TREINO EM ANDAMENTO", 11, Ui.Accent, FontAttributes.Bold));
        var details = new Grid
        {
            ColumnSpacing = 8,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
        };
        details.Add(Ui.Text(workout.Name, 18, Ui.Foreground, FontAttributes.Bold));
        _elapsedLabel = Ui.Text(string.Empty, 12, Ui.Muted);
        _elapsedLabel.VerticalOptions = LayoutOptions.Center;
        details.Add(_elapsedLabel, 1);
        content.Add(details);
        content.Add(Ui.Button("Continuar treino", () => RunAsync(() => _navigator.StartWorkoutAsync(active.WorkoutId))));
        var card = Ui.Card(content);
        card.BackgroundColor = Ui.Elevated;
        _content.Add(card);
        UpdateElapsed();
    }

    private void UpdateElapsed()
    {
        if (_activeStartedAt is not { } started || _elapsedLabel is null) return;
        var utcStart = started.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(started, DateTimeKind.Utc) : started.ToUniversalTime();
        var minutes = Math.Max(0, (int)(DateTime.UtcNow - utcStart).TotalMinutes);
        _elapsedLabel.Text = minutes >= 60 ? $"{minutes / 60}h {minutes % 60:00}min" : $"{minutes} min";
    }

    private void AddWorkoutList(IReadOnlyList<WorkoutCatalogItem> workouts,
        IReadOnlyDictionary<int, IReadOnlyList<string>> previews, WorkoutSession? active)
    {
        var heading = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
        };
        var title = Ui.Text("Minhas rotinas", 18, Ui.Foreground, FontAttributes.Bold);
        title.VerticalOptions = LayoutOptions.Center;
        heading.Add(title);
        var create = Ui.Button("+", () => RunAsync(() => _navigator.OpenWorkoutEditorAsync(null)));
        create.WidthRequest = 42;
        create.MinimumHeightRequest = 42;
        create.FontSize = 21;
        create.Padding = 0;
        SemanticProperties.SetDescription(create, "Criar treino");
        heading.Add(create, 1);
        _content.Add(heading);

        if (workouts.Count == 0)
        {
            AddEmptyState();
            return;
        }
        var list = new VerticalStackLayout { Spacing = 10 };
        foreach (var workout in workouts)
            list.Add(WorkoutCard(workout, previews[workout.Id], active));
        _content.Add(list);
        var explore = Ui.Button("Explorar treinos prontos  ›",
            () => RunAsync(() => _navigator.NavigateAsync(AppSection.Templates)), true);
        explore.MinimumHeightRequest = 42;
        _content.Add(explore);
        var history = Ui.Button("Ver histórico", () => RunAsync(() => _navigator.OpenHistoryAsync()), true);
        history.MinimumHeightRequest = 42;
        _content.Add(history);
    }

    private void AddEmptyState()
    {
        var content = new VerticalStackLayout { Spacing = 11 };
        var icon = new Microsoft.Maui.Controls.Shapes.Path
        {
            Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString(
                "M 8,12 L 16,12 M 4,7 L 8,7 L 8,17 L 4,17 Z M 16,7 L 20,7 L 20,17 L 16,17 Z M 2,10 L 4,10 M 20,10 L 22,10 M 2,14 L 4,14 M 20,14 L 22,14")!,
            Stroke = new SolidColorBrush(Ui.Accent), StrokeThickness = 1.8,
            StrokeLineCap = PenLineCap.Round, WidthRequest = 30, HeightRequest = 30,
            Aspect = Stretch.Uniform, HorizontalOptions = LayoutOptions.Center
        };
        content.Add(icon);
        var title = Ui.Text("Seu primeiro treino começa aqui", 18, Ui.Foreground, FontAttributes.Bold);
        title.HorizontalTextAlignment = TextAlignment.Center;
        content.Add(title);
        var description = Ui.Text("Crie sua rotina ou escolha um treino pronto para começar.", 13, Ui.Muted);
        description.HorizontalTextAlignment = TextAlignment.Center;
        content.Add(description);
        content.Add(Ui.Button("Criar meu treino", () => RunAsync(() => _navigator.OpenWorkoutEditorAsync(null))));
        content.Add(Ui.Button("Explorar treinos prontos",
            () => RunAsync(() => _navigator.NavigateAsync(AppSection.Templates)), true));
        _content.Add(Ui.Card(content, new Thickness(18)));
    }

    private Border WorkoutCard(WorkoutCatalogItem workout, IReadOnlyList<string> preview, WorkoutSession? active)
    {
        var content = new VerticalStackLayout { Spacing = 9 };
        var top = new Grid
        {
            ColumnSpacing = 8,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
        };
        var identity = new VerticalStackLayout { Spacing = 3 };
        identity.Add(Ui.Text(workout.Name, 17, Ui.Foreground, FontAttributes.Bold));
        identity.Add(Ui.Text(ConsumerUi.ExerciseCount(workout.ExerciseCount), 12, Ui.Muted));
        top.Add(identity);
        var menu = Ui.Button("⋯", () => ShowOptionsAsync(workout), true);
        menu.WidthRequest = 42;
        menu.MinimumHeightRequest = 42;
        menu.Padding = 0;
        menu.FontSize = 20;
        SemanticProperties.SetDescription(menu, "Opções de " + workout.Name);
        top.Add(menu, 1);
        content.Add(top);
        if (!string.IsNullOrWhiteSpace(workout.Description))
            content.Add(Ui.Text(workout.Description, 12, Ui.Muted));
        if (preview.Count > 0)
        {
            content.Add(new BoxView { HeightRequest = 1, Color = Ui.Border });
            foreach (var exercise in preview)
                content.Add(Ui.Text("•  " + exercise, 12, Ui.Muted));
            if (workout.ExerciseCount > preview.Count)
                content.Add(Ui.Text($"+{workout.ExerciseCount - preview.Count} exercícios", 11, Ui.Muted));
        }
        var isActive = active?.WorkoutId == workout.Id;
        var start = Ui.Button(isActive ? "Continuar treino" : "Iniciar treino",
            () => RunAsync(() => _navigator.StartWorkoutAsync(workout.Id)));
        start.MinimumHeightRequest = 44;
        start.IsEnabled = workout.ExerciseCount > 0 && (active is null || isActive);
        content.Add(start);
        if (workout.ExerciseCount == 0)
            content.Add(Ui.Text("Adicione exercícios para iniciar.", 12, Ui.Muted));
        else if (active is not null && !isActive)
            content.Add(Ui.Text("Conclua o treino em andamento para iniciar outro.", 12, Ui.Muted));
        return Ui.Card(content);
    }

    private async Task ShowOptionsAsync(WorkoutCatalogItem workout)
    {
        var choice = await DisplayActionSheetAsync(workout.Name, "Cancelar", null,
            "Editar", "Duplicar", "Excluir");
        switch (choice)
        {
            case "Editar": await RunAsync(() => _navigator.OpenWorkoutEditorAsync(workout.Id)); break;
            case "Duplicar": await DuplicateAsync(workout.Id); break;
            case "Excluir": await DeleteAsync(workout.Id); break;
        }
    }

    private async Task DuplicateAsync(int id)
    {
        if (_session.CurrentUser is not { } user) return;
        await RunAsync(async () =>
        {
            await _catalog.DuplicatePersonalAsync(id, user.Id);
            await LoadAsync();
            _status.Text = "Treino duplicado.";
        });
    }

    private async Task DeleteAsync(int id)
    {
        if (_busy || _session.CurrentUser is not { } user) return;
        if (!await DisplayAlertAsync("Excluir treino?",
                "Esta rotina sairá da lista. Seu histórico de sessões será preservado.",
                "Excluir", "Cancelar")) return;
        await RunAsync(async () =>
        {
            await _catalog.DeletePersonalAsync(id, user.Id);
            await LoadAsync();
            _status.Text = "Treino excluído.";
        });
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        try { _status.Text = string.Empty; await action(); }
        catch (InvalidOperationException exception) { _status.Text = exception.Message; }
        catch { _status.Text = "Não foi possível concluir esta ação. Tente novamente."; }
        finally { _busy = false; }
    }

    private static DateTime ToLocal(DateTime value) =>
        (value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value).ToLocalTime();
}
