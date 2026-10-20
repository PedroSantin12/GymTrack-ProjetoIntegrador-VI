using GymTrack.Data.Dao;
using GymTrack.Models;
using GymTrack.Services;
using GymTrack.Views.Components;

namespace GymTrack.Views.Consumer;

public sealed class TemplateLibraryPage : WorkspacePage
{
    private readonly IAppNavigator _navigator;
    private readonly AppSession _session;
    private readonly IWorkoutCatalogService _catalog;
    private readonly IWorkoutDao _workouts;
    private readonly IExerciseDao _exercises;
    private readonly SearchBar _search;
    private readonly VerticalStackLayout _items = new() { Spacing = 10 };
    private readonly Label _status = ConsumerUi.Status();
    private int _request;
    private bool _busy;

    public TemplateLibraryPage(IAppNavigator navigator, AppSession session, IWorkoutCatalogService catalog,
        IWorkoutDao workouts, IExerciseDao exercises)
        : base("Treinos prontos", "Escolha uma base e personalize.", AppSection.Templates, navigator, session)
    {
        _navigator = navigator;
        _session = session;
        _catalog = catalog;
        _workouts = workouts;
        _exercises = exercises;
        _search = new SearchBar
        {
            Placeholder = "Pesquisar treino",
            PlaceholderColor = Ui.Muted,
            TextColor = Ui.Foreground,
            BackgroundColor = Ui.Surface,
            CancelButtonColor = Ui.Accent
        };
        _search.SearchButtonPressed += async (_, _) => await LoadAsync();
        _search.TextChanged += async (_, args) =>
        {
            if (string.IsNullOrEmpty(args.NewTextValue)) await LoadAsync();
        };

        var body = new VerticalStackLayout { Spacing = 16 };
        body.Add(_search);
        body.Add(_status);
        body.Add(_items);
        body.Add(Ui.Button("Voltar aos meus treinos",
            () => RunAsync(() => _navigator.NavigateAsync(AppSection.PersonalWorkouts)), true));
        SetBody(body);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var request = ++_request;
        try
        {
            _status.Text = "Buscando treinos...";
            var term = _search.Text?.Trim();
            var starters = _catalog.GetStarterPlans()
                .Where(item => string.IsNullOrEmpty(term) ||
                    item.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
                    item.Description.Contains(term, StringComparison.CurrentCultureIgnoreCase))
                .ToList();
            var templates = (await _catalog.GetTemplatesAsync(term))
                .Where(template => template.ExerciseCount > 0).ToList();
            var names = (await _exercises.GetAllAsync(includeInactive: true))
                .ToDictionary(item => item.Id, item => item.Name);
            if (request != _request) return;
            _items.Clear();
            if (starters.Count > 0)
            {
                _items.Add(Ui.Text("Modelos para começar", 17, Ui.Foreground, FontAttributes.Bold));
                foreach (var starter in starters) _items.Add(StarterCard(starter));
            }
            if (templates.Count > 0)
            {
                _items.Add(Ui.Text("Da equipe", 17, Ui.Foreground, FontAttributes.Bold));
                foreach (var template in templates)
                {
                    var plans = await _workouts.GetExercisesAsync(template.Id);
                    if (request != _request) return;
                    var preview = plans.Select(item =>
                        names.GetValueOrDefault(item.ExerciseId, "Exercício indisponível")).ToArray();
                    _items.Add(TemplateCard(template, preview));
                }
            }
            if (starters.Count + templates.Count == 0)
            {
                _items.Add(ConsumerUi.Empty("Nenhum treino encontrado", "Tente pesquisar por outro nome."));
            }
            _status.Text = string.Empty;
        }
        catch
        {
            if (request == _request) _status.Text = "Não foi possível buscar os treinos. Pesquise novamente para tentar.";
        }
    }

    private Border StarterCard(StarterWorkoutPlan plan) => PlanCard(
        plan.Name, plan.Description, plan.Exercises.Select(item => item.Name).ToArray(),
        async () =>
        {
            if (_session.CurrentUser is not { } user) return;
            var id = await _catalog.CreateFromStarterAsync(plan.Key, user.Id);
            await _navigator.OpenWorkoutEditorAsync(id);
        });

    private Border TemplateCard(WorkoutCatalogItem template, IReadOnlyList<string> exercises) => PlanCard(
        template.Name, template.Description, exercises,
        async () =>
        {
            if (_session.CurrentUser is not { } user) return;
            var id = await _catalog.CopyTemplateAsync(template.Id, user.Id);
            await _navigator.OpenWorkoutEditorAsync(id);
        });

    private Border PlanCard(string name, string? description, IReadOnlyList<string> exercises, Func<Task> select)
    {
        var content = new VerticalStackLayout { Spacing = 8 };
        content.Add(Ui.Text(name, 17, Ui.Foreground, FontAttributes.Bold));
        if (!string.IsNullOrWhiteSpace(description))
            content.Add(Ui.Text(description, 12, Ui.Muted));
        content.Add(Ui.Text(ConsumerUi.ExerciseCount(exercises.Count), 12, Ui.Muted));
        var preview = new VerticalStackLayout { Spacing = 5, IsVisible = false };
        foreach (var exercise in exercises) preview.Add(Ui.Text("•  " + exercise, 12, Ui.Muted));
        Button toggle = null!;
        toggle = Ui.Button("Ver exercícios", () =>
        {
            preview.IsVisible = !preview.IsVisible;
            toggle.Text = preview.IsVisible ? "Ocultar exercícios" : "Ver exercícios";
            return Task.CompletedTask;
        }, true);
        toggle.MinimumHeightRequest = 40;
        content.Add(toggle);
        content.Add(preview);
        var choose = Ui.Button("Usar e personalizar", () => RunAsync(select));
        content.Add(choose);
        return Ui.Card(content);
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            _status.Text = string.Empty;
            await action();
        }
        catch
        {
            _status.Text = "Não foi possível selecionar o treino. Tente novamente.";
        }
        finally
        {
            _busy = false;
        }
    }
}
