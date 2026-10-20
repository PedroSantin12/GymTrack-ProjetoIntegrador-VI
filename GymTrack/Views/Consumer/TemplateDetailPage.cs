using GymTrack.Data.Dao;
using GymTrack.Models;
using GymTrack.Services;
using GymTrack.Views.Components;

namespace GymTrack.Views.Consumer;

public sealed class TemplateDetailPage : ContentPage
{
    private readonly IWorkoutDao _workouts;
    private readonly IExerciseDao _exercises;
    private readonly IWorkoutCatalogService _catalog;
    private readonly IAppNavigator _navigator;
    private readonly AppSession _session;
    private readonly VerticalStackLayout _content = new() { Spacing = 16 };
    private readonly Label _status = Ui.Text(string.Empty, 13, Ui.Accent);
    private readonly Button _choose;
    private int _templateId;
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
        _copiedWorkoutId = null;
        _status.Text = string.Empty;
        _choose.IsEnabled = false;
        _choose.Text = "Adicionar aos meus treinos";
        _content.Clear();
        _content.Add(Ui.Text("Carregando treino...", 14, Ui.Muted));

        var workout = await _workouts.GetByIdAsync(templateId);
        if (workout is null || !workout.IsTemplate)
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

        foreach (var plan in plans)
        {
            if (exercises.TryGetValue(plan.ExerciseId, out var exercise))
                _content.Add(ExerciseCard(exercise, plan));
        }

        if (plans.Count == 0)
            _content.Add(ConsumerUi.Empty("Treino em preparação", "A equipe ainda não adicionou exercícios a esta rotina."));

        _content.Add(new Grid { HeightRequest = 20 });
        _choose.IsEnabled = plans.Count > 0;
    }

    private Border ExerciseCard(Exercise exercise, WorkoutExercise plan)
    {
        var content = new VerticalStackLayout { Spacing = 9 };
        content.Add(Ui.Tag(exercise.MuscleGroup));
        content.Add(Ui.Text(exercise.Name, 19, Ui.Foreground, FontAttributes.Bold));
        content.Add(new Image
        {
            Source = ExerciseMediaCatalog.ForExercise(exercise.Name, exercise.MuscleGroup),
            HeightRequest = 150,
            Aspect = Aspect.AspectFit,
            IsAnimationPlaying = true,
            BackgroundColor = Color.FromArgb("#191919")
        });
        content.Add(Ui.Text($"{plan.PlannedSets} séries · {plan.PlannedReps} repetições", 13, Ui.Muted));
        content.Add(Ui.Button("Ver execução", () => _navigator.OpenExerciseDemoAsync(exercise.Id), true));
        return Ui.Card(content, new Thickness(18));
    }

    private async Task ChooseAsync()
    {
        if (_busy || _session.CurrentUser is not { Role: UserRole.Consumer, IsActive: true } user)
            return;

        _busy = true;
        _status.Text = string.Empty;
        try
        {
            _copiedWorkoutId ??= await _catalog.CopyTemplateAsync(_templateId, user.Id);
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
