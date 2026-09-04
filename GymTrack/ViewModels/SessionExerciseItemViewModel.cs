using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using GymTrack.Models;

namespace GymTrack.ViewModels;

public sealed class SessionExerciseItemViewModel
{
    private readonly IReadOnlyDictionary<int, SetRecord> _previousSets;

    public SessionExerciseItemViewModel(
        Exercise exercise,
        WorkoutExercise plan,
        IReadOnlyList<SetRecord> previousSets,
        Action<SessionExerciseItemViewModel> addSet)
    {
        ExerciseId = exercise.Id;
        Name = exercise.Name;
        MuscleGroup = exercise.MuscleGroup;
        PlannedReps = plan.PlannedReps;
        PlannedLoad = plan.PlannedLoad;
        _previousSets = previousSets.ToDictionary(record => record.SetNumber);
        AddSetCommand = new RelayCommand(() => addSet(this));

        var reference = previousSets.OrderBy(record => record.SetNumber).FirstOrDefault();
        PreviousPerformanceText = reference is null
            ? "Sem desempenho anterior"
            : $"Anterior: {reference.LoadKg:0.##} kg x {reference.Reps}";
    }

    public int ExerciseId { get; }

    public string Name { get; }

    public string MuscleGroup { get; }

    public int PlannedReps { get; }

    public double? PlannedLoad { get; }

    public string PreviousPerformanceText { get; }

    public ObservableCollection<SessionSetItemViewModel> Sets { get; } = [];

    public IRelayCommand AddSetCommand { get; }

    public SetRecord? GetPreviousSet(int setNumber)
    {
        return _previousSets.GetValueOrDefault(setNumber);
    }
}
