using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GymTrack.Data.Dao;
using GymTrack.Models;
using Microsoft.Extensions.Logging;

namespace GymTrack.ViewModels;

public sealed record WorkoutFilterOption(int? Id, string Name)
{
    public override string ToString() => Name;
}

public sealed record PeriodFilterOption(string Name, int? Days, bool IsCustom = false)
{
    public override string ToString() => Name;
}

public sealed class HistorySessionRequestedEventArgs(int sessionId) : EventArgs
{
    public int SessionId { get; } = sessionId;
}

public sealed class HistorySessionItemViewModel(SessionSummary summary)
{
    public int Id => summary.Id;
    public string WorkoutName => summary.WorkoutName;
    public string DateText => summary.StartedAt.ToLocalTime()
        .ToString("dd/MM/yyyy 'às' HH:mm", CultureInfo.GetCultureInfo("pt-BR"));
    public string DurationText => FormatDuration(summary.StartedAt, summary.FinishedAt);
    public string VolumeText => $"{summary.TotalVolume:N0} kg";
    public string SetCountText => summary.SetCount == 1 ? "1 série" : $"{summary.SetCount} séries";

    internal static string FormatDuration(DateTime startedAt, DateTime? finishedAt)
    {
        var duration = (finishedAt ?? startedAt) - startedAt;
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}h {duration.Minutes:D2}min"
            : $"{Math.Max(1, (int)Math.Ceiling(duration.TotalMinutes))} min";
    }
}

public partial class HistoryViewModel : BaseViewModel
{
    private readonly ISessionDao _sessionDao;
    private readonly IWorkoutDao _workoutDao;
    private readonly ILogger<HistoryViewModel> _logger;
    private int _latestLoadRequest;

    public HistoryViewModel(
        ISessionDao sessionDao,
        IWorkoutDao workoutDao,
        ILogger<HistoryViewModel> logger)
    {
        _sessionDao = sessionDao;
        _workoutDao = workoutDao;
        _logger = logger;
        Title = "Histórico";
        Periods.Add(new("Todo o período", null));
        Periods.Add(new("Últimos 7 dias", 7));
        Periods.Add(new("Últimos 30 dias", 30));
        Periods.Add(new("Últimos 90 dias", 90));
        Periods.Add(new("Período personalizado", null, true));
        SelectedPeriod = Periods[0];
        StartDate = DateTime.Today.AddMonths(-1);
        EndDate = DateTime.Today;
    }

    public event EventHandler<HistorySessionRequestedEventArgs>? SessionRequested;
    public ObservableCollection<HistorySessionItemViewModel> Sessions { get; } = [];
    public ObservableCollection<WorkoutFilterOption> Workouts { get; } = [];
    public ObservableCollection<PeriodFilterOption> Periods { get; } = [];

    [ObservableProperty]
    private WorkoutFilterOption? selectedWorkout;

    [ObservableProperty]
    private PeriodFilterOption? selectedPeriod;

    [ObservableProperty]
    private DateTime startDate;

    [ObservableProperty]
    private DateTime endDate;

    [ObservableProperty]
    private HistorySessionItemViewModel? selectedSession;

    public bool HasSessions => Sessions.Count > 0;
    public bool IsEmpty => !IsBusy && !HasSessions;
    public bool IsCustomPeriod => SelectedPeriod?.IsCustom == true;

    partial void OnSelectedPeriodChanged(PeriodFilterOption? value) =>
        OnPropertyChanged(nameof(IsCustomPeriod));

    partial void OnSelectedSessionChanged(HistorySessionItemViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        SelectedSession = null;
        SessionRequested?.Invoke(this, new HistorySessionRequestedEventArgs(value.Id));
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        var request = Interlocked.Increment(ref _latestLoadRequest);
        IsBusy = true;
        StatusMessage = null;

        try
        {
            if (Workouts.Count == 0)
            {
                var workouts = await _workoutDao.GetAllAsync();
                Workouts.Add(new(null, "Todos os treinos"));
                foreach (var workout in workouts.OrderBy(workout => workout.Name))
                {
                    Workouts.Add(new(workout.Id, workout.Name));
                }

                SelectedWorkout = Workouts[0];
            }

            var from = SelectedPeriod?.IsCustom == true
                ? StartDate.Date.ToUniversalTime()
                : SelectedPeriod?.Days is int days
                    ? DateTime.Now.Date.AddDays(-(days - 1)).ToUniversalTime()
                    : (DateTime?)null;
            var until = SelectedPeriod?.IsCustom == true
                ? EndDate.Date.AddDays(1).ToUniversalTime()
                : (DateTime?)null;
            if (SelectedPeriod?.IsCustom == true && EndDate.Date < StartDate.Date)
            {
                StatusMessage = "A data final deve ser igual ou posterior à inicial.";
                Sessions.Clear();
                OnPropertyChanged(nameof(HasSessions));
                OnPropertyChanged(nameof(IsEmpty));
                return;
            }

            var summaries = await _sessionDao.GetSummariesAsync(SelectedWorkout?.Id, from, until);
            if (request != Volatile.Read(ref _latestLoadRequest))
            {
                return;
            }

            Sessions.Clear();
            foreach (var summary in summaries)
            {
                Sessions.Add(new(summary));
            }

            OnPropertyChanged(nameof(HasSessions));
            OnPropertyChanged(nameof(IsEmpty));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao carregar o histórico.");
            StatusMessage = "Não foi possível carregar o histórico.";
        }
        finally
        {
            if (request == Volatile.Read(ref _latestLoadRequest))
            {
                IsBusy = false;
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
    }
}
