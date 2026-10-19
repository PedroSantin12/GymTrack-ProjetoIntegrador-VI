using GymTrack.Models;
using GymTrack.Data.Dao;
using GymTrack.Services;
using GymTrack.Views.Components;

namespace GymTrack.Views.Admin;

public sealed class StatisticsPage : WorkspacePage
{
    private readonly IAccountService _accounts;
    private readonly IWorkoutCatalogService _catalog;
    private readonly IWorkoutDao _workouts;
    private readonly Label _allWorkoutCount = Ui.Text("—", 36, Ui.Foreground, FontAttributes.Bold);
    private readonly Label _userCount = Ui.Text("—", 36, Ui.Foreground, FontAttributes.Bold);
    private readonly Label _templateCount = Ui.Text("—", 36, Ui.Foreground, FontAttributes.Bold);
    private readonly Label _activeCount = Ui.Text("—", 27, Ui.Foreground, FontAttributes.Bold);
    private readonly Label _inactiveCount = Ui.Text("—", 27, Ui.Foreground, FontAttributes.Bold);
    private readonly Label _consumerCount = Ui.Text("—", 14, Ui.Muted);
    private readonly Label _employeeCount = Ui.Text("—", 14, Ui.Muted);
    private readonly Label _updatedAt = Ui.Text("Carregando indicadores…", 12, Ui.Muted);
    private readonly Label _status = Ui.Text(string.Empty, 13, Ui.Accent);
    private readonly ProgressBar _consumerBar = new() { ProgressColor = Ui.Accent, BackgroundColor = Ui.Elevated, HeightRequest = 6 };
    private readonly ProgressBar _employeeBar = new() { ProgressColor = Color.FromArgb("#B9B9B9"), BackgroundColor = Ui.Elevated, HeightRequest = 6 };
    private bool _isLoading;

    public StatisticsPage(IAppNavigator navigator, AppSession session,
        IAccountService accounts, IWorkoutCatalogService catalog, IWorkoutDao workouts)
        : base("Estatísticas", "Os números atuais da sua academia.", AppSection.Statistics, navigator, session)
    {
        _accounts = accounts;
        _catalog = catalog;
        _workouts = workouts;
        _status.IsVisible = false;

        var accountsGrid = new Grid { ColumnSpacing = 12 };
        accountsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        accountsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        accountsGrid.Add(Metric("CONTAS ATIVAS", _activeCount));
        accountsGrid.Add(Metric("DESATIVADAS", _inactiveCount), 1);

        SetBody(new VerticalStackLayout
        {
            Spacing = 18,
            Children =
            {
                Metric("USUÁRIOS CADASTRADOS", _userCount, "Todas as contas registradas no aplicativo."),
                Metric("TREINOS CRIADOS", _allWorkoutCount, "Total de rotinas pessoais e modelos do catálogo."),
                Metric("TREINOS PRONTOS", _templateCount, "Rotinas criadas para o catálogo da academia."),
                accountsGrid,
                Ui.Card(new VerticalStackLayout
                {
                    Spacing = 16,
                    Children =
                    {
                        Ui.Text("Perfis de acesso", 20, Ui.Foreground, FontAttributes.Bold),
                        Distribution("Consumidores", _consumerCount, _consumerBar),
                        Distribution("Funcionários", _employeeCount, _employeeBar)
                    }
                }, new Thickness(20)),
                _status,
                Ui.Button("Atualizar indicadores", LoadStatisticsAsync, true),
                _updatedAt
            }
        });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadStatisticsAsync();
    }

    private async Task LoadStatisticsAsync()
    {
        if (_isLoading)
            return;

        _isLoading = true;
        try
        {
            var userTask = _accounts.GetUsersAsync();
            var templateTask = _catalog.CountTemplatesAsync();
            var workoutsTask = _workouts.GetAllAsync();
            await Task.WhenAll(userTask, templateTask, workoutsTask);
            var users = await userTask;
            var consumers = users.Count(user => user.Role == UserRole.Consumer);
            var employees = users.Count(user => user.Role == UserRole.Employee);

            _userCount.Text = users.Count.ToString();
            _templateCount.Text = (await templateTask).ToString();
            _allWorkoutCount.Text = (await workoutsTask).Count.ToString();
            _activeCount.Text = users.Count(user => user.IsActive).ToString();
            _inactiveCount.Text = users.Count(user => !user.IsActive).ToString();
            _consumerCount.Text = consumers.ToString();
            _employeeCount.Text = employees.ToString();
            _consumerBar.Progress = users.Count == 0 ? 0 : (double)consumers / users.Count;
            _employeeBar.Progress = users.Count == 0 ? 0 : (double)employees / users.Count;
            _updatedAt.Text = $"Atualizado em {DateTime.Now:dd/MM/yyyy 'às' HH:mm}";
            _status.IsVisible = false;
        }
        catch
        {
            _status.Text = "Não foi possível atualizar os indicadores. Tente novamente.";
            _status.IsVisible = true;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private static Border Metric(string title, Label count, string? caption = null)
    {
        var content = new VerticalStackLayout
        {
            Spacing = 8,
            Children = { Ui.Text(title, 10, Ui.Accent, FontAttributes.Bold), count }
        };
        if (caption is not null)
            content.Children.Add(Ui.Text(caption, 13, Ui.Muted));
        return Ui.Card(content, new Thickness(20));
    }

    private static VerticalStackLayout Distribution(string title, Label count, ProgressBar bar)
    {
        var heading = new Grid();
        heading.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        heading.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        heading.Add(Ui.Text(title, 14, Ui.Foreground));
        heading.Add(count, 1);
        return new VerticalStackLayout { Spacing = 10, Children = { heading, bar } };
    }
}
