using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GymTrack.ViewModels;

public partial class ExercisesViewModel : BaseViewModel
{
    public ExercisesViewModel()
    {
        Title = "Exercícios";
    }

    public event EventHandler? AddExerciseRequested;

    public ObservableCollection<string> Exercises { get; } = [];

    public IReadOnlyList<string> MuscleGroups { get; } =
    [
        "Todos",
        "Peito",
        "Costas",
        "Pernas",
        "Ombros",
        "Braços",
        "Core"
    ];

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string selectedMuscleGroup = "Todos";

    [RelayCommand]
    private void AddExercise()
    {
        AddExerciseRequested?.Invoke(this, EventArgs.Empty);
    }
}
