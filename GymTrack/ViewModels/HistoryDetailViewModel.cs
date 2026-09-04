using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using GymTrack.Data.Dao;
using Microsoft.Extensions.Logging;

namespace GymTrack.ViewModels;

public sealed class HistorySetItemViewModel(int setNumber, int reps, double loadKg)
{
    public string NumberText => $"Série {setNumber}";
    public string PerformanceText => $"{loadKg:N1} kg × {reps} reps";
    public string VolumeText => $"{loadKg * reps:N1} kg";
}

public sealed class HistoryExerciseGroupViewModel(int exerciseId, string exerciseName, string muscleGroup)
{
    public int ExerciseId { get; } = exerciseId;
    public string ExerciseName { get; } = exerciseName;
    public string MuscleGroup { get; } = muscleGroup;
    public ObservableCollection<HistorySetItemViewModel> Sets { get; } = [];
}

public partial class HistoryDetailViewModel : BaseViewModel
{
    private readonly ISessionDao _sessionDao;
    private readonly ILogger<HistoryDetailViewModel> _logger;

    public HistoryDetailViewModel(ISessionDao sessionDao, ILogger<HistoryDetailViewModel> logger)
    {
        _sessionDao = sessionDao;
        _logger = logger;
        Title = "Detalhe da sessão";
    }

    public event EventHandler? ProgressRequested;
    public ObservableCollection<HistoryExerciseGroupViewModel> Exercises { get; } = [];
    public string WorkoutName { get; private set; } = string.Empty;
    public string DateText { get; private set; } = string.Empty;
    public string DurationText { get; private set; } = string.Empty;
    public string VolumeText { get; private set; } = string.Empty;
    public string SetCountText { get; private set; } = string.Empty;

    public async Task InitializeAsync(int sessionId)
    {
        IsBusy = true;
        StatusMessage = null;

        try
        {
            var session = await _sessionDao.GetByIdAsync(sessionId)
                ?? throw new InvalidOperationException("Sessão não encontrada.");
            var summary = (await _sessionDao.GetSummariesAsync()).First(item => item.Id == sessionId);
            var details = await _sessionDao.GetSetDetailsAsync(sessionId);

            WorkoutName = summary.WorkoutName;
            DateText = session.StartedAt.ToLocalTime()
                .ToString("dddd, dd 'de' MMMM 'de' yyyy 'às' HH:mm", CultureInfo.GetCultureInfo("pt-BR"));
            DurationText = HistorySessionItemViewModel.FormatDuration(session.StartedAt, session.FinishedAt);
            VolumeText = $"{summary.TotalVolume:N1} kg";
            SetCountText = summary.SetCount == 1 ? "1 série" : $"{summary.SetCount} séries";

            Exercises.Clear();
            foreach (var values in details.GroupBy(detail => new
            {
                detail.ExerciseId,
                detail.ExerciseName,
                detail.MuscleGroup,
                detail.ExerciseOrder
            }))
            {
                var group = new HistoryExerciseGroupViewModel(
                    values.Key.ExerciseId,
                    values.Key.ExerciseName,
                    values.Key.MuscleGroup);
                foreach (var set in values)
                {
                    group.Sets.Add(new(set.SetNumber, set.Reps, set.LoadKg));
                }

                Exercises.Add(group);
            }

            OnPropertyChanged(nameof(WorkoutName));
            OnPropertyChanged(nameof(DateText));
            OnPropertyChanged(nameof(DurationText));
            OnPropertyChanged(nameof(VolumeText));
            OnPropertyChanged(nameof(SetCountText));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao carregar a sessão {SessionId}.", sessionId);
            StatusMessage = "Não foi possível carregar esta sessão.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ViewProgress() => ProgressRequested?.Invoke(this, EventArgs.Empty);
}
