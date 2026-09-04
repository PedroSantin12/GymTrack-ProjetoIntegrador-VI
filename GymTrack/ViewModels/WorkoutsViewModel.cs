using System.Collections.ObjectModel;

namespace GymTrack.ViewModels;

public sealed class WorkoutsViewModel : BaseViewModel
{
    public WorkoutsViewModel()
    {
        Title = "Treinos";
    }

    public ObservableCollection<string> Workouts { get; } = [];
}
