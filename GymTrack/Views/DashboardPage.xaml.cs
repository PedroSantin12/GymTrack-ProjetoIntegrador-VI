using GymTrack.ViewModels;

namespace GymTrack.Views;

public partial class DashboardPage : ContentPage
{
    private readonly DashboardViewModel _viewModel;
    private readonly IServiceProvider _services;
    private bool _isNavigating;

    public DashboardPage(DashboardViewModel viewModel, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
        _services = services;
        viewModel.StartRequested += OnStartRequested;
        viewModel.WorkoutsRequested += OnWorkoutsRequested;
        viewModel.HistoryRequested += OnHistoryRequested;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private async void OnStartRequested(object? sender, WorkoutStartRequestedEventArgs e)
    {
        if (_isNavigating)
        {
            return;
        }

        _isNavigating = true;
        try
        {
            var page = _services.GetRequiredService<WorkoutSessionPage>();
            if (await page.InitializeAsync(e.WorkoutId))
            {
                await Navigation.PushModalAsync(page);
            }
        }
        finally
        {
            _isNavigating = false;
        }
    }

    private void OnWorkoutsRequested(object? sender, EventArgs e) => FindTabs()?.SelectWorkouts();

    private void OnHistoryRequested(object? sender, EventArgs e) => FindTabs()?.SelectHistory();

    private static MainTabbedPage? FindTabs() =>
        Application.Current?.Windows.FirstOrDefault()?.Page is MainFlyoutPage flyout
            ? flyout.Detail as MainTabbedPage
            : null;
}
