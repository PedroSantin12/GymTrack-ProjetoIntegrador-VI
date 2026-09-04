using GymTrack.ViewModels;

namespace GymTrack.Views;

public partial class ExerciseEditPage : ContentPage
{
    public ExerciseEditPage(ExerciseEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        viewModel.CloseRequested += OnCloseRequested;
    }

    private async void OnCloseRequested(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}
