using GymTrack.Models;
using GymTrack.Services;
using GymTrack.Views.Components;
using Microsoft.Extensions.DependencyInjection;

namespace GymTrack.Views.Consumer;

public sealed class TemplateLibraryPage : WorkspacePage
{
    private readonly IAppNavigator _navigator;
    private readonly IWorkoutCatalogService _catalog;
    private readonly IStarterCatalogService _starterCatalog;
    private readonly IServiceProvider _services;
    private readonly SearchBar _search;
    private readonly VerticalStackLayout _items = new() { Spacing = 12 };
    private readonly Label _status = ConsumerUi.Status();
    private int _request;
    private bool _busy;

    public TemplateLibraryPage(IAppNavigator navigator, AppSession session,
        IWorkoutCatalogService catalog, IStarterCatalogService starterCatalog,
        IServiceProvider services)
        : base("Treinos prontos", "Escolha uma rotina e faça dela a sua.", AppSection.Templates, navigator, session)
    {
        _navigator = navigator;
        _catalog = catalog;
        _starterCatalog = starterCatalog;
        _services = services;
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
        body.Add(Ui.Text("Veja os exercícios e a execução antes de adicionar um treino à sua rotina.", 13, Ui.Muted));
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
            await _starterCatalog.EnsureSeededAsync();
            var term = _search.Text?.Trim();
            var starters = _catalog.GetStarterPlans()
                .Where(item => string.IsNullOrEmpty(term) ||
                    item.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
                    item.Description.Contains(term, StringComparison.CurrentCultureIgnoreCase))
                .ToList();
            var templates = (await _catalog.GetTemplatesAsync(term))
                .Where(template => template.ExerciseCount > 0).ToList();
            if (request != _request) return;

            _items.Clear();
            if (starters.Count > 0)
            {
                _items.Add(Ui.Text("Modelos para começar", 17, Ui.Foreground, FontAttributes.Bold));
                foreach (var starter in starters) _items.Add(StarterCard(starter));
            }
            if (templates.Count > 0)
            {
                _items.Add(Ui.Text("Treinos de músculos e da equipe", 17, Ui.Foreground, FontAttributes.Bold));
                foreach (var template in templates) _items.Add(TemplateCard(template));
            }
            if (starters.Count + templates.Count == 0)
                _items.Add(ConsumerUi.Empty("Nenhum treino encontrado", "Tente pesquisar por outro nome."));
            _status.Text = string.Empty;
        }
        catch
        {
            if (request == _request)
                _status.Text = "Não foi possível buscar os treinos. Pesquise novamente para tentar.";
        }
    }

    private Border StarterCard(StarterWorkoutPlan plan) => PlanCard(
        plan.Name, plan.Description, plan.Exercises.Count,
        async () =>
        {
            var detail = _services.GetRequiredService<TemplateDetailPage>();
            await detail.InitializeStarterAsync(plan);
            await Navigation.PushAsync(detail);
        });

    private Border TemplateCard(WorkoutCatalogItem template) => PlanCard(
        template.Name, template.Description, template.ExerciseCount,
        async () =>
        {
            var detail = _services.GetRequiredService<TemplateDetailPage>();
            await detail.InitializeAsync(template.Id);
            await Navigation.PushAsync(detail);
        });

    private Border PlanCard(string name, string? description, int count, Func<Task> open)
    {
        var content = new VerticalStackLayout { Spacing = 12 };
        content.Add(Ui.Tag("TREINO PRONTO"));
        content.Add(Ui.Text(name, 20, Ui.Foreground, FontAttributes.Bold));
        if (!string.IsNullOrWhiteSpace(description))
            content.Add(Ui.Text(description, 13, Ui.Muted));
        content.Add(Ui.Text(ConsumerUi.ExerciseCount(count), 13, Ui.Muted));
        content.Add(Ui.Button("Ver exercícios", () => RunAsync(open)));
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
