using GymTrack.ViewModels;

namespace GymTrack.Views;

public partial class HistoryDetailPage : ContentPage
{
    private readonly HistoryDetailViewModel _viewModel;

    public HistoryDetailPage(HistoryDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
        viewModel.ProgressRequested += OnProgressRequested;
    }

    public Task InitializeAsync(int sessionId) => _viewModel.InitializeAsync(sessionId);

    private async void OnProgressRequested(object? sender, EventArgs e)
    {
        await Navigation.PopToRootAsync();
        if (Application.Current?.Windows.FirstOrDefault()?.Page is MainFlyoutPage flyout &&
            flyout.Detail is MainTabbedPage tabs)
        {
            tabs.SelectProgress();
        }
    }
}
