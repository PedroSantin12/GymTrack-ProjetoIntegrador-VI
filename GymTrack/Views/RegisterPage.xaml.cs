using GymTrack.ViewModels;

namespace GymTrack.Views;

public partial class RegisterPage : ContentPage
{
    private readonly RegisterViewModel _viewModel;

    public RegisterPage(RegisterViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        SizeChanged += (_, _) => BrandLogo.IsVisible = Height >= 460;
    }

    private void OnNameCompleted(object? sender, EventArgs e) => EmailInput.Focus();

    private void OnEmailCompleted(object? sender, EventArgs e) => PasswordInput.Focus();

    private void OnPasswordCompleted(object? sender, EventArgs e) => ConfirmPasswordInput.Focus();

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        if (_viewModel.IsBusy) return;
        await Navigation.PopAsync();
    }
}
