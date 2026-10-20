using GymTrack.Models;
using GymTrack.Services;
using GymTrack.Views.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;

namespace GymTrack.Views.Consumer;

public sealed class ProfilePage : WorkspacePage
{
    private readonly IAppNavigator _navigator;
    private readonly AppSession _session;
    private readonly IAccountService _accounts;
    private readonly IServiceProvider _services;
    private readonly VerticalStackLayout _content = new() { Spacing = 20 };
    private readonly Label _status = ConsumerUi.Status();
    private bool _busy;

    public ProfilePage(IAppNavigator navigator, AppSession session,
        IAccountService accounts, IServiceProvider services)
        : base("Meu perfil", "Sua conta e preferências.", AppSection.Profile, navigator, session)
    {
        _navigator = navigator;
        _session = session;
        _accounts = accounts;
        _services = services;
        SetBody(new VerticalStackLayout { Spacing = 12, Children = { _status, _content } });
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Render();
    }

    private void Render()
    {
        _content.Clear();
        if (_session.CurrentUser is not { } user) return;
        var role = _session.IsEmployee ? "Funcionário" : "Consumidor";

        var identity = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 12
        };
        var initial = string.IsNullOrWhiteSpace(user.Name) ? "?" : user.Name.Trim()[0].ToString().ToUpperInvariant();
        identity.Add(new Border
        {
            WidthRequest = 52, HeightRequest = 52, Padding = 0,
            BackgroundColor = Ui.Elevated,
            Stroke = new SolidColorBrush(Ui.Border), StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 26 },
            Content = new Label
            {
                Text = initial, FontSize = 21, FontAttributes = FontAttributes.Bold,
                TextColor = Ui.Accent,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        });
        var name = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
        name.Add(Ui.Text(user.Name, 18, Ui.Foreground, FontAttributes.Bold));
        name.Add(Ui.Text(user.Email, 12, Ui.Muted));
        identity.Add(name, 1);
        _content.Add(identity);

        _content.Add(Ui.Text("Informações da conta", 17, Ui.Foreground, FontAttributes.Bold));
        var account = new VerticalStackLayout { Spacing = 0 };
        account.Add(DetailRow("Nome", user.Name));
        account.Add(DetailRow("E-mail", user.Email));
        account.Add(DetailRow("Tipo de conta", role));
        _content.Add(account);

        _content.Add(Ui.Text("Configurações", 17, Ui.Foreground, FontAttributes.Bold));
        var settings = new VerticalStackLayout { Spacing = 0 };
        settings.Add(ActionRow("Editar perfil", EditProfileAsync));
        settings.Add(ActionRow("Sobre o GymTrack", async () =>
            await Navigation.PushAsync(_services.GetRequiredService<AboutPage>())));
        settings.Add(ActionRow("Sair da conta", SignOutAsync, true));
        _content.Add(settings);
    }

    private static View DetailRow(string label, string value)
    {
        var row = new Grid
        {
            Padding = new Thickness(0, 10),
            ColumnDefinitions = { new ColumnDefinition(new GridLength(104)), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 8
        };
        row.Add(Ui.Text(label, 12, Ui.Muted));
        var detail = Ui.Text(value, 13, Ui.Foreground);
        detail.LineBreakMode = LineBreakMode.TailTruncation;
        row.Add(detail, 1);
        return new VerticalStackLayout { Children = { row, Divider() } };
    }

    private static View ActionRow(string label, Func<Task> action, bool danger = false)
    {
        var row = new Grid { HeightRequest = 48,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
        row.Add(Ui.Text(label, 14, danger ? Color.FromArgb("#FF7474") : Ui.Foreground));
        row.Add(Ui.Text("›", 20, Ui.Muted), 1);
        var button = new Button
        {
            Text = string.Empty, BackgroundColor = Colors.Transparent,
            BorderWidth = 0, Padding = 0, CornerRadius = 0,
            AutomationId = label.Replace(" ", string.Empty)
        };
        SemanticProperties.SetDescription(button, label);
        button.Clicked += async (_, _) =>
        {
            if (!button.IsEnabled) return;
            button.IsEnabled = false;
            try { await action(); }
            finally { button.IsEnabled = true; }
        };
        row.Add(button);
        return new VerticalStackLayout { Children = { row, Divider() } };
    }

    private static View Divider() => new BoxView { HeightRequest = 1, BackgroundColor = Ui.Border };

    private async Task EditProfileAsync()
    {
        if (_busy || _session.CurrentUser is not { } user) return;
        var choice = await DisplayActionSheetAsync("Editar perfil", "Cancelar", null, "Alterar nome", "Alterar e-mail");
        if (choice is not ("Alterar nome" or "Alterar e-mail")) return;
        var changeName = choice == "Alterar nome";
        var value = await DisplayPromptAsync(choice, changeName ? "Nome" : "E-mail",
            "Salvar", "Cancelar", initialValue: changeName ? user.Name : user.Email,
            maxLength: changeName ? 100 : 254,
            keyboard: changeName ? Keyboard.Text : Keyboard.Email);
        if (value is null) return;
        value = value.Trim();
        if (changeName && value.Length < 2)
        {
            _status.Text = "Informe um nome com pelo menos 2 caracteres.";
            return;
        }
        _busy = true;
        try
        {
            var updated = new AppUser
            {
                Id = user.Id, Name = changeName ? value : user.Name,
                Email = changeName ? user.Email : value, Role = user.Role,
                IsActive = user.IsActive, CreatedAt = user.CreatedAt
            };
            await _accounts.UpdateUserAsync(updated);
            _session.SignIn(updated);
            _status.Text = "Perfil atualizado.";
            Render();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            _status.Text = exception.Message;
        }
        finally { _busy = false; }
    }

    private async Task SignOutAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            if (!await DisplayAlertAsync("Sair da conta?", "Você poderá entrar novamente com seu e-mail e sua senha.",
                    "Sair", "Continuar aqui")) return;
            _session.SignOut();
            await _navigator.ShowLoginAsync();
        }
        catch { _status.Text = "Não foi possível voltar ao login. Tente novamente."; }
        finally { _busy = false; }
    }
}
