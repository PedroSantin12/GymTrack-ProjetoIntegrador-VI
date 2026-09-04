using CommunityToolkit.Mvvm.Input;
using GymTrack.Models;

namespace GymTrack.ViewModels;

public sealed class WorkoutListItemViewModel
{
    private readonly WorkoutSummary _summary;

    public WorkoutListItemViewModel(
        WorkoutSummary summary,
        Action<WorkoutSummary> edit,
        Func<WorkoutSummary, Task> start,
        Func<WorkoutSummary, Task> showActions)
    {
        _summary = summary;
        EditCommand = new RelayCommand(() => edit(_summary));
        StartCommand = new AsyncRelayCommand(() => start(_summary));
        ActionsCommand = new AsyncRelayCommand(() => showActions(_summary));
    }

    public int Id => _summary.Id;

    public string Name => _summary.Name;

    public string? Description => _summary.Description;

    public int ExerciseCount => _summary.ExerciseCount;

    public string ExerciseCountText => ExerciseCount == 1
        ? "1 exercício"
        : $"{ExerciseCount} exercícios";

    public string LastSessionText => _summary.LastSessionAt is null
        ? "Ainda não executado"
        : $"Última execução: {AsLocalTime(_summary.LastSessionAt.Value):dd/MM/yyyy}";

    public IRelayCommand EditCommand { get; }

    public IAsyncRelayCommand StartCommand { get; }

    public IAsyncRelayCommand ActionsCommand { get; }

    private static DateTime AsLocalTime(DateTime value)
    {
        var utcValue = value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value;
        return utcValue.ToLocalTime();
    }
}
