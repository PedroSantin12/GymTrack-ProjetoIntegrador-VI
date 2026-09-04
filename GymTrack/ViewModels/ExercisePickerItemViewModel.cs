using CommunityToolkit.Mvvm.Input;
using GymTrack.Models;

namespace GymTrack.ViewModels;

public sealed class ExercisePickerItemViewModel
{
    private readonly Exercise _exercise;

    public ExercisePickerItemViewModel(
        Exercise exercise,
        Action<Exercise> select)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        ArgumentNullException.ThrowIfNull(select);

        _exercise = exercise;
        SelectCommand = new RelayCommand(() => select(_exercise));
    }

    public int Id => _exercise.Id;

    public string Name => _exercise.Name;

    public string MuscleGroup => _exercise.MuscleGroup;

    public string? Notes => _exercise.Notes;

    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);

    public IRelayCommand SelectCommand { get; }
}
