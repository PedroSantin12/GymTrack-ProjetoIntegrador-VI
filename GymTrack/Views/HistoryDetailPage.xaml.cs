using GymTrack.ViewModels;
using GymTrack.Services;

namespace GymTrack.Views;

public partial class HistoryDetailPage : ContentPage
{
    private readonly HistoryDetailViewModel _viewModel;
    private readonly IAppNavigator _navigator;

    public HistoryDetailPage(HistoryDetailViewModel viewModel, IAppNavigator navigator)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
        _navigator = navigator;
        viewModel.ProgressRequested += OnProgressRequested;
    }

    public Task InitializeAsync(int sessionId) => _viewModel.InitializeAsync(sessionId);

    private async void OnProgressRequested(object? sender, EventArgs e)
    {
        await _navigator.NavigateAsync(AppSection.Progress);
    }
}
