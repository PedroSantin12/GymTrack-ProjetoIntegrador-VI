using System.Globalization;
using GymTrack.Data.Dao;
using GymTrack.Services;
using GymTrack.Views.Components;
using Microsoft.Maui.Controls.Shapes;

namespace GymTrack.Views.Admin;

public sealed class AdminDashboardPage : WorkspacePage
{
    private const string ClipboardIcon = "M 9,5 L 5,5 L 5,22 L 19,22 L 19,5 L 15,5 M 9,3 L 15,3 L 15,7 L 9,7 Z M 8,11 L 16,11 M 8,15 L 16,15 M 8,19 L 13,19";
    private const string ExerciseIcon = "M 8,12 L 16,12 M 4,7 L 8,7 L 8,17 L 4,17 Z M 16,7 L 20,7 L 20,17 L 16,17 Z M 2,10 L 4,10 M 20,10 L 22,10 M 2,14 L 4,14 M 20,14 L 22,14";
    private const string ActivityIcon = "M 3,21 L 3,13 L 8,13 L 8,21 Z M 10,21 L 10,8 L 15,8 L 15,21 Z M 17,21 L 17,3 L 22,3 L 22,21 Z";
    private static readonly CultureInfo Portuguese = CultureInfo.GetCultureInfo("pt-BR");
    private readonly StudentService _students;
    private readonly IWorkoutCatalogService _catalog;
    private readonly IExerciseDao _exercises;
    private readonly Label _studentCount = CountLabel();
    private readonly Label _templateCount = CountLabel();
    private readonly Label _exerciseCount = CountLabel();
    private readonly Label _trainingCount = CountLabel();
    private readonly Label _status = Ui.Text("Carregando indicadores…", 13, Ui.Muted);
    private bool _loading;

    public AdminDashboardPage(IAppNavigator navigator, AppSession session,
        StudentService students, IWorkoutCatalogService catalog, IExerciseDao exercises)
        : base("Início", AppSection.AdminDashboard, navigator, session)
    {
        _students = students;
        _catalog = catalog;
        _exercises = exercises;

        var summary = new Grid
        {
            ColumnSpacing = 10,
            RowSpacing = 10,
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }
        };
        summary.Add(Metric(_studentCount, "Alunos cadastrados", Ui.PeopleIcon));
        summary.Add(Metric(_templateCount, "Treinos prontos", ClipboardIcon), 1);
        summary.Add(Metric(_exerciseCount, "Exercícios ativos", ExerciseIcon), 0, 1);
        summary.Add(Metric(_trainingCount, "Treinos registrados", ActivityIcon), 1, 1);

        var createWorkout = Ui.Button("Criar treino", () => navigator.OpenWorkoutEditorAsync(null));
        createWorkout.CornerRadius = 18;

        SetBody(new VerticalStackLayout
        {
            Spacing = 18,
            Children =
            {
                Heading("Painel administrativo", 25, SemanticHeadingLevel.Level1),
                summary,
                _status,
                Heading("Acesso rápido", 17, SemanticHeadingLevel.Level2),
                new VerticalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        Ui.LinkCard("Consultar alunos", "Dados, treinos e medidas", Ui.PeopleIcon,
                            () => navigator.NavigateAsync(AppSection.Students)),
                        Ui.LinkCard("Gerenciar treinos", "Modelos do catálogo", ClipboardIcon,
                            () => navigator.NavigateAsync(AppSection.AdminWorkouts)),
                        Ui.LinkCard("Biblioteca de exercícios", "Cadastrar e editar exercícios", ExerciseIcon,
                            () => navigator.NavigateAsync(AppSection.Exercises)),
                        createWorkout
                    }
                }
            }
        });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loading || AppDialogs.IsShowingFor(this)) return;
        _loading = true;
        try
        {
            var students = _students.GetDashboardCountsAsync();
            var templates = _catalog.CountTemplatesAsync();
            var exercises = _exercises.GetAllAsync();
            await Task.WhenAll(students, templates, exercises);
            var counts = await students;
            _studentCount.Text = counts.Students.ToString("N0", Portuguese);
            _templateCount.Text = (await templates).ToString("N0", Portuguese);
            _exerciseCount.Text = (await exercises).Count.ToString("N0", Portuguese);
            _trainingCount.Text = counts.RecordedWorkouts.ToString("N0", Portuguese);
            _status.IsVisible = false;
        }
        catch
        {
            _status.Text = "Não foi possível atualizar o resumo.";
            _status.IsVisible = true;
        }
        finally { _loading = false; }
    }

    private static Label CountLabel() => Ui.Text("—", 32, attributes: FontAttributes.Bold);

    private static Label Heading(string text, int size, SemanticHeadingLevel level)
    {
        var heading = Ui.Text(text, size, attributes: FontAttributes.Bold);
        SemanticProperties.SetHeadingLevel(heading, level);
        return heading;
    }

    private static View Metric(Label value, string caption, string icon)
    {
        var symbol = Ui.LineIcon(icon, Ui.Accent, 20);
        symbol.HorizontalOptions = LayoutOptions.Start;
        var card = Ui.Card(new VerticalStackLayout
        {
            Spacing = 5,
            Children = { symbol, value, Ui.Text(caption, 12, Ui.Muted) }
        }, new Thickness(13));
        card.StrokeShape = new RoundRectangle { CornerRadius = 17 };
        card.MinimumHeightRequest = 109;
        return card;
    }
}
