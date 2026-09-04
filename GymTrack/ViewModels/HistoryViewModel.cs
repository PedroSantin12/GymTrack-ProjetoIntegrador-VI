using System.Collections.ObjectModel;

namespace GymTrack.ViewModels;

public sealed class HistoryViewModel : BaseViewModel
{
    public HistoryViewModel()
    {
        Title = "Histórico";
    }

    public ObservableCollection<string> Sessions { get; } = [];
}
