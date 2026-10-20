using GymTrack.Data.Dao;
using GymTrack.Models;
using GymTrack.Services;
using GymTrack.Views.Components;
using Microsoft.Maui.Controls.Shapes;

namespace GymTrack.Views.Consumer;

public sealed class TemplateDetailPage : ContentPage
{
    private readonly IWorkoutDao _workouts;
    private readonly IExerciseDao _exercises;
    private readonly IWorkoutCatalogService _catalog;
    private readonly IAppNavigator _navigator;
    private readonly AppSession _session;
    private readonly VerticalStackLayout _content = new() { Spacing = 10 };
    private readonly Label _status = Ui.Text(string.Empty, 13, Ui.Accent);
    private readonly Button _choose;
    private int _templateId;
    private string? _starterKey;
    private int? _copiedWorkoutId;
    private bool _busy;

    public TemplateDetailPage(
        IWorkoutDao workouts,
        IExerciseDao exercises,
        IWorkoutCatalogService catalog,
        IAppNavigator navigator,
        AppSession session)
    {
        _workouts = workouts;
        _exercises = exercises;
        _catalog = catalog;
        _navigator = navigator;
        _session = session;

        Title = "Treino pronto";
        BackgroundColor = Ui.Background;
        NavigationPage.SetHasNavigationBar(this, false);

        var back = Ui.Button("‹  Voltar", async () => { await Navigation.PopAsync(); }, true);
        back.HorizontalOptions = LayoutOptions.Start;
        back.MinimumWidthRequest = 105;
        back.Margin = new Thickness(20, 16, 20, 8);

        var scroll = new ScrollView
        {
            Content = _content,
            Margin = new Thickness(20, 4, 20, 0)
        };

        _choose = Ui.Button("Adicionar aos meus treinos", ChooseAsync);
        _choose.IsEnabled = false;

        var footer = new VerticalStackLayout
        {
            Padding = new Thickness(20, 12, 20, 20),
            Spacing = 8,
            BackgroundColor = Ui.Background,
            Children = { _status, _choose }
        };

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            }
        };
        root.Add(back, 0, 0);
        root.Add(scroll, 0, 1);
        root.Add(footer, 0, 2);
        Content = root;
    }

    public async Task InitializeAsync(int templateId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(templateId);

        _templateId = templateId;
        _starterKey = null;
        _copiedWorkoutId = null;
        _status.Text = string.Empty;
        _choose.IsEnabled = false;
        _choose.Text = "Adicionar aos meus treinos";
        _content.Clear();
        _content.Add(Ui.Text("Carregando treino...", 14, Ui.Muted));

        var workout = await _workouts.GetByIdAsync(templateId);
        if (workout is null || !workout.IsTemplate || workout.IsArchived)
            throw new InvalidOperationException("Treino pronto não encontrado.");

        var plans = await _workouts.GetExercisesAsync(templateId);
        var exercises = (await _exercises.GetAllAsync(includeInactive: true))
            .ToDictionary(exercise => exercise.Id);

        _content.Clear();
        _content.Add(Ui.Tag("TREINO PRONTO"));
        _content.Add(Ui.Text(workout.Name, 30, Ui.Foreground, FontAttributes.Bold));
        if (!string.IsNullOrWhiteSpace(workout.Description))
            _content.Add(Ui.Text(workout.Description, 14, Ui.Muted));
        _content.Add(Ui.Text(ConsumerUi.ExerciseCount(plans.Count), 13, Ui.Muted));
        _content.Add(new Grid { HeightRequest = 6 });
        _content.Add(Ui.Text("Exercícios", 17, Ui.Foreground, FontAttributes.Bold));

        foreach (var plan in plans)
        {
            if (exercises.TryGetValue(plan.ExerciseId, out var exercise))
                _content.Add(ExerciseCard(exercise.Name, exercise.MuscleGroup,
                    plan.PlannedSets, plan.PlannedReps, exercise.Id));
        }

        if (plans.Count == 0)
            _content.Add(ConsumerUi.Empty("Treino em preparação", "A equipe ainda não adicionou exercícios a esta rotina."));

        _content.Add(new Grid { HeightRequest = 20 });
        _choose.IsEnabled = plans.Count > 0;
    }

    public async Task InitializeStarterAsync(StarterWorkoutPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        _templateId = 0;
        _starterKey = plan.Key;
        _copiedWorkoutId = null;
        _status.Text = string.Empty;
        _choose.IsEnabled = false;
        _choose.Text = "Usar e personalizar";

        var exercises = await _exercises.GetAllAsync(includeInactive: true);
        _content.Clear();
        _content.Add(Ui.Tag("MODELO DE TREINO"));
        _content.Add(Ui.Text(plan.Name, 30, Ui.Foreground, FontAttributes.Bold));
        if (!string.IsNullOrWhiteSpace(plan.Description))
            _content.Add(Ui.Text(plan.Description, 14, Ui.Muted));
        _content.Add(Ui.Text(ConsumerUi.ExerciseCount(plan.Exercises.Count), 13, Ui.Muted));
        _content.Add(new Grid { HeightRequest = 6 });
        _content.Add(Ui.Text("Exercícios", 17, Ui.Foreground, FontAttributes.Bold));

        foreach (var item in plan.Exercises)
        {
            var exerciseId = exercises.FirstOrDefault(exercise =>
                exercise.IsActive &&
                string.Equals(exercise.Name, item.Name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(exercise.MuscleGroup, item.MuscleGroup, StringComparison.OrdinalIgnoreCase))?.Id;
            _content.Add(ExerciseCard(item.Name, item.MuscleGroup,
                item.Sets, item.Reps, exerciseId));
        }

        _content.Add(new Grid { HeightRequest = 20 });
        _choose.IsEnabled = plan.Exercises.Count > 0;
    }

    private Border ExerciseCard(string name, string muscleGroup, int sets, int reps,
        int? exerciseId)
    {
        var media = ExerciseMediaCatalog.Get(name);
        View thumb = media.Image is { } image
            ? new Image { Source = image, Aspect = Aspect.AspectFit,
                WidthRequest = 86, HeightRequest = 68, BackgroundColor = Colors.White }
            : new Label
            {
                Text = "SEM\nMÍDIA",
                TextColor = Ui.Muted,
                FontSize = 10,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                WidthRequest = 86,
                HeightRequest = 68
            };
        var thumbnail = new Border
        {
            Content = thumb,
            WidthRequest = 86,
            HeightRequest = 68,
            Padding = 0,
            BackgroundColor = Color.FromArgb("#151619"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 9 }
        };

        var title = Ui.Text(name, 16, Ui.Foreground, FontAttributes.Bold);
        title.MaxLines = 2;
        var info = new VerticalStackLayout { Spacing = 3 };
        info.Add(title);
        info.Add(Ui.Text(muscleGroup, 12, Color.FromArgb("#FF8A1F")));
        info.Add(Ui.Text($"{sets} × {reps}", 12, Ui.Muted));
        if (exerciseId is > 0)
        {
            var preview = Ui.Button("Ver execução  →",
                () => _navigator.OpenExerciseDemoAsync(exerciseId.Value), true);
            preview.TextColor = Color.FromArgb("#FF8A1F");
            preview.BackgroundColor = Colors.Transparent;
            preview.BorderWidth = 0;
            preview.MinimumHeightRequest = 36;
            preview.Padding = new Thickness(0, 2);
            preview.HorizontalOptions = LayoutOptions.Start;
            info.Add(preview);
        }
        else
        {
            info.Add(Ui.Text("Guia indisponível", 11, Ui.Muted));
        }

        var row = new Grid { ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(86) },
            new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 12 };
        row.Add(thumbnail, 0, 0);
        row.Add(info, 1, 0);
        return new Border
        {
            Content = row,
            BackgroundColor = Color.FromArgb("#202126"),
            Stroke = new SolidColorBrush(Color.FromArgb("#34363B")),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Padding = 12
        };
    }

    private async Task ChooseAsync()
    {
        if (_busy || _session.CurrentUser is not { Role: UserRole.Consumer, IsActive: true } user)
            return;

        _busy = true;
        _status.Text = string.Empty;
        try
        {
            _copiedWorkoutId ??= _starterKey is { } key
                ? await _catalog.CreateFromStarterAsync(key, user.Id)
                : await _catalog.CopyTemplateAsync(_templateId, user.Id);
            _choose.Text = "Abrir meu treino";
            await _navigator.OpenWorkoutEditorAsync(_copiedWorkoutId.Value);
        }
        catch (Exception)
        {
            _status.Text = _copiedWorkoutId is null
                ? "Não foi possível adicionar este treino. Tente novamente."
                : "Treino adicionado. Toque para abrir sua cópia.";
        }
        finally
        {
            _busy = false;
        }
    }
}
