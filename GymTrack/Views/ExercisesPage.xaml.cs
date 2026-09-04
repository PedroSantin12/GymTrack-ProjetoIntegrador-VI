using GymTrack.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GymTrack.Views;

public partial class ExercisesPage : ContentPage
{
    private readonly IServiceProvider _services;

    public ExercisesPage(
        ExercisesViewModel viewModel,
        IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _services = services;

        viewModel.AddExerciseRequested += OnAddExerciseRequested;
    }

    private async void OnAddExerciseRequested(object? sender, EventArgs e)
    {
        var editor = _services.GetRequiredService<ExerciseEditPage>();
        await Navigation.PushModalAsync(editor);
    }
}
