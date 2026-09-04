using GymTrack.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GymTrack.Views;

public partial class WorkoutsPage : ContentPage
{
    private readonly WorkoutsViewModel _viewModel;
    private readonly IServiceProvider _services;
    private readonly ILogger<WorkoutsPage> _logger;
    private bool _isNavigating;

    public WorkoutsPage(
        WorkoutsViewModel viewModel,
        IServiceProvider services,
        ILogger<WorkoutsPage> logger)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
        _services = services;
        _logger = logger;

        viewModel.EditorRequested += OnEditorRequested;
        viewModel.ManageExercisesRequested += OnManageExercisesRequested;
        viewModel.StartRequested += OnStartRequested;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private async void OnEditorRequested(
        object? sender,
        WorkoutEditorRequestedEventArgs e)
    {
        if (_isNavigating)
        {
            return;
        }

        _isNavigating = true;

        try
        {
            var editor = _services.GetRequiredService<WorkoutEditPage>();
            await editor.InitializeAsync(e.WorkoutId);
            await Navigation.PushAsync(editor);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao abrir o editor de treino.");
            _viewModel.StatusMessage = "Não foi possível abrir o editor de treino.";
        }
        finally
        {
            _isNavigating = false;
        }
    }

    private async void OnManageExercisesRequested(object? sender, EventArgs e)
    {
        if (_isNavigating || Navigation.NavigationStack.LastOrDefault() is ExercisesPage)
        {
            return;
        }

        _isNavigating = true;

        try
        {
            await Navigation.PushAsync(_services.GetRequiredService<ExercisesPage>());
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao abrir o gerenciamento de exercícios.");
            _viewModel.StatusMessage = "Não foi possível abrir os exercícios.";
        }
        finally
        {
            _isNavigating = false;
        }
    }

    private async void OnStartRequested(
        object? sender,
        WorkoutStartRequestedEventArgs e)
    {
        if (_isNavigating)
        {
            return;
        }

        _isNavigating = true;

        try
        {
            var sessionPage = _services.GetRequiredService<WorkoutSessionPage>();
            if (await sessionPage.InitializeAsync(e.WorkoutId))
            {
                await Navigation.PushModalAsync(sessionPage);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao iniciar a sessão de treino.");
            _viewModel.StatusMessage = "Não foi possível iniciar o treino.";
        }
        finally
        {
            _isNavigating = false;
        }
    }
}
