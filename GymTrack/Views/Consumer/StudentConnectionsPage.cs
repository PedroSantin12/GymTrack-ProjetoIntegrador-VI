using GymTrack.Models;
using GymTrack.Services;
using GymTrack.Views.Components;

namespace GymTrack.Views.Consumer;

public sealed class StudentConnectionsPage : ContentPage
{
    private readonly StudentService _students;
    private readonly VerticalStackLayout _connections = new() { Spacing = 10 };
    private readonly Entry _email = new()
    {
        Placeholder = "E-mail do funcionário", Keyboard = Keyboard.Email, MaxLength = 254,
        AutomationId = "LinkEmployeeEmail"
    };
    private readonly Label _status = Ui.Text(string.Empty, 13, Ui.Accent);
    private bool _busy;

    public StudentConnectionsPage(StudentService students)
    {
        Title = "Vínculos com funcionários";
        _students = students;
        SemanticProperties.SetDescription(_email, "E-mail do funcionário");
        var link = Ui.Button("Vincular", () => ChangeAsync(async () =>
        {
            await _students.LinkAsync(_email.Text);
            _email.Text = string.Empty;
        }));
        link.AutomationId = "LinkEmployee";

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(18, 14), Spacing = 14, MaximumWidthRequest = 540,
                Children =
                {
                    Ui.Text("Quem pode atribuir treinos", 21, Ui.Foreground, FontAttributes.Bold),
                    Ui.Text("Vincule um funcionário pelo e-mail. O histórico é compartilhado somente se você autorizar.",
                        14, Ui.Muted),
                    Ui.Card(new VerticalStackLayout { Spacing = 8, Children = { _email, link } }),
                    _status,
                    _connections
                }
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
        try
        {
            var connections = await _students.GetConnectionsAsync();
            _connections.Clear();
            if (connections.Count == 0)
                _connections.Add(Ui.Text("Nenhum funcionário vinculado.", 13, Ui.Muted));
            foreach (var connection in connections)
                _connections.Add(ConnectionCard(connection));
        }
        catch
        {
            _connections.Clear();
            _status.Text = "Não foi possível carregar seus vínculos.";
        }
    }

    private Border ConnectionCard(StudentConnection connection)
    {
        var employee = connection.Employee;
        return Ui.Card(new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                Ui.Text(employee.Name, 17, Ui.Foreground, FontAttributes.Bold),
                Ui.Text(employee.Email, 13, Ui.Muted),
                Ui.Text(connection.SharesHistory ? "Histórico compartilhado" : "Histórico privado", 13, Ui.Accent),
                Ui.Button(connection.SharesHistory ? "Parar de compartilhar" : "Compartilhar histórico",
                    () => ChangeAsync(async () =>
                    {
                        if (!connection.SharesHistory && !await DisplayAlertAsync("Compartilhar histórico?",
                                $"{employee.Name} poderá ver nomes e datas dos seus treinos registrados.",
                                "Compartilhar", "Cancelar")) return;
                        await _students.SetHistorySharingAsync(employee.Id, !connection.SharesHistory);
                    }), true),
                Ui.Button("Remover vínculo", () => ChangeAsync(async () =>
                {
                    if (!await DisplayAlertAsync("Remover vínculo?",
                            "O funcionário perderá o acesso à sua ficha. Os treinos já atribuídos ficam na sua conta.",
                            "Remover", "Cancelar")) return;
                    await _students.UnlinkAsync(employee.Id);
                }), true)
            }
        });
    }

    private async Task ChangeAsync(Func<Task> change)
    {
        if (_busy) return;
        _busy = true;
        _status.Text = string.Empty;
        try
        {
            await change();
            await LoadAsync();
        }
        catch (Exception exception) when (exception is InvalidOperationException or UnauthorizedAccessException)
        {
            _status.Text = exception.Message;
        }
        catch
        {
            _status.Text = "Não foi possível atualizar o vínculo. Tente novamente.";
        }
        finally { _busy = false; }
    }
}
