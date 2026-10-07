using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GymTrack.Data.Dao;
using GymTrack.Models;
using Microsoft.Extensions.Logging;

namespace GymTrack.ViewModels;

public sealed class DashboardWorkoutItem(WorkoutSummary workout, bool hasActiveSession = false)
{
    public int Id => workout.Id;
    public string Name => workout.Name;
    public int ExerciseCount => workout.ExerciseCount;
    public bool CanStart => ExerciseCount > 0 || hasActiveSession;
    public string Description => workout.ExerciseCount == 1 ? "1 exercício" : $"{workout.ExerciseCount} exercícios";
    public override string ToString() => Name;
}

public partial class DashboardViewModel : BaseViewModel
{
    private readonly IWorkoutDao _workoutDao;
    private readonly ISessionDao _sessionDao;
    private readonly ILogger<DashboardViewModel> _logger;

    public DashboardViewModel(
        IWorkoutDao workoutDao,
        ISessionDao sessionDao,
        ILogger<DashboardViewModel> logger)
    {
        _workoutDao = workoutDao;
        _sessionDao = sessionDao;
        _logger = logger;
        Title = "Início";
    }

    public event EventHandler<WorkoutStartRequestedEventArgs>? StartRequested;
    public event EventHandler? WorkoutsRequested;
    public event EventHandler? HistoryRequested;

    public ObservableCollection<DashboardWorkoutItem> Workouts { get; } = [];
    public ObservableCollection<HistorySessionItemViewModel> RecentSessions { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartWorkoutCommand))]
    private DashboardWorkoutItem? selectedWorkout;

    public bool HasWorkouts => Workouts.Count > 0;
    public bool HasNoWorkouts => !HasWorkouts;
    public bool HasLastWorkout => RecentSessions.Count > 0;
    public bool HasRecentSessions => RecentSessions.Count > 0;
    public bool HasNoRecentSessions => !HasRecentSessions;
    public string LastWorkoutName => RecentSessions.FirstOrDefault()?.WorkoutName ?? "Nenhuma sessão concluída";
    public string LastWorkoutDetails => RecentSessions.FirstOrDefault() is { } last
        ? $"{last.DateText} • {last.DurationText}"
        : "Finalize um treino para acompanhar seu histórico.";
    public string WeeklySessionsText { get; private set; } = "0";
    public string WeeklyVolumeText { get; private set; } = "0 kg";

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            var workoutSummaries = await _workoutDao.GetSummariesAsync();
            var sessions = await _sessionDao.GetSummariesAsync();
            var activeSession = await _sessionDao.GetActiveAsync();
            Workouts.Clear();
            foreach (var workout in workoutSummaries)
            {
                Workouts.Add(new(workout, activeSession?.WorkoutId == workout.Id));
            }

            SelectedWorkout = Workouts.FirstOrDefault(item => item.CanStart) ?? Workouts.FirstOrDefault();
            RecentSessions.Clear();
            foreach (var session in sessions.Take(3))
            {
                RecentSessions.Add(new(session));
            }

            var today = DateTime.Now.Date;
            var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
            var weekStart = today.AddDays(-daysSinceMonday).ToUniversalTime();
            var weekly = sessions.Where(session => session.StartedAt >= weekStart).ToList();
            WeeklySessionsText = weekly.Count.ToString();
            WeeklyVolumeText = $"{weekly.Sum(session => session.TotalVolume):N0} kg";
            NotifyDashboardChanged();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao carregar o painel inicial.");
            StatusMessage = "Não foi possível carregar o resumo.";
        }
        finally
        {
            IsBusy = false;
            StartWorkoutCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanStartWorkout() => SelectedWorkout?.CanStart == true && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanStartWorkout))]
    private void StartWorkout()
    {
        if (SelectedWorkout is { } workout)
        {
            StartRequested?.Invoke(this, new WorkoutStartRequestedEventArgs(workout.Id));
        }
    }

    [RelayCommand]
    private void OpenWorkouts() => WorkoutsRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void OpenHistory() => HistoryRequested?.Invoke(this, EventArgs.Empty);

    private void NotifyDashboardChanged()
    {
        OnPropertyChanged(nameof(HasWorkouts));
        OnPropertyChanged(nameof(HasNoWorkouts));
        OnPropertyChanged(nameof(HasLastWorkout));
        OnPropertyChanged(nameof(HasRecentSessions));
        OnPropertyChanged(nameof(HasNoRecentSessions));
        OnPropertyChanged(nameof(LastWorkoutName));
        OnPropertyChanged(nameof(LastWorkoutDetails));
        OnPropertyChanged(nameof(WeeklySessionsText));
        OnPropertyChanged(nameof(WeeklyVolumeText));
        StartWorkoutCommand.NotifyCanExecuteChanged();
    }
}
