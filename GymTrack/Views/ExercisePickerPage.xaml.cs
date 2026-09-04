using GymTrack.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GymTrack.Views;

public partial class ExercisePickerPage : ContentPage
{
    private readonly ExercisePickerViewModel _viewModel;
    private readonly IServiceProvider _services;
    private readonly ILogger<ExercisePickerPage> _logger;
    private bool _hasAppeared;
    private bool _isClosing;
    private bool _isOpeningManager;
    private bool _isInitialized;

    public ExercisePickerPage(
        ExercisePickerViewModel viewModel,
        IServiceProvider services,
        ILogger<ExercisePickerPage> logger)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
        _services = services;
        _logger = logger;

        viewModel.ExerciseSelected += OnExerciseSelected;
        viewModel.ManageExercisesRequested += OnManageExercisesRequested;
        viewModel.CloseRequested += OnCloseRequested;
    }

    public event EventHandler<ExercisePickerSelectionEventArgs>? ExerciseSelected;

    public event EventHandler? ManageExercisesRequested;

    public async Task InitializeAsync(IReadOnlyCollection<int> excludedIds)
    {
        await _viewModel.InitializeAsync(excludedIds);
        _isInitialized = true;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_isInitialized)
        {
            return;
        }

        if (!_hasAppeared)
        {
            _hasAppeared = true;
            return;
        }

        try
        {
            await _viewModel.LoadCommand.ExecuteAsync(null);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao atualizar o seletor de exercícios.");
            _viewModel.StatusMessage = "Não foi possível atualizar os exercícios.";
        }
    }

    private async void OnExerciseSelected(
        object? sender,
        ExercisePickerSelectionEventArgs e)
    {
        ExerciseSelected?.Invoke(this, e);
        await CloseAsync();
    }

    private async void OnManageExercisesRequested(object? sender, EventArgs e)
    {
        if (_isOpeningManager)
        {
            return;
        }

        _isOpeningManager = true;
        ManageExercisesRequested?.Invoke(this, EventArgs.Empty);

        try
        {
            var exercisesPage = _services.GetRequiredService<ExercisesPage>();

            if (Parent is NavigationPage || Navigation.NavigationStack.Contains(this))
            {
                await Navigation.PushAsync(exercisesPage);
                return;
            }

            var managerNavigation = new NavigationPage(exercisesPage);
            var closeItem = new ToolbarItem { Text = "Fechar" };
            closeItem.Clicked += async (_, _) => await Navigation.PopModalAsync();
            exercisesPage.ToolbarItems.Add(closeItem);
            await Navigation.PushModalAsync(managerNavigation);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao abrir o gerenciamento de exercícios.");
            _viewModel.StatusMessage = "Não foi possível abrir o gerenciamento de exercícios.";
        }
        finally
        {
            _isOpeningManager = false;
        }
    }

    private async void OnCloseRequested(object? sender, EventArgs e)
    {
        await CloseAsync();
    }

    private async Task CloseAsync()
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;

        try
        {
            if (Navigation.ModalStack.Contains(this) ||
                (Parent is NavigationPage && Navigation.ModalStack.Count > 0))
            {
                await Navigation.PopModalAsync();
            }
            else if (Navigation.NavigationStack.Contains(this) &&
                     Navigation.NavigationStack.Count > 1)
            {
                await Navigation.PopAsync();
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao fechar o seletor de exercícios.");
            _viewModel.StatusMessage = "Não foi possível fechar o seletor.";
        }
        finally
        {
            _isClosing = false;
        }
    }
}
