using GymTrack.Services;
using GymTrack.Views.Components;

namespace GymTrack.Views.Admin;

public sealed class AdminDashboardPage : WorkspacePage
{
    private readonly IAccountService _accounts;
    private readonly IWorkoutCatalogService _catalog;
    private readonly IAppNavigator _navigator;
    private readonly Label _userCount = Ui.Text("—", 32, Ui.Foreground, FontAttributes.Bold);
    private readonly Label _templateCount = Ui.Text("—", 32, Ui.Foreground, FontAttributes.Bold);
    private readonly Label _status = Ui.Text(string.Empty, 13, Ui.Accent);
    private bool _isLoading;

    public AdminDashboardPage(IAppNavigator navigator, AppSession session,
        IAccountService accounts, IWorkoutCatalogService catalog)
        : base("Painel administrativo", "Uma visão geral do GymTrack.", AppSection.AdminDashboard, navigator, session)
    {
        _accounts = accounts;
        _catalog = catalog;
        _navigator = navigator;
        _status.IsVisible = false;

        var metrics = new Grid { ColumnSpacing = 12 };
        metrics.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        metrics.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        metrics.Add(Metric(_userCount, "Usuários cadastrados"));
        metrics.Add(Metric(_templateCount, "Treinos prontos"), 1);

        SetBody(new VerticalStackLayout
        {
            Spacing = 20,
            Children =
            {
                Ui.Card(new VerticalStackLayout
                {
                    Spacing = 10,
                    Children =
                    {
                        Ui.Tag("ÁREA DO FUNCIONÁRIO"),
                        Ui.Text("Tudo pronto para\num novo treino.", 27, Ui.Foreground, FontAttributes.Bold),
                        Ui.Text("Cuide das pessoas, organize os exercícios e prepare as próximas rotinas.", 14, Ui.Muted)
                    }
                }, new Thickness(22)),
                metrics,
                _status,
                Ui.Text("Gerencie sua academia", 20, Ui.Foreground, FontAttributes.Bold),
                Destination("01", "Usuários", "Pesquise, edite e gerencie o acesso das contas.", AppSection.Users),
                Destination("02", "Exercícios", "Organize os movimentos por grupo muscular.", AppSection.Exercises),
                Destination("03", "Treinos prontos", "Monte e atualize as rotinas do catálogo.", AppSection.AdminWorkouts),
                Destination("04", "Estatísticas", "Acompanhe os números atuais do sistema.", AppSection.Statistics)
            }
        });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_isLoading)
            return;

        _isLoading = true;
        try
        {
            var users = _accounts.CountUsersAsync();
            var templates = _catalog.CountTemplatesAsync();
            await Task.WhenAll(users, templates);
            _userCount.Text = (await users).ToString();
            _templateCount.Text = (await templates).ToString();
            _status.IsVisible = false;
        }
        catch
        {
            _status.Text = "Não foi possível atualizar os indicadores. Volte a esta página para tentar novamente.";
            _status.IsVisible = true;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private static Border Metric(Label value, string caption) => Ui.Card(new VerticalStackLayout
    {
        Spacing = 6,
        Children = { value, Ui.Text(caption, 12, Ui.Muted) }
    }, new Thickness(18));

    private Border Destination(string index, string title, string description, AppSection section)
    {
        var heading = new Grid { ColumnSpacing = 12 };
        heading.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        heading.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        heading.Add(Ui.Text(index, 16, Ui.Accent, FontAttributes.Bold));
        heading.Add(Ui.Text(title, 19, Ui.Foreground, FontAttributes.Bold), 1);

        return Ui.Card(new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                heading,
                Ui.Text(description, 13, Ui.Muted),
                Ui.Button("Abrir", async () =>
                {
                    try
                    {
                        await _navigator.NavigateAsync(section);
                    }
                    catch
                    {
                        _status.Text = "Não foi possível abrir esta página. Tente novamente.";
                        _status.IsVisible = true;
                    }
                }, true)
            }
        }, new Thickness(18));
    }
}
