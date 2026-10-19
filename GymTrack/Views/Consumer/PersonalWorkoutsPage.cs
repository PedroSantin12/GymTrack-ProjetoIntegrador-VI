using GymTrack.Models;
using GymTrack.Services;
using GymTrack.Views.Components;

namespace GymTrack.Views.Consumer;

public sealed class PersonalWorkoutsPage : WorkspacePage
{
    private readonly IAppNavigator _navigator;
    private readonly AppSession _session;
    private readonly IWorkoutCatalogService _catalog;
    private readonly VerticalStackLayout _items = new() { Spacing = 14 };
    private readonly Label _status = ConsumerUi.Status();
    private bool _loading;
    private bool _busy;

    public PersonalWorkoutsPage(IAppNavigator navigator, AppSession session, IWorkoutCatalogService catalog)
        : base("Meus treinos", "Sua rotina, organizada do seu jeito.", AppSection.PersonalWorkouts, navigator, session)
    {
        _navigator = navigator;
        _session = session;
        _catalog = catalog;

        var body = new VerticalStackLayout { Spacing = 18 };
        body.Add(Ui.Button("+ Criar treino", () => RunAsync(async () =>
        {
            if (_session.CurrentUser is not { } user) return;
            await _navigator.OpenWorkoutEditorAsync(null);
        })));
        body.Add(Ui.Button("Explorar treinos prontos", () => RunAsync(() => _navigator.NavigateAsync(AppSection.Templates)), true));
        body.Add(_status);
        body.Add(_items);
        SetBody(body);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loading) return;
        _loading = true;
        try
        {
            if (_session.CurrentUser is not { } user) return;
            _status.Text = "Carregando seus treinos...";
            var workouts = await _catalog.GetPersonalAsync(user.Id);
            _items.Clear();
            foreach (var workout in workouts) _items.Add(WorkoutCard(workout));
            if (workouts.Count == 0) _items.Add(ConsumerUi.Empty("Um espaço para suas rotinas", "Crie um treino com seus exercícios ou escolha um dos treinos prontos da equipe."));
            _status.Text = string.Empty;
        }
        catch
        {
            _status.Text = "Não foi possível carregar seus treinos. Abra esta página novamente para tentar.";
        }
        finally
        {
            _loading = false;
        }
    }

    private Border WorkoutCard(WorkoutCatalogItem workout)
    {
        var content = new VerticalStackLayout { Spacing = 13 };
        content.Add(Ui.Text(workout.Name, 22, null, FontAttributes.Bold));
        if (!string.IsNullOrWhiteSpace(workout.Description)) content.Add(Ui.Text(workout.Description, 14, Ui.Muted));
        content.Add(Ui.Text(ConsumerUi.ExerciseCount(workout.ExerciseCount), 13, Ui.Muted));

        var actions = new Grid
        {
            ColumnSpacing = 10,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }
        };
        actions.Add(Ui.Button("Editar", () => RunAsync(() => _navigator.OpenWorkoutEditorAsync(workout.Id)), true), 0);
        var start = Ui.Button("Começar treino", () => RunAsync(() => _navigator.StartWorkoutAsync(workout.Id)));
        start.IsEnabled = workout.ExerciseCount > 0;
        actions.Add(start, 1);
        content.Add(actions);
        if (workout.ExerciseCount == 0) content.Add(Ui.Text("Adicione exercícios para começar este treino.", 12, Ui.Muted));
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
            _status.Text = "Não foi possível abrir o treino. Tente novamente.";
        }
        finally
        {
            _busy = false;
        }
    }
}
