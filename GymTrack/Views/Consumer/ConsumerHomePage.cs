using GymTrack.Models;
using GymTrack.Services;
using GymTrack.Views.Components;

namespace GymTrack.Views.Consumer;

public sealed class ConsumerHomePage : WorkspacePage
{
    private readonly IAppNavigator _navigator;
    private readonly AppSession _session;
    private readonly IWorkoutCatalogService _catalog;
    private readonly VerticalStackLayout _workouts = new() { Spacing = 12 };
    private readonly Label _status = ConsumerUi.Status();
    private bool _loading;
    private bool _busy;

    public ConsumerHomePage(IAppNavigator navigator, AppSession session, IWorkoutCatalogService catalog)
        : base("Seu ritmo. Seu treino.", "Um novo dia para cuidar de você.", AppSection.Home, navigator, session)
    {
        _navigator = navigator;
        _session = session;
        _catalog = catalog;

        var body = new VerticalStackLayout { Spacing = 22 };
        body.Add(Ui.Tag("SEU PRÓXIMO PASSO"));
        body.Add(ActionCard("01 / DESCUBRA", "Encontre seu treino", "Explore os treinos preparados para você e escolha por onde começar.", "Ver treinos prontos", () => _navigator.NavigateAsync(AppSection.Templates)));
        body.Add(ActionCard("02 / PERSONALIZE", "Do seu jeito", "Monte uma rotina com os exercícios que fazem sentido para seu objetivo.", "Criar meu treino", CreateWorkoutAsync, true));
        body.Add(ConsumerUi.Section("Meus treinos", "Suas rotinas, sempre à mão."));
        body.Add(_workouts);
        body.Add(_status);
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
            _workouts.Clear();
            if (workouts.Count == 0)
            {
                _workouts.Add(ConsumerUi.Empty("Sua rotina começa aqui", "Escolha um treino pronto ou crie seu primeiro treino acima."));
            }
            else
            {
                foreach (var workout in workouts.Take(2))
                {
                    var details = new VerticalStackLayout { Spacing = 10 };
                    details.Add(Ui.Text(workout.Name, 18, null, FontAttributes.Bold));
                    details.Add(Ui.Text(ConsumerUi.ExerciseCount(workout.ExerciseCount), 13, Ui.Muted));
                    details.Add(Ui.Button("Abrir treino", () => RunAsync(() => _navigator.OpenWorkoutEditorAsync(workout.Id)), true));
                    _workouts.Add(Ui.Card(details));
                }
                _workouts.Add(Ui.Button("Ver todos os meus treinos", () => RunAsync(() => _navigator.NavigateAsync(AppSection.PersonalWorkouts)), true));
            }
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

    private Border ActionCard(string eyebrow, string title, string description, string action, Func<Task> execute, bool secondary = false)
    {
        var content = new VerticalStackLayout { Spacing = 16 };
        content.Add(Ui.Text(eyebrow, 11, Ui.Accent, FontAttributes.Bold));
        content.Add(Ui.Text(title, 28, null, FontAttributes.Bold));
        content.Add(Ui.Text(description, 14, Ui.Muted));
        content.Add(Ui.Button(action, () => RunAsync(execute), secondary));
        return Ui.Card(content, new Thickness(22));
    }

    private async Task CreateWorkoutAsync()
    {
        if (_session.CurrentUser is not { } user) return;
        await _navigator.OpenWorkoutEditorAsync(null);
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

internal static class ConsumerUi
{
    public static Label Status() => Ui.Text(string.Empty, 13, Ui.Accent);

    public static string ExerciseCount(int count) => count == 1 ? "1 exercício" : $"{count} exercícios";

    public static View Section(string title, string description)
    {
        var content = new VerticalStackLayout { Spacing = 5 };
        content.Add(Ui.Text(title, 21, null, FontAttributes.Bold));
        content.Add(Ui.Text(description, 13, Ui.Muted));
        return content;
    }

    public static Border Empty(string title, string description)
    {
        var content = new VerticalStackLayout { Spacing = 9 };
        content.Add(Ui.Text(title, 18, null, FontAttributes.Bold));
        content.Add(Ui.Text(description, 14, Ui.Muted));
        return Ui.Card(content);
    }

    public static Border Metric(string value, string title)
    {
        var content = new VerticalStackLayout { Spacing = 5 };
        content.Add(Ui.Text(value, 26, Ui.Accent, FontAttributes.Bold));
        content.Add(Ui.Text(title, 12, Ui.Muted));
        return Ui.Card(content, new Thickness(14));
    }

    public static View Detail(string title, string value)
    {
        var content = new VerticalStackLayout { Spacing = 5 };
        content.Add(Ui.Text(title, 12, Ui.Muted));
        content.Add(Ui.Text(value, 16, null, FontAttributes.Bold));
        return content;
    }
}
