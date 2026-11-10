using GymTrack.Services;
using GymTrack.Views.Components;

namespace GymTrack.Views.Admin;

public sealed class AdminDashboardPage : WorkspacePage
{
    private readonly StudentService _students;
    private readonly IWorkoutCatalogService _catalog;
    private readonly Label _studentCount = Ui.Text("—", 26, Ui.Accent, FontAttributes.Bold);
    private readonly Label _templateCount = Ui.Text("—", 26, Ui.Accent, FontAttributes.Bold);
    private readonly Label _status = Ui.Text(string.Empty, 13, Ui.Muted);
    private bool _loading;

    public AdminDashboardPage(IAppNavigator navigator, AppSession session,
        StudentService students, IWorkoutCatalogService catalog)
        : base("Início", AppSection.AdminDashboard, navigator, session)
    {
        _students = students;
        _catalog = catalog;

        var summary = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }
        };
        summary.Add(Metric(_studentCount, "Alunos vinculados"));
        summary.Add(Metric(_templateCount, "Treinos no catálogo"), 1);

        SetBody(new VerticalStackLayout
        {
            Spacing = 18,
            Children =
            {
                Ui.Text("Início", 25, Ui.Foreground, FontAttributes.Bold),
                Ui.Card(summary),
                _status,
                Ui.Text("Atalhos", 17, Ui.Foreground, FontAttributes.Bold),
                new VerticalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        Ui.Button("Criar treino", () => navigator.OpenWorkoutEditorAsync(null)),
                        Ui.Button("Consultar alunos", () => navigator.NavigateAsync(AppSection.Students), true),
                        Ui.Button("Abrir biblioteca de exercícios", () => navigator.NavigateAsync(AppSection.Exercises), true)
                    }
                }
            }
        });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loading) return;
        _loading = true;
        try
        {
            var students = _students.GetLinkedStudentsAsync();
            var templates = _catalog.CountTemplatesAsync();
            await Task.WhenAll(students, templates);
            var studentCount = (await students).Count;
            _studentCount.Text = studentCount.ToString();
            _templateCount.Text = (await templates).ToString();
            _status.Text = studentCount == 0
                ? "Para aparecer aqui, o aluno pode vincular seu e-mail pelo Perfil."
                : string.Empty;
            _status.IsVisible = studentCount == 0;
        }
        catch
        {
            _status.Text = "Não foi possível atualizar o resumo.";
            _status.IsVisible = true;
        }
        finally { _loading = false; }
    }

    private static View Metric(Label value, string caption) => new VerticalStackLayout
    {
        Spacing = 4,
        Children = { value, Ui.Text(caption, 12, Ui.Muted) }
    };
}
