using System.Globalization;
using GymTrack.Models;
using GymTrack.Services;
using GymTrack.Views.Components;

namespace GymTrack.Views.Admin;

internal sealed class StudentDetailsPage : ContentPage
{
    private static readonly CultureInfo Portuguese = CultureInfo.GetCultureInfo("pt-BR");
    private readonly int _studentId;
    private readonly StudentService _students;
    private readonly IWorkoutCatalogService _catalog;
    private readonly Label _status = Ui.Text("Carregando ficha…", 13, Ui.Muted);
    private readonly VerticalStackLayout _details = new() { Spacing = 15 };

    public StudentDetailsPage(int studentId, StudentService students, IWorkoutCatalogService catalog)
    {
        Title = "Ficha do aluno";
        _studentId = studentId;
        _students = students;
        _catalog = catalog;
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

    private async Task LoadAsync(string message = "")
    {
        _details.Clear();
        try
        {
            var student = await _students.GetDetailsAsync(_studentId);
            var templates = (await _catalog.GetTemplatesAsync()).Where(item => item.ExerciseCount > 0).ToList();
            _status.Text = message;
            _status.IsVisible = message.Length > 0;
            _details.Add(Ui.Text(student.Student.Name, 25, Ui.Foreground, FontAttributes.Bold));
            _details.Add(Ui.Text(student.Student.Email, 14, Ui.Muted));
            AddWorkouts(student, templates);
            AddHistory(student);
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

    private void AddWorkouts(StudentDetails student, List<WorkoutCatalogItem> templates)
    {
        _details.Add(Ui.Text("Treinos atribuídos", 18, Ui.Foreground, FontAttributes.Bold));
        if (student.Workouts.Count == 0)
            _details.Add(Ui.Text("Nenhum treino atribuído por você.", 13, Ui.Muted));
        foreach (var workout in student.Workouts)
        {
            var details = new VerticalStackLayout { Spacing = 4 };
            details.Add(Ui.Text(workout.Name, 16, Ui.Foreground, FontAttributes.Bold));
            details.Add(Ui.Text(workout.ExerciseCount == 1 ? "1 exercício" : $"{workout.ExerciseCount} exercícios",
                12, Ui.Muted));
            if (!string.IsNullOrWhiteSpace(workout.Description))
                details.Add(Ui.Text(workout.Description, 13, Ui.Muted));
            _details.Add(Ui.Card(details));
        }

        if (templates.Count == 0)
        {
            _details.Add(Ui.Text("Cadastre um treino com exercícios para poder atribuí-lo.", 13, Ui.Muted));
            return;
        }

        var picker = new Picker
        {
            Title = "Selecione um treino pronto",
            ItemsSource = templates,
            ItemDisplayBinding = new Binding(nameof(WorkoutCatalogItem.Name)),
            AutomationId = "AssignmentTemplate"
        };
        var assign = Ui.Button("Atribuir treino", () => AssignAsync(picker, student.Student.Name));
        assign.AutomationId = "AssignWorkout";
        _details.Add(Ui.Card(new VerticalStackLayout { Spacing = 8, Children = { picker, assign } }));
    }

    private void AddHistory(StudentDetails student)
    {
        _details.Add(Ui.Text("Histórico de atividades", 18, Ui.Foreground, FontAttributes.Bold));
        if (student.History.Count == 0)
        {
            _details.Add(Ui.Text(student.SharesHistory ? "Nenhum treino registrado."
                : "O histórico fica disponível quando o aluno autoriza o compartilhamento.", 13, Ui.Muted));
            return;
        }

        var history = new VerticalStackLayout { Spacing = 12 };
        foreach (var activity in student.History)
        {
            history.Add(new VerticalStackLayout
            {
                Spacing = 3,
                Children =
                {
                    Ui.Text(activity.WorkoutName, 15, Ui.Foreground, FontAttributes.Bold),
                    Ui.Text($"{activity.LocalDate.ToString("dd/MM/yyyy", Portuguese)} · " +
                        (activity.IsManual ? "Registro manual" : "Sessão concluída"), 12, Ui.Muted)
                }
            });
        }
        _details.Add(Ui.Card(history));
    }

    private async Task AssignAsync(Picker picker, string studentName)
    {
        if (picker.SelectedItem is not WorkoutCatalogItem template)
        {
            await DisplayAlertAsync("Escolha um treino", "Selecione um treino pronto para atribuir.", "Entendi");
            return;
        }

        if (!await DisplayAlertAsync("Atribuir treino?",
                $"“{template.Name}” ficará disponível em Meus treinos para {studentName}.",
                "Atribuir", "Cancelar")) return;

        try
        {
            await _students.AssignWorkoutAsync(_studentId, template.Id);
            await LoadAsync("Treino disponível na conta do aluno.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or UnauthorizedAccessException)
        {
            await DisplayAlertAsync("Atribuição indisponível", exception.Message, "Entendi");
        }
    }

    private void ShowStatus(string message)
    {
        _status.Text = message;
        _status.IsVisible = true;
    }
}
