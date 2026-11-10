using System.Globalization;
using GymTrack.Models;
using GymTrack.Services;
using GymTrack.Views.Components;

namespace GymTrack.Views.Admin;

public sealed class StudentsPage : WorkspacePage
{
    private readonly StudentService _students;
    private readonly IWorkoutCatalogService _catalog;
    private readonly VerticalStackLayout _list = new() { Spacing = 10 };
    private readonly Label _status = Ui.Text("Carregando alunos…", 13, Ui.Muted);
    private readonly SearchBar _search = new() { Placeholder = "Pesquisar por nome ou e-mail" };
    private IReadOnlyList<AppUser> _linkedStudents = [];
    private bool _loaded;

    public StudentsPage(IAppNavigator navigator, AppSession session,
        StudentService students, IWorkoutCatalogService catalog)
        : base("Alunos", AppSection.Students, navigator, session)
    {
        _students = students;
        _catalog = catalog;
        _search.TextChanged += (_, _) => Render();
        SetBody(new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                Ui.Text("Alunos", 25, Ui.Foreground, FontAttributes.Bold),
                _search, _status, _list
            }
        });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            _linkedStudents = await _students.GetLinkedStudentsAsync();
            _loaded = true;
            Render();
        }
        catch
        {
            _loaded = false;
            _list.Clear();
            _status.Text = "Não foi possível carregar os alunos vinculados.";
        }
    }

    private void Render()
    {
        if (!_loaded) return;
        var term = _search.Text?.Trim() ?? string.Empty;
        var compare = CultureInfo.GetCultureInfo("pt-BR").CompareInfo;
        const CompareOptions options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
        var visible = _linkedStudents.Where(student =>
            compare.IndexOf(student.Name, term, options) >= 0 ||
            compare.IndexOf(student.Email, term, options) >= 0).ToArray();

        _list.Clear();
        _status.Text = visible.Length == 1 ? "1 aluno vinculado" : $"{visible.Length} alunos vinculados";
        foreach (var student in visible)
        {
            _list.Add(Ui.Card(new VerticalStackLayout
            {
                Spacing = 7,
                Children =
                {
                    Ui.Text(student.Name, 17, Ui.Foreground, FontAttributes.Bold),
                    Ui.Text(student.Email, 13, Ui.Muted),
                    Ui.Button("Abrir ficha", () => Navigation.PushAsync(
                        new StudentDetailsPage(student.Id, _students, _catalog)), true)
                }
            }));
        }

        if (visible.Length == 0)
            _list.Add(Ui.Card(Ui.Text(_linkedStudents.Count == 0
                ? "O aluno pode criar o vínculo em Perfil → Vínculos com funcionários, usando seu e-mail."
                : "Nenhum aluno corresponde à pesquisa.", 14, Ui.Muted)));
    }
}
