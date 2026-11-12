using System.Globalization;
using GymTrack.Models;
using GymTrack.Services;
using GymTrack.Views.Components;
using GymTrack.Views.Consumer;

namespace GymTrack.Views.Admin;

internal sealed class StudentDetailsPage : ContentPage
{
    private static readonly CultureInfo Portuguese = CultureInfo.GetCultureInfo("pt-BR");
    private readonly int _studentId;
    private readonly StudentService _students;
    private readonly Label _status = Ui.Text("Carregando ficha…", 13, Ui.Muted);
    private readonly VerticalStackLayout _details = new() { Spacing = 15 };
    private bool _loading;

    public StudentDetailsPage(int studentId, StudentService students)
    {
        Title = "Ficha do aluno";
        BackgroundColor = Ui.Background;
        _studentId = studentId;
        _students = students;
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(18, 14, 18, 24), Spacing = 8, MaximumWidthRequest = 640,
                Children = { _status, _details }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loading || AppDialogs.IsShowingFor(this)) return;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var profile = await _students.GetDetailsAsync(_studentId);
            _details.Clear();
            _details.Add(PersonalDetails(profile.Account));
            _details.Add(WorkoutHistory(profile));
            _details.Add(MeasurementHistory(profile.Measurements));
            _status.IsVisible = false;
        }
        catch (UnauthorizedAccessException exception)
        {
            ShowStatus(exception.Message);
        }
        catch
        {
            ShowStatus("Não foi possível carregar a ficha. Volte e abra o aluno novamente.");
        }
        finally
        {
            _loading = false;
        }
    }

    private View PersonalDetails(AppUser student)
    {
        var name = Ui.Text(student.Name, 23, attributes: FontAttributes.Bold);
        SemanticProperties.SetHeadingLevel(name, SemanticHeadingLevel.Level1);
        var edit = Ui.Button("Editar dados pessoais", () => EditDetailsAsync(student), true);
        edit.CornerRadius = 18;
        return Ui.Card(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                name,
                Ui.Text(student.IsActive ? "Conta ativa" : "Conta desativada", 12, Ui.Accent),
                Detail("E-mail", student.Email),
                Detail("Telefone", string.IsNullOrWhiteSpace(student.Phone) ? "Não informado" : student.Phone),
                edit
            }
        });
    }

    private Task EditDetailsAsync(AppUser student)
    {
        var name = ProfileSettingsPage.Field("Nome", 100, student.Name);
        var email = ProfileSettingsPage.Field("E-mail", 254, student.Email, Keyboard.Email);
        var phone = ProfileSettingsPage.Field("Telefone (opcional)", 30, student.Phone, Keyboard.Telephone);
        var page = ProfileSettingsPage.Form("Editar dados do aluno",
            [("Nome", name), ("E-mail", email), ("Telefone (opcional)", phone)], "Salvar dados", async () =>
            {
                await _students.UpdateStudentAsync(student.Id, name.Text ?? string.Empty, email.Text ?? string.Empty, phone.Text);
            });
        return Navigation.PushAsync(page);
    }

    private static View WorkoutHistory(StudentDetails profile)
    {
        var section = Section("Treinos realizados");
        var entries = profile.Sessions
            .Select(session => (Date: session.StartedAt.ToLocalTime(), Name: session.WorkoutName,
                Info: WorkoutStats(session), Notes: (string?)null))
            .Concat(profile.ManualLogs.Select(log =>
                (Date: log.PerformedOn, Name: log.WorkoutName, Info: "Registrado manualmente", Notes: log.Notes)))
            .OrderByDescending(entry => entry.Date);

        foreach (var entry in entries)
        {
            var row = new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    Ui.Text(entry.Name, 15, attributes: FontAttributes.Bold),
                    Ui.Text(DateText(entry.Date), 12, Ui.Accent),
                    Ui.Text(entry.Info, 13, Ui.Muted)
                }
            };
            if (!string.IsNullOrWhiteSpace(entry.Notes))
                row.Add(Ui.Text(entry.Notes, 13, Ui.Muted));
            section.Add(Ui.Card(row, new Thickness(13)));
        }

        if (profile.Sessions.Count == 0 && profile.ManualLogs.Count == 0)
            section.Add(Ui.Card(Ui.Text("Nenhum treino registrado.", 13, Ui.Muted)));
        return section;
    }

    private static View MeasurementHistory(IReadOnlyList<BodyMeasurement> history)
    {
        var section = Section("Medidas cadastradas");
        foreach (var measurement in history)
        {
            var row = new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    Ui.Text(DateText(measurement.MeasuredOn), 15, attributes: FontAttributes.Bold),
                    Ui.Text($"Peso · {Measure(measurement.WeightKg, "kg")}", 13, Ui.Muted)
                }
            };
            AddMeasurement(row, "Altura", measurement.HeightCm);
            AddMeasurement(row, "Cintura", measurement.WaistCm);
            AddMeasurement(row, "Quadril", measurement.HipCm);
            AddMeasurement(row, "Peito", measurement.ChestCm);
            section.Add(Ui.Card(row, new Thickness(13)));
        }
        if (history.Count == 0)
            section.Add(Ui.Card(Ui.Text("Nenhuma medida cadastrada.", 13, Ui.Muted)));
        return section;
    }

    private static VerticalStackLayout Section(string title)
    {
        var heading = Ui.Text(title, 17, attributes: FontAttributes.Bold);
        SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level2);
        return new VerticalStackLayout { Spacing = 8, Children = { heading } };
    }

    private static View Detail(string name, string value) => new VerticalStackLayout
    {
        Spacing = 2,
        Children = { Ui.Text(name, 12, Ui.Muted), Ui.Text(value, 14) }
    };

    private static void AddMeasurement(VerticalStackLayout row, string name, double? value)
    {
        if (value is double number)
            row.Add(Ui.Text($"{name} · {Measure(number, "cm")}", 13, Ui.Muted));
    }

    private static string WorkoutStats(SessionSummary session)
    {
        var sets = session.SetCount == 1 ? "1 série" : $"{session.SetCount} séries";
        var reps = session.TotalRepetitions == 1 ? "1 repetição" : $"{session.TotalRepetitions} repetições";
        return $"{sets} · {reps} · {Measure(session.TotalVolume, "kg")} de volume";
    }

    private static string Measure(double number, string unit) => $"{number.ToString("0.#", Portuguese)} {unit}";

    private static string DateText(DateTime date) => date.ToString("dd/MM/yyyy", Portuguese);

    private void ShowStatus(string message)
    {
        _details.Clear();
        _status.Text = message;
        _status.IsVisible = true;
    }
}
