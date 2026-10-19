using GymTrack.Models;
using GymTrack.Services;
using GymTrack.Views.Components;

namespace GymTrack.Views.Admin;

public sealed class TemplateManagementPage : WorkspacePage
{
    private readonly IWorkoutCatalogService _catalog;
    private readonly IAppNavigator _navigator;
    private readonly VerticalStackLayout _templates = new() { Spacing = 12 };
    private readonly Label _summary = Ui.Text("Carregando treinos…", 13, Ui.Muted);
    private readonly Label _status = Ui.Text(string.Empty, 13, Ui.Accent);
    private readonly SearchBar _search = new()
    {
        Placeholder = "Pesquisar treino pronto",
        BackgroundColor = Ui.Surface,
        TextColor = Ui.Foreground,
        PlaceholderColor = Ui.Muted,
        CancelButtonColor = Ui.Accent,
        FontSize = 14
    };
    private int _loadVersion;
    private bool _isBusy;

    public TemplateManagementPage(IAppNavigator navigator, AppSession session, IWorkoutCatalogService catalog)
        : base("Treinos prontos", "Rotinas preparadas para seus alunos.", AppSection.AdminWorkouts, navigator, session)
    {
        _catalog = catalog;
        _navigator = navigator;
        _status.IsVisible = false;
        _search.TextChanged += OnSearchChanged;

        SetBody(new VerticalStackLayout
        {
            Spacing = 18,
            Children =
            {
                Ui.Card(new VerticalStackLayout
                {
                    Spacing = 12,
                    Children =
                    {
                        Ui.Tag("CATÁLOGO DA ACADEMIA"),
                        Ui.Text("Um bom treino\ncomeça aqui.", 25, Ui.Foreground, FontAttributes.Bold),
                        Ui.Text("Crie rotinas com exercícios, séries e repetições para os consumidores.", 14, Ui.Muted),
                        Ui.Button("Criar treino pronto", CreateTemplateAsync)
                    }
                }, new Thickness(22)),
                _search,
                _summary,
                _status,
                _templates
            }
        });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadTemplatesAsync();
    }

    private async void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        await LoadTemplatesAsync();
    }

    private async Task<bool> LoadTemplatesAsync()
    {
        var version = ++_loadVersion;
        try
        {
            var templates = await _catalog.GetTemplatesAsync(_search.Text);
            if (version != _loadVersion)
                return false;

            _templates.Children.Clear();
            _summary.Text = templates.Count == 1 ? "1 treino pronto" : $"{templates.Count} treinos prontos";
            foreach (var template in templates)
                _templates.Children.Add(TemplateCard(template));

            if (templates.Count == 0)
            {
                _templates.Children.Add(Ui.Card(new VerticalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        Ui.Text("Nenhum treino por aqui", 18, Ui.Foreground, FontAttributes.Bold),
                        Ui.Text(string.IsNullOrWhiteSpace(_search.Text)
                            ? "Crie o primeiro treino pronto para começar seu catálogo."
                            : "Experimente outro nome na pesquisa.", 14, Ui.Muted)
                    }
                }));
            }

            _status.Text = string.Empty;
            _status.IsVisible = false;
            return true;
        }
        catch
        {
            if (version == _loadVersion)
            {
                _summary.Text = "Não foi possível carregar o catálogo.";
                ShowStatus("Volte a esta página ou altere a pesquisa para tentar novamente.");
            }
            return false;
        }
    }

    private Border TemplateCard(WorkoutCatalogItem template)
    {
        var actions = new Grid { ColumnSpacing = 10, Margin = new Thickness(0, 8, 0, 0) };
        actions.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        actions.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        actions.Add(Ui.Button("Editar treino", () => EditTemplateAsync(template.Id), true));
        actions.Add(Ui.Button("Excluir", () => DeleteTemplateAsync(template), true), 1);

        var content = new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                Ui.Tag("TREINO PRONTO"),
                Ui.Text(template.Name, 21, Ui.Foreground, FontAttributes.Bold),
                Ui.Text(template.ExerciseCount == 1 ? "1 exercício" : $"{template.ExerciseCount} exercícios", 13, Ui.Muted)
            }
        };
        if (!string.IsNullOrWhiteSpace(template.Description))
            content.Children.Add(Ui.Text(template.Description, 14, Ui.Muted));
        content.Children.Add(actions);
        return Ui.Card(content, new Thickness(20));
    }

    private async Task CreateTemplateAsync()
    {
        if (_isBusy)
            return;

        _isBusy = true;
        try
        {
            await _navigator.OpenWorkoutEditorAsync(null);
        }
        catch
        {
            ShowStatus("Não foi possível abrir o novo treino. Tente novamente.");
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task EditTemplateAsync(int id)
    {
        if (_isBusy)
            return;

        _isBusy = true;
        try
        {
            await _navigator.OpenWorkoutEditorAsync(id);
        }
        catch
        {
            ShowStatus("Não foi possível abrir o editor. Tente novamente.");
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task DeleteTemplateAsync(WorkoutCatalogItem template)
    {
        if (_isBusy)
            return;

        _isBusy = true;
        try
        {
            if (!await DisplayAlertAsync("Excluir treino pronto?", $"O treino “{template.Name}” será removido do catálogo.", "Excluir", "Cancelar"))
                return;

            await _catalog.DeleteTemplateAsync(template.Id);
            if (await LoadTemplatesAsync())
                ShowStatus("Treino removido do catálogo.");
        }
        catch
        {
            ShowStatus("Não foi possível excluir o treino. Tente novamente.");
        }
        finally
        {
            _isBusy = false;
        }
    }

    private void ShowStatus(string message)
    {
        _status.Text = message;
        _status.IsVisible = true;
    }
}
