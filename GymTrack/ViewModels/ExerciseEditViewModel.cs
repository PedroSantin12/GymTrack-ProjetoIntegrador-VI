using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GymTrack.ViewModels;

public partial class ExerciseEditViewModel : BaseViewModel
{
    public ExerciseEditViewModel()
    {
        Title = "Novo exercício";
    }

    public event EventHandler? CloseRequested;

    public IReadOnlyList<string> MuscleGroups { get; } =
    [
        "Peito",
        "Costas",
        "Pernas",
        "Ombros",
        "Braços",
        "Core"
    ];

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string? selectedMuscleGroup;

    [ObservableProperty]
    private string notes = string.Empty;

    [ObservableProperty]
    private bool isActive = true;

    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
