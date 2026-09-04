using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GymTrack.Models;

namespace GymTrack.ViewModels;

public partial class WorkoutExerciseItemViewModel : ObservableObject
{
    public WorkoutExerciseItemViewModel(
        Exercise exercise,
        WorkoutExercise? plan,
        Action<WorkoutExerciseItemViewModel> moveUp,
        Action<WorkoutExerciseItemViewModel> moveDown,
        Action<WorkoutExerciseItemViewModel> remove)
    {
        ExerciseId = exercise.Id;
        Name = exercise.Name;
        MuscleGroup = exercise.MuscleGroup;
        IsActive = exercise.IsActive;
        PlannedSets = plan?.PlannedSets ?? 3;
        PlannedReps = (plan?.PlannedReps ?? 10).ToString(CultureInfo.InvariantCulture);
        PlannedLoad = plan?.PlannedLoad?.ToString("0.##", CultureInfo.CurrentCulture) ?? string.Empty;
        MoveUpCommand = new RelayCommand(() => moveUp(this));
        MoveDownCommand = new RelayCommand(() => moveDown(this));
        RemoveCommand = new RelayCommand(() => remove(this));
    }

    public int ExerciseId { get; }

    public string Name { get; }

    public string MuscleGroup { get; }

    public bool IsActive { get; }

    public string StateText => IsActive ? string.Empty : "Arquivado";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PositionText))]
    private int position;

    [ObservableProperty]
    private double plannedSets;

    [ObservableProperty]
    private string plannedReps = string.Empty;

    [ObservableProperty]
    private string plannedLoad = string.Empty;

    [ObservableProperty]
    private bool canMoveUp;

    [ObservableProperty]
    private bool canMoveDown;

    public string PositionText => $"{Position}. {Name}";

    public IRelayCommand MoveUpCommand { get; }

    public IRelayCommand MoveDownCommand { get; }

    public IRelayCommand RemoveCommand { get; }

    public bool TryBuildModel(
        int orderIndex,
        out WorkoutExercise model,
        out string? validationMessage)
    {
        model = new WorkoutExercise
        {
            ExerciseId = ExerciseId,
            OrderIndex = orderIndex
        };

        if (PlannedSets is < 1 or > 20 || PlannedSets % 1 != 0)
        {
            validationMessage = $"{Name}: informe de 1 a 20 séries.";
            return false;
        }

        if (!int.TryParse(
                (PlannedReps ?? string.Empty).Trim(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var repetitions) ||
            repetitions is < 1 or > 100)
        {
            validationMessage = $"{Name}: informe de 1 a 100 repetições.";
            return false;
        }

        if (!TryParseOptionalLoad(PlannedLoad, out var load))
        {
            validationMessage = $"{Name}: informe uma carga válida e não negativa.";
            return false;
        }

        model.PlannedSets = (int)PlannedSets;
        model.PlannedReps = repetitions;
        model.PlannedLoad = load;
        validationMessage = null;
        return true;
    }

    private static bool TryParseOptionalLoad(string? value, out double? load)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            load = null;
            return true;
        }

        var parsed = double.TryParse(
                normalized,
                NumberStyles.Float,
                CultureInfo.CurrentCulture,
                out var number) ||
            double.TryParse(
                normalized.Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out number);

        if (!parsed || number < 0 || double.IsNaN(number) || double.IsInfinity(number))
        {
            load = null;
            return false;
        }

        load = number;
        return true;
    }
}
