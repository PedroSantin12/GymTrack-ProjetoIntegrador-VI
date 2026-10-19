using GymTrack.ViewModels;

namespace GymTrack.Views;

public partial class LoginPage : ContentPage
{
    private readonly LoginViewModel _viewModel;

    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    private void OnEmailCompleted(object? sender, EventArgs e) => PasswordInput.Focus();

    private void OnConsumerDemoClicked(object? sender, EventArgs e) => FillDemo("consumidor@gymtrack.app");

    private void OnEmployeeDemoClicked(object? sender, EventArgs e) => FillDemo("funcionario@gymtrack.app");

    private void FillDemo(string email)
    {
        if (_viewModel.IsBusy) return;
        _viewModel.Email = email;
        _viewModel.Password = "GymTrack123!";
        _viewModel.ErrorMessage = null;
    }
}
