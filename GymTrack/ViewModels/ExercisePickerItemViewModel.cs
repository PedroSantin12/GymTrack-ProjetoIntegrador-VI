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
        Action<Exercise> preview,
        bool canSelect = true)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        ArgumentNullException.ThrowIfNull(select);
        ArgumentNullException.ThrowIfNull(preview);

        _exercise = exercise;
        CanSelect = canSelect;
        SelectCommand = new RelayCommand(() => select(_exercise));
        PreviewCommand = new RelayCommand(() => preview(_exercise));
    }

    public int Id => _exercise.Id;

    public string Name => _exercise.Name;

    public string MuscleGroup => _exercise.MuscleGroup;

    public string? Notes => _exercise.Notes;

    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);

    public string? PreviewSource => ExerciseMediaCatalog.ForExercise(Name, MuscleGroup);

    public bool HasPreview => PreviewSource is not null;

    public bool HasNoPreview => !HasPreview;

    public bool CanSelect { get; }

    public IRelayCommand SelectCommand { get; }

    public IRelayCommand PreviewCommand { get; }
}
