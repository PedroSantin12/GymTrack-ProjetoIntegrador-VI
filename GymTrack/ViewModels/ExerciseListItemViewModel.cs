using CommunityToolkit.Mvvm.Input;
using GymTrack.Models;

namespace GymTrack.ViewModels;

public sealed class ExerciseListItemViewModel
{
    private readonly Exercise _exercise;

    public ExerciseListItemViewModel(
        Exercise exercise,
        Action<Exercise> edit,
        Func<Exercise, Task> showActions)
    {
        _exercise = exercise;
        EditCommand = new RelayCommand(() => edit(_exercise));
        ActionsCommand = new AsyncRelayCommand(() => showActions(_exercise));
    }

    public int Id => _exercise.Id;

    public string Name => _exercise.Name;

    public string MuscleGroup => _exercise.MuscleGroup;

    public bool IsActive => _exercise.IsActive;

    public string StateText => IsActive ? "Ativo" : "Arquivado";

    public IRelayCommand EditCommand { get; }

    public IAsyncRelayCommand ActionsCommand { get; }
}
