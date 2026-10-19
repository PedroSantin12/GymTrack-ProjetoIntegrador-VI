using GymTrack.Models;
using GymTrack.Services;
using GymTrack.Views.Components;

namespace GymTrack.Views.Consumer;

public sealed class TemplateLibraryPage : WorkspacePage
{
    private readonly IAppNavigator _navigator;
    private readonly AppSession _session;
    private readonly IWorkoutCatalogService _catalog;
    private readonly SearchBar _search;
    private readonly VerticalStackLayout _items = new() { Spacing = 14 };
    private readonly Label _status = ConsumerUi.Status();
    private int _request;
    private bool _busy;

    public TemplateLibraryPage(IAppNavigator navigator, AppSession session, IWorkoutCatalogService catalog)
        : base("Treinos prontos", "Escolha uma rotina e faça dela a sua.", AppSection.Templates, navigator, session)
    {
        _navigator = navigator;
        _session = session;
        _catalog = catalog;
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

        var body = new VerticalStackLayout { Spacing = 18 };
        body.Add(Ui.Tag("BIBLIOTECA"));
        body.Add(_search);
        body.Add(Ui.Text("Ao escolher, você recebe uma cópia para adaptar e acompanhar seus treinos.", 13, Ui.Muted));
        body.Add(_status);
        body.Add(_items);
        body.Add(Ui.Button("Meus treinos", () => RunAsync(() => _navigator.NavigateAsync(AppSection.PersonalWorkouts)), true));
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
            var templates = (await _catalog.GetTemplatesAsync(_search.Text))
                .Where(template => template.ExerciseCount > 0).ToList();
            if (request != _request) return;
            _items.Clear();
            foreach (var template in templates) _items.Add(TemplateCard(template));
            if (templates.Count == 0)
            {
                _items.Add(ConsumerUi.Empty("Nenhum treino encontrado", string.IsNullOrWhiteSpace(_search.Text)
                    ? "Os treinos publicados pela equipe aparecerão aqui. Você também pode criar seu próprio treino."
                    : "Tente pesquisar por outro nome."));
            }
            _status.Text = templates.Count == 0 ? string.Empty : $"{templates.Count} {(templates.Count == 1 ? "treino disponível" : "treinos disponíveis")}";
        }
        catch
        {
            if (request == _request) _status.Text = "Não foi possível buscar os treinos. Pesquise novamente para tentar.";
        }
    }

    private Border TemplateCard(WorkoutCatalogItem template)
    {
        var content = new VerticalStackLayout { Spacing = 13 };
        content.Add(Ui.Tag("TREINO PRONTO"));
        content.Add(Ui.Text(template.Name, 22, null, FontAttributes.Bold));
        if (!string.IsNullOrWhiteSpace(template.Description)) content.Add(Ui.Text(template.Description, 14, Ui.Muted));
        content.Add(Ui.Text(ConsumerUi.ExerciseCount(template.ExerciseCount), 13, Ui.Muted));
        var choose = Ui.Button("Escolher este treino", () => RunAsync(async () =>
        {
            if (_session.CurrentUser is not { } user) return;
            var id = await _catalog.CopyTemplateAsync(template.Id, user.Id);
            await _navigator.OpenWorkoutEditorAsync(id);
        }));
        choose.IsEnabled = template.ExerciseCount > 0;
        content.Add(choose);
        if (template.ExerciseCount == 0) content.Add(Ui.Text("A equipe está preparando os exercícios deste treino.", 12, Ui.Muted));
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
