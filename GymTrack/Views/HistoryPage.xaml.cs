using GymTrack.ViewModels;
using GymTrack.Views.Components;

namespace GymTrack.Views;

public partial class HistoryPage : ContentPage
{
    private readonly HistoryViewModel _viewModel;
    private readonly IServiceProvider _services;
    private bool _isNavigating;

    public HistoryPage(HistoryViewModel viewModel, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
        _services = services;

        var workout = Ui.Text("Todos os treinos");
        workout.SetBinding(Label.TextProperty, new Binding(
            $"{nameof(HistoryViewModel.SelectedWorkout)}.{nameof(WorkoutFilterOption.Name)}")
        {
            FallbackValue = "Todos os treinos"
        });
        WorkoutFilter.Content = Ui.OptionField("Treino", workout, ChooseWorkoutAsync, "FilterHistoryWorkout");

        var period = Ui.Text("Todo o período");
        period.SetBinding(Label.TextProperty, new Binding(
            $"{nameof(HistoryViewModel.SelectedPeriod)}.{nameof(PeriodFilterOption.Name)}")
        {
            FallbackValue = "Todo o período"
        });
        PeriodFilter.Content = Ui.OptionField("Período", period, ChoosePeriodAsync, "FilterHistoryPeriod");

        viewModel.SessionRequested += OnSessionRequested;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (AppDialogs.IsShowingFor(this)) return;
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private async Task ChooseWorkoutAsync()
    {
        if (_viewModel.IsBusy) return;
        var workout = await this.ChooseAppOptionAsync("Treino", "Cancelar", _viewModel.Workouts,
            _viewModel.SelectedWorkout, static option => option.Name);
        if (workout is not null)
            _viewModel.SelectedWorkout = workout;
    }

    private async Task ChoosePeriodAsync()
    {
        if (_viewModel.IsBusy) return;
        var period = await this.ChooseAppOptionAsync("Período", "Cancelar", _viewModel.Periods,
            _viewModel.SelectedPeriod, static option => option.Name);
        if (period is not null)
            _viewModel.SelectedPeriod = period;
    }

    private async void OnSessionRequested(object? sender, HistorySessionRequestedEventArgs e)
    {
        if (_isNavigating)
        {
            return;
        }

        _isNavigating = true;
        try
        {
            var page = _services.GetRequiredService<HistoryDetailPage>();
            await page.InitializeAsync(e.SessionId);
            await Navigation.PushAsync(page);
        }
        finally
        {
            _isNavigating = false;
        }
    }
}
