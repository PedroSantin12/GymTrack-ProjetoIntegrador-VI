using GymTrack.ViewModels;
using Microsoft.Extensions.Logging;

namespace GymTrack.Views;

public partial class WorkoutSessionPage : ContentPage
{
    private readonly WorkoutSessionViewModel _viewModel;
    private readonly ILogger<WorkoutSessionPage> _logger;
    private IDispatcherTimer? _timer;
    private bool _isClosing;

    public WorkoutSessionPage(
        WorkoutSessionViewModel viewModel,
        ILogger<WorkoutSessionPage> logger)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
        _logger = logger;

        viewModel.CloseRequested += OnCloseRequested;
    }

    public Task<bool> InitializeAsync(int workoutId)
    {
        return _viewModel.InitializeAsync(workoutId);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.RefreshElapsedTime();

        _timer ??= CreateTimer();
        _timer.Start();
    }

    protected override void OnDisappearing()
    {
        _timer?.Stop();
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        _ = _viewModel.RequestExitCommand.ExecuteAsync(null);
        return true;
    }

    private IDispatcherTimer CreateTimer()
    {
        var timer = Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.Tick += (_, _) => _viewModel.RefreshElapsedTime();
        return timer;
    }

    private async void OnCloseRequested(object? sender, EventArgs e)
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        _timer?.Stop();

        try
        {
            if (Navigation.ModalStack.Contains(this))
            {
                await Navigation.PopModalAsync();
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao fechar a sessão de treino.");
            _viewModel.ValidationMessage = "Não foi possível fechar a sessão.";
        }
        finally
        {
            _isClosing = false;
        }
    }
}
