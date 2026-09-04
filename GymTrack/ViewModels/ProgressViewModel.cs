using CommunityToolkit.Mvvm.ComponentModel;

namespace GymTrack.ViewModels;

public partial class ProgressViewModel : BaseViewModel
{
    public ProgressViewModel()
    {
        Title = "Evolução";
    }

    [ObservableProperty]
    private bool isMaxLoadSelected = true;

    [ObservableProperty]
    private bool isVolumeSelected;

    partial void OnIsMaxLoadSelectedChanged(bool value)
    {
        if (value)
        {
            IsVolumeSelected = false;
        }
    }

    partial void OnIsVolumeSelectedChanged(bool value)
    {
        if (value)
        {
            IsMaxLoadSelected = false;
        }
    }
}
