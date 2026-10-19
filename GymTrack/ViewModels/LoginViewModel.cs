using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GymTrack.Services;
using Microsoft.Extensions.Logging;

namespace GymTrack.ViewModels;

public partial class LoginViewModel(IAccountService accounts, AppSession session,
    IAppNavigator navigator, ILogger<LoginViewModel> logger) : ObservableObject
{
    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string password = string.Empty;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private bool isBusy;

    [RelayCommand]
    private async Task SignInAsync()
    {
        if (IsBusy) return;
        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Informe seu e-mail e sua senha.";
            return;
        }
        IsBusy = true;
        try
        {
            var user = await accounts.SignInAsync(Email, Password);
            if (user is null)
            {
                ErrorMessage = "E-mail ou senha inválidos, ou conta desativada.";
                return;
            }
            session.SignIn(user);
            await navigator.OpenWorkspaceAsync();
            Password = string.Empty;
        }
        catch (Exception exception)
        {
            session.SignOut();
            logger.LogError(exception, "Falha ao abrir a conta local.");
            ErrorMessage = "Não foi possível entrar. Tente novamente.";
        }
        finally { IsBusy = false; }
    }
}
