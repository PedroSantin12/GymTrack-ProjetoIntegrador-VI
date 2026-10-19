using GymTrack.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GymTrack.Views;

public partial class LoginPage : ContentPage
{
    private readonly LoginViewModel _viewModel;
    private readonly IServiceProvider _services;

    public LoginPage(LoginViewModel viewModel, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _services = services;
        SizeChanged += (_, _) => BrandLogo.IsVisible = Height >= 460;
    }

    private void OnEmailCompleted(object? sender, EventArgs e) => PasswordInput.Focus();

    private async void OnRegisterClicked(object? sender, EventArgs e)
    {
        if (_viewModel.IsBusy) return;
        await Navigation.PushAsync(_services.GetRequiredService<RegisterPage>());
    }
}
