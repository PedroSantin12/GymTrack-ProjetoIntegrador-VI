using System.Globalization;
using GymTrack.Services;
using GymTrack.Views.Components;

namespace GymTrack.Views.Consumer;

public sealed class ProfilePage : WorkspacePage
{
    private readonly IAppNavigator _navigator;
    private readonly AppSession _session;
    private readonly VerticalStackLayout _content = new() { Spacing = 20 };
    private readonly Label _status = ConsumerUi.Status();
    private bool _busy;

    public ProfilePage(IAppNavigator navigator, AppSession session)
        : base("Meu perfil", "Suas informações em um só lugar.", AppSection.Profile, navigator, session)
    {
        _navigator = navigator;
        _session = session;
        var body = new VerticalStackLayout { Spacing = 20 };
        body.Add(_content);
        body.Add(_status);
        body.Add(Ui.Button("Sair da conta", SignOutAsync, true));
        SetBody(body);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _content.Clear();
        if (_session.CurrentUser is not { } user) return;
        var role = _session.IsEmployee ? "Funcionário" : "Consumidor";

        var identity = new VerticalStackLayout { Spacing = 12 };
        identity.Add(Ui.Tag("SEU ESPAÇO"));
        identity.Add(Ui.Text(user.Name, 28, null, FontAttributes.Bold));
        identity.Add(Ui.Text($"Conta local • {role}", 14, Ui.Muted));
        _content.Add(Ui.Card(identity, new Thickness(22)));
        _content.Add(ConsumerUi.Section("Dados da conta", "As informações do seu cadastro no GymTrack."));

        var details = new VerticalStackLayout { Spacing = 22 };
        details.Add(ConsumerUi.Detail("Nome", user.Name));
        details.Add(ConsumerUi.Detail("E-mail", user.Email));
        details.Add(ConsumerUi.Detail("Tipo de acesso", role));
        if (user.CreatedAt != default)
        {
            details.Add(ConsumerUi.Detail("Membro desde", user.CreatedAt.ToLocalTime().ToString("dd 'de' MMMM 'de' yyyy", CultureInfo.GetCultureInfo("pt-BR"))));
        }
        _content.Add(Ui.Card(details));
        _content.Add(Ui.Text("Esta conta é mantida neste dispositivo. Seus treinos e seu histórico ficam vinculados ao seu perfil local.", 13, Ui.Muted));
    }

    private async Task SignOutAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            if (!await DisplayAlertAsync("Sair da conta?", "Você poderá entrar novamente com seu e-mail e sua senha.", "Sair", "Continuar aqui")) return;
            _session.SignOut();
            await _navigator.ShowLoginAsync();
        }
        catch
        {
            _status.Text = "Não foi possível voltar ao login. Tente novamente.";
        }
        finally
        {
            _busy = false;
        }
    }
}
