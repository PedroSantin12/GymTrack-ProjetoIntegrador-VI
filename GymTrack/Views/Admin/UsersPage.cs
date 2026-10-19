using GymTrack.Models;
using GymTrack.Services;
using GymTrack.Views.Components;

namespace GymTrack.Views.Admin;

public sealed class UsersPage : WorkspacePage
{
    private readonly IAccountService _accounts;
    private readonly AppSession _session;
    private readonly VerticalStackLayout _users = new() { Spacing = 12 };
    private readonly Label _summary = Ui.Text("Carregando usuários…", 13, Ui.Muted);
    private readonly Label _status = Ui.Text(string.Empty, 13, Ui.Accent);
    private readonly SearchBar _search = new()
    {
        Placeholder = "Pesquisar por nome ou e-mail",
        BackgroundColor = Ui.Surface,
        TextColor = Ui.Foreground,
        PlaceholderColor = Ui.Muted,
        CancelButtonColor = Ui.Accent,
        FontSize = 14
    };
    private int _loadVersion;
    private bool _isEditing;

    public UsersPage(IAppNavigator navigator, AppSession session, IAccountService accounts)
        : base("Usuários", "Pessoas e acessos em um só lugar.", AppSection.Users, navigator, session)
    {
        _accounts = accounts;
        _session = session;
        _status.IsVisible = false;
        _search.TextChanged += OnSearchChanged;
        SetBody(new VerticalStackLayout
        {
            Spacing = 18,
            Children = { _search, _summary, _status, _users }
        });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadUsersAsync();
    }

    private async void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        await LoadUsersAsync();
    }

    private async Task<bool> LoadUsersAsync()
    {
        var version = ++_loadVersion;
        try
        {
            var users = await _accounts.GetUsersAsync(_search.Text);
            if (version != _loadVersion)
                return false;

            _users.Children.Clear();
            _summary.Text = users.Count == 1 ? "1 usuário encontrado" : $"{users.Count} usuários encontrados";
            foreach (var user in users)
                _users.Children.Add(UserCard(user));

            if (users.Count == 0)
            {
                _users.Children.Add(Ui.Card(new VerticalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        Ui.Text("Nenhum usuário encontrado", 18, Ui.Foreground, FontAttributes.Bold),
                        Ui.Text("Experimente outro nome ou e-mail na pesquisa.", 14, Ui.Muted)
                    }
                }));
            }

            _status.Text = string.Empty;
            _status.IsVisible = false;
            return true;
        }
        catch
        {
            if (version == _loadVersion)
            {
                _summary.Text = "Não foi possível carregar os usuários.";
                ShowStatus("Volte a esta página ou altere a pesquisa para tentar novamente.");
            }
            return false;
        }
    }

    private Border UserCard(AppUser user)
    {
        var isCurrent = _session.CurrentUser?.Id == user.Id;
        var details = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                Ui.Tag(user.Role == UserRole.Employee ? "FUNCIONÁRIO" : "CONSUMIDOR"),
                Ui.Text(user.Name, 20, Ui.Foreground, FontAttributes.Bold),
                Ui.Text(user.Email, 13, Ui.Muted),
                Ui.Text(user.IsActive ? "Conta ativa" : "Conta desativada", 12, user.IsActive ? Ui.Accent : Ui.Muted)
            }
        };

        if (isCurrent)
        {
            details.Children.Add(Ui.Text("Sua conta atual.", 12, Ui.Muted));
        }
        else
        {
            var actions = new Grid { ColumnSpacing = 10, Margin = new Thickness(0, 8, 0, 0) };
            actions.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            actions.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            actions.Add(Ui.Button("Editar", () => EditUserAsync(user), true));
            actions.Add(Ui.Button(user.IsActive ? "Desativar" : "Reativar", () => ToggleUserAsync(user), true), 1);
            details.Children.Add(actions);
        }

        return Ui.Card(details, new Thickness(18));
    }

    private async Task EditUserAsync(AppUser user)
    {
        if (_isEditing || _session.CurrentUser?.Id == user.Id)
            return;

        _isEditing = true;
        try
        {
            var name = await DisplayPromptAsync("Editar usuário", "Nome", "Continuar", "Cancelar",
                initialValue: user.Name, maxLength: 100);
            if (name is null)
                return;

            var email = await DisplayPromptAsync("Editar usuário", "E-mail", "Salvar", "Cancelar",
                initialValue: user.Email, maxLength: 254, keyboard: Keyboard.Email);
            if (email is null)
                return;

            var previousName = user.Name;
            var previousEmail = user.Email;
            user.Name = name.Trim();
            user.Email = email.Trim();
            try
            {
                await _accounts.UpdateUserAsync(user);
            }
            catch
            {
                user.Name = previousName;
                user.Email = previousEmail;
                throw;
            }

            if (await LoadUsersAsync())
                ShowStatus("Informações atualizadas.");
        }
        catch (ArgumentException exception)
        {
            ShowStatus(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            ShowStatus(exception.Message);
        }
        catch
        {
            ShowStatus("Não foi possível salvar o usuário. Tente novamente.");
        }
        finally
        {
            _isEditing = false;
        }
    }

    private async Task ToggleUserAsync(AppUser user)
    {
        if (_isEditing || _session.CurrentUser?.Id == user.Id)
            return;

        _isEditing = true;
        try
        {
            var action = user.IsActive ? "Desativar" : "Reativar";
            var message = user.IsActive
                ? $"{user.Name} perderá o acesso ao aplicativo. O histórico será mantido."
                : $"{user.Name} poderá acessar o aplicativo novamente.";
            if (!await DisplayAlertAsync($"{action} usuário?", message, action, "Cancelar"))
                return;

            user.IsActive = !user.IsActive;
            try
            {
                await _accounts.UpdateUserAsync(user);
            }
            catch
            {
                user.IsActive = !user.IsActive;
                throw;
            }

            if (await LoadUsersAsync())
                ShowStatus(user.IsActive ? "Conta reativada." : "Conta desativada.");
        }
        catch
        {
            ShowStatus("Não foi possível alterar o acesso. Tente novamente.");
        }
        finally
        {
            _isEditing = false;
        }
    }

    private void ShowStatus(string message)
    {
        _status.Text = message;
        _status.IsVisible = true;
    }
}
