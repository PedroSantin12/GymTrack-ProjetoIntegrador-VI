using GymTrack.ViewModels;

namespace GymTrack.Views;

public partial class WorkoutsPage : ContentPage
{
    public WorkoutsPage(WorkoutsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
