using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GymTrack.Services;
using Microsoft.Extensions.Logging;

namespace GymTrack.ViewModels;

public partial class RegisterViewModel(IAccountService accounts, AppSession session,
    IAppNavigator navigator, ILogger<RegisterViewModel> logger) : ObservableObject
{
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string password = string.Empty;
    [ObservableProperty] private string confirmPassword = string.Empty;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private bool isBusy;

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (IsBusy) return;
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Email) ||
            string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(ConfirmPassword))
        {
            ErrorMessage = "Preencha todos os campos.";
            return;
        }

        if (Password != ConfirmPassword)
        {
            ErrorMessage = "As senhas não conferem.";
            return;
        }

        IsBusy = true;
        var accountCreated = false;
        try
        {
            var user = await accounts.RegisterConsumerAsync(Name, Email, Password);
            accountCreated = true;
            session.SignIn(user);
            await navigator.OpenWorkspaceAsync();
            Password = string.Empty;
            ConfirmPassword = string.Empty;
        }
        catch (ArgumentException exception)
        {
            ErrorMessage = exception.Message.Split('\n', StringSplitOptions.TrimEntries)[0];
        }
        catch (InvalidOperationException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (Exception exception)
        {
            session.SignOut();
            logger.LogError(exception, "Falha ao cadastrar a conta local.");
            ErrorMessage = accountCreated
                ? "Conta criada. Volte e entre com seu e-mail e sua senha."
                : "Não foi possível cadastrar. Tente novamente.";
        }
        finally { IsBusy = false; }
    }
}
