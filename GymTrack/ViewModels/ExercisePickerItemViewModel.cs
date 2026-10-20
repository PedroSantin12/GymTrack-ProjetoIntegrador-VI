using CommunityToolkit.Mvvm.Input;
using GymTrack.Models;
using GymTrack.Services;

namespace GymTrack.ViewModels;

public sealed class ExercisePickerItemViewModel
{
    private readonly Exercise _exercise;

    public ExercisePickerItemViewModel(
        Exercise exercise,
        Action<Exercise> select,
        Action<Exercise> preview)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        ArgumentNullException.ThrowIfNull(select);
        ArgumentNullException.ThrowIfNull(preview);

        _exercise = exercise;
        SelectCommand = new RelayCommand(() => select(_exercise));
        PreviewCommand = new RelayCommand(() => preview(_exercise));
    }

    public int Id => _exercise.Id;

    public string Name => _exercise.Name;

    public string MuscleGroup => _exercise.MuscleGroup;

    public string? Notes => _exercise.Notes;

    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);

    public string PreviewSource => ExerciseMediaCatalog.ForExercise(Name, MuscleGroup);

    public IRelayCommand SelectCommand { get; }

    public IRelayCommand PreviewCommand { get; }
}
