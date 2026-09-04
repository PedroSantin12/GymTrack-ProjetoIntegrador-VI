using GymTrack.ViewModels;

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
        viewModel.SessionRequested += OnSessionRequested;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
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
