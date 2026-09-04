using GymTrack.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GymTrack.Views;

public partial class ExercisesPage : ContentPage
{
    private readonly ExercisesViewModel _viewModel;
    private readonly IServiceProvider _services;
    private readonly ILogger<ExercisesPage> _logger;
    private bool _isOpeningEditor;

    public ExercisesPage(
        ExercisesViewModel viewModel,
        IServiceProvider services,
        ILogger<ExercisesPage> logger)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
        _services = services;
        _logger = logger;

        viewModel.EditorRequested += OnEditorRequested;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private async void OnEditorRequested(
        object? sender,
        ExerciseEditorRequestedEventArgs e)
    {
        if (_isOpeningEditor)
        {
            return;
        }

        _isOpeningEditor = true;

        try
        {
            var editor = _services.GetRequiredService<ExerciseEditPage>();
            await editor.InitializeAsync(e.ExerciseId);
            await Navigation.PushModalAsync(editor);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao abrir o editor de exercício.");
            _viewModel.StatusMessage = "Não foi possível abrir o editor de exercício.";
        }
        finally
        {
            _isOpeningEditor = false;
        }
    }
}
