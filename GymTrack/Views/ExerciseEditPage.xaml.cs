using GymTrack.ViewModels;
using Microsoft.Extensions.Logging;

namespace GymTrack.Views;

public partial class ExerciseEditPage : ContentPage
{
    private readonly ExerciseEditViewModel _viewModel;
    private readonly ILogger<ExerciseEditPage> _logger;
    private bool _isClosing;

    public ExerciseEditPage(
        ExerciseEditViewModel viewModel,
        ILogger<ExerciseEditPage> logger)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
        _logger = logger;

        viewModel.CloseRequested += OnCloseRequested;
    }

    public Task InitializeAsync(int? exerciseId)
    {
        return _viewModel.InitializeAsync(exerciseId);
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
            if (Navigation.ModalStack.Contains(this))
            {
                await Navigation.PopModalAsync();
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao fechar o editor de exercício.");
        }
        finally
        {
            _isClosing = false;
        }
    }
}
