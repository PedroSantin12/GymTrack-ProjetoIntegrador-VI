using GymTrack.Services;
using GymTrack.Views.Components;

namespace GymTrack.Views.Admin;

internal sealed class StudentDetailsPage : ContentPage
{
    private readonly int _studentId;
    private readonly StudentService _students;
    private readonly Label _status = Ui.Text("Carregando ficha…", 13, Ui.Muted);
    private readonly VerticalStackLayout _details = new() { Spacing = 15 };

    public StudentDetailsPage(int studentId, StudentService students)
    {
        Title = "Ficha do aluno";
        _studentId = studentId;
        _students = students;
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(18, 14), Spacing = 8, MaximumWidthRequest = 640,
                Children = { _status, _details }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _details.Clear();
        try
        {
            var student = await _students.GetStudentAsync(_studentId);
            _status.IsVisible = false;
            _details.Add(Ui.Text(student.Name, 25, Ui.Foreground, FontAttributes.Bold));
            _details.Add(Ui.Text(student.Email, 14, Ui.Muted));
        }
        catch (UnauthorizedAccessException exception)
        {
            ShowStatus(exception.Message);
        }
        catch
        {
            ShowStatus("Não foi possível carregar a ficha. Volte e abra o aluno novamente.");
        }
    }

    private void ShowStatus(string message)
    {
        _status.Text = message;
        _status.IsVisible = true;
    }
}
