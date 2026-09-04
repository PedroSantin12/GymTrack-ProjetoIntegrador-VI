using GymTrack.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GymTrack.Views;

public partial class WorkoutEditPage : ContentPage
{
    private readonly WorkoutEditViewModel _viewModel;
    private readonly IServiceProvider _services;
    private readonly ILogger<WorkoutEditPage> _logger;
    private bool _isOpeningPicker;
    private bool _isClosing;

    public WorkoutEditPage(
        WorkoutEditViewModel viewModel,
        IServiceProvider services,
        ILogger<WorkoutEditPage> logger)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
        _services = services;
        _logger = logger;

        viewModel.AddExerciseRequested += OnAddExerciseRequested;
        viewModel.CloseRequested += OnCloseRequested;
    }

    public Task InitializeAsync(int? workoutId)
    {
        return _viewModel.InitializeAsync(workoutId);
    }

    private async void OnAddExerciseRequested(object? sender, EventArgs e)
    {
        if (_isOpeningPicker)
        {
            return;
        }

        _isOpeningPicker = true;

        try
        {
            var picker = _services.GetRequiredService<ExercisePickerPage>();
            await picker.InitializeAsync(_viewModel.SelectedExerciseIds);
            picker.ExerciseSelected += OnExerciseSelected;
            await Navigation.PushModalAsync(new NavigationPage(picker));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao abrir o seletor de exercícios.");
            _viewModel.ValidationMessage = "Não foi possível abrir o seletor de exercícios.";
        }
        finally
        {
            _isOpeningPicker = false;
        }
    }

    private void OnExerciseSelected(
        object? sender,
        ExercisePickerSelectionEventArgs e)
    {
        _viewModel.AddExercise(e.Exercise);

        if (sender is ExercisePickerPage picker)
        {
            picker.ExerciseSelected -= OnExerciseSelected;
        }
    }

    private async void OnCloseRequested(object? sender, EventArgs e)
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;

        try
        {
            if (Navigation.NavigationStack.Contains(this) &&
                Navigation.NavigationStack.Count > 1)
            {
                await Navigation.PopAsync();
            }
            else if (Navigation.ModalStack.Contains(this))
            {
                await Navigation.PopModalAsync();
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao fechar o editor de treino.");
            _viewModel.ValidationMessage = "Não foi possível fechar o editor.";
        }
        finally
        {
            _isClosing = false;
        }
    }
}
