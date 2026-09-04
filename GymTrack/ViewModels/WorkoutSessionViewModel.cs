using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GymTrack.Data.Dao;
using GymTrack.Models;
using GymTrack.Services;
using Microsoft.Extensions.Logging;

namespace GymTrack.ViewModels;

public partial class WorkoutSessionViewModel : BaseViewModel
{
    private readonly IWorkoutDao _workoutDao;
    private readonly IExerciseDao _exerciseDao;
    private readonly ISessionDao _sessionDao;
    private readonly IDialogService _dialogService;
    private readonly INotificationService _notificationService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WorkoutSessionViewModel> _logger;
    private WorkoutSession? _session;
    private int _closeRequested;

    public WorkoutSessionViewModel(
        IWorkoutDao workoutDao,
        IExerciseDao exerciseDao,
        ISessionDao sessionDao,
        IDialogService dialogService,
        INotificationService notificationService,
        TimeProvider timeProvider,
        ILogger<WorkoutSessionViewModel> logger)
    {
        _workoutDao = workoutDao;
        _exerciseDao = exerciseDao;
        _sessionDao = sessionDao;
        _dialogService = dialogService;
        _notificationService = notificationService;
        _timeProvider = timeProvider;
        _logger = logger;
        Title = "Executar treino";
    }

    public event EventHandler? CloseRequested;

    public ObservableCollection<SessionExerciseItemViewModel> Exercises { get; } = [];

    public int SessionId => _session?.Id ?? 0;

    [ObservableProperty]
    private string workoutName = string.Empty;

    [ObservableProperty]
    private string elapsedTimeText = "00:00:00";

    [ObservableProperty]
    private double progress;

    [ObservableProperty]
    private string progressText = "0 de 0 séries concluídas";

    [ObservableProperty]
    private string volumeText = "0 kg de volume";

    [ObservableProperty]
    private int completedSetsCount;

    [ObservableProperty]
    private int totalSetsCount;

    [ObservableProperty]
    private bool hasProgress;

    [ObservableProperty]
    private bool isFinished;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationMessage))]
    private string? validationMessage;

    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

    public async Task<bool> InitializeAsync(int workoutId)
    {
        IsBusy = true;
        StatusMessage = null;
        ValidationMessage = null;
        IsFinished = false;
        Volatile.Write(ref _closeRequested, 0);
        Exercises.Clear();
        _session = null;

        try
        {
            var workout = await _workoutDao.GetByIdAsync(workoutId);
            if (workout is null)
            {
                await _dialogService.AlertAsync(
                    "Treino não encontrado",
                    "Não foi possível localizar este treino.",
                    "Entendi");
                return false;
            }

            var composition = await _workoutDao.GetExercisesAsync(workoutId);
            if (composition.Count == 0)
            {
                await _dialogService.AlertAsync(
                    "Treino incompleto",
                    "Adicione ao menos um exercício para iniciar o treino.",
                    "Entendi");
                return false;
            }

            var activeSession = await _sessionDao.GetActiveAsync();
            if (activeSession is not null && activeSession.WorkoutId != workoutId)
            {
                await _dialogService.AlertAsync(
                    "Sessão em andamento",
                    "Finalize ou retome o treino em andamento antes de iniciar outro.",
                    "Entendi");
                return false;
            }

            var isNewSession = activeSession is null;
            _session = activeSession ?? new WorkoutSession
            {
                WorkoutId = workoutId,
                StartedAt = UtcNow()
            };

            if (isNewSession && await _sessionDao.InsertAsync(_session) != 1)
            {
                throw new InvalidOperationException("A sessão não foi criada.");
            }

            var catalog = await _exerciseDao.GetAllAsync(includeInactive: true);
            var exercisesById = catalog.ToDictionary(exercise => exercise.Id);
            var savedSets = await _sessionDao.GetSetsAsync(_session.Id);

            WorkoutName = workout.Name;
            Title = workout.Name;

            foreach (var plan in composition.OrderBy(item => item.OrderIndex))
            {
                if (!exercisesById.TryGetValue(plan.ExerciseId, out var exercise))
                {
                    throw new InvalidOperationException(
                        $"O exercício {plan.ExerciseId} não foi encontrado.");
                }

                var previousSets = await _sessionDao.GetPreviousSetsAsync(
                    workoutId,
                    exercise.Id,
                    _session.Id);
                var exerciseItem = new SessionExerciseItemViewModel(
                    exercise,
                    plan,
                    previousSets,
                    AddSet);
                var savedByNumber = savedSets
                    .Where(record => record.ExerciseId == exercise.Id)
                    .ToDictionary(record => record.SetNumber);
                var rowCount = Math.Max(
                    plan.PlannedSets,
                    savedByNumber.Keys.DefaultIfEmpty(0).Max());

                for (var setNumber = 1; setNumber <= rowCount; setNumber++)
                {
                    savedByNumber.TryGetValue(setNumber, out var savedRecord);
                    exerciseItem.Sets.Add(CreateSetItem(
                        exerciseItem,
                        setNumber,
                        savedRecord));
                }

                Exercises.Add(exerciseItem);
                UpdateRemovableSets(exerciseItem);
            }

            UpdateMetrics();
            RefreshElapsedTime();

            if (isNewSession)
            {
                await TryNotifyAsync("Sessão iniciada.");
            }

            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao iniciar treino {WorkoutId}.", workoutId);
            ValidationMessage = "Não foi possível iniciar o treino. Tente novamente.";
            await TryNotifyAsync(ValidationMessage);
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void RefreshElapsedTime()
    {
        if (_session is null)
        {
            ElapsedTimeText = "00:00:00";
            return;
        }

        var startedAt = AsUtc(_session.StartedAt);
        var endedAt = _session.FinishedAt is DateTime finishedAt
            ? AsUtc(finishedAt)
            : UtcNow();
        var elapsed = endedAt - startedAt;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        ElapsedTimeText = $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
    }

    [RelayCommand]
    private async Task FinishAsync()
    {
        if (IsBusy || IsFinished || _session is null)
        {
            return;
        }

        var completedSeriesText = CompletedSetsCount == 1
            ? "1 série concluída"
            : $"{CompletedSetsCount} séries concluídas";
        var message = CompletedSetsCount == 0
            ? "Nenhuma série foi concluída. Finalizar e salvar esta sessão vazia?"
            : $"Finalizar com {completedSeriesText} e {FormatVolume(CalculateVolume())} kg de volume?";
        var confirmed = await _dialogService.ConfirmAsync(
            CompletedSetsCount == 0 ? "Finalizar treino vazio?" : "Finalizar treino?",
            message,
            "Finalizar",
            "Continuar");
        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        ValidationMessage = null;

        try
        {
            _session.FinishedAt = UtcNow();
            if (await _sessionDao.UpdateAsync(_session) != 1)
            {
                throw new InvalidOperationException("A sessão não foi finalizada.");
            }

            IsFinished = true;
            RefreshElapsedTime();
            var bestLoad = Exercises
                .SelectMany(exercise => exercise.Sets)
                .Where(set => set.IsCompleted)
                .Select(set => set.CompletedLoadKg ?? 0)
                .DefaultIfEmpty(0)
                .Max();
            var bestLoadText = bestLoad > 0
                ? $"\nMaior carga da sessão: {bestLoad.ToString("0.##", CultureInfo.CurrentCulture)} kg."
                : string.Empty;

            await _dialogService.AlertAsync(
                "Treino finalizado",
                $"Duração: {ElapsedTimeText}.\nSéries: {CompletedSetsCount}.\nVolume: {FormatVolume(CalculateVolume())} kg.{bestLoadText}",
                "Concluir");
            await TryNotifyAsync("Sessão salva no histórico.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao finalizar sessão {SessionId}.", _session.Id);
            _session.FinishedAt = null;
            ValidationMessage = "Não foi possível finalizar o treino. Tente novamente.";
            await TryNotifyAsync(ValidationMessage);
        }
        finally
        {
            IsBusy = false;
        }

        if (IsFinished)
        {
            RequestClose();
        }
    }

    [RelayCommand]
    private async Task RequestExitAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (IsFinished)
        {
            RequestClose();
            return;
        }

        var confirmed = await _dialogService.ConfirmAsync(
            "Sair do treino?",
            "As séries concluídas ficam salvas e você poderá retomar esta sessão depois.",
            "Sair",
            "Continuar treino");
        if (confirmed)
        {
            RequestClose();
        }
    }

    private SessionSetItemViewModel CreateSetItem(
        SessionExerciseItemViewModel exercise,
        int setNumber,
        SetRecord? savedRecord)
    {
        var previous = exercise.GetPreviousSet(setNumber);
        var item = new SessionSetItemViewModel(
            setNumber,
            previous is null ? "—" : $"{previous.LoadKg:0.##} x {previous.Reps}",
            exercise.PlannedLoad?.ToString("0.##", CultureInfo.CurrentCulture) ?? "0",
            exercise.PlannedReps.ToString(CultureInfo.InvariantCulture),
            set => CompleteSetAsync(exercise, set),
            set => RemoveSet(exercise, set));

        if (savedRecord is not null)
        {
            item.MarkCompleted(savedRecord);
        }

        return item;
    }

    private void AddSet(SessionExerciseItemViewModel exercise)
    {
        if (IsBusy || IsFinished)
        {
            return;
        }

        var setNumber = exercise.Sets.Count + 1;
        exercise.Sets.Add(CreateSetItem(exercise, setNumber, savedRecord: null));
        UpdateRemovableSets(exercise);
        UpdateMetrics();
    }

    private void RemoveSet(
        SessionExerciseItemViewModel exercise,
        SessionSetItemViewModel set)
    {
        if (IsBusy || IsFinished || set.IsCompleted || exercise.Sets.LastOrDefault() != set)
        {
            return;
        }

        exercise.Sets.Remove(set);
        UpdateRemovableSets(exercise);
        UpdateMetrics();
    }

    private async Task CompleteSetAsync(
        SessionExerciseItemViewModel exercise,
        SessionSetItemViewModel set)
    {
        if (_session is null || IsFinished || set.IsCompleted || set.IsBusy)
        {
            return;
        }

        if (!TryParseLoad(set.LoadText, out var load))
        {
            set.ValidationMessage = "Informe uma carga válida e não negativa.";
            return;
        }

        if (!int.TryParse(
                (set.RepsText ?? string.Empty).Trim(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var reps) || reps <= 0)
        {
            set.ValidationMessage = "Informe uma quantidade positiva de repetições.";
            return;
        }

        set.IsBusy = true;
        set.ValidationMessage = null;

        try
        {
            var record = new SetRecord
            {
                Id = set.Id,
                SessionId = _session.Id,
                ExerciseId = exercise.ExerciseId,
                SetNumber = set.SetNumber,
                Reps = reps,
                LoadKg = load
            };

            if (await _sessionDao.SaveSetAsync(record) != 1)
            {
                throw new InvalidOperationException("A série não foi salva.");
            }

            set.MarkCompleted(record);
            UpdateRemovableSets(exercise);
            UpdateMetrics();
            await TryNotifyAsync("Série concluída.");
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Falha ao salvar série {SetNumber} da sessão {SessionId}.",
                set.SetNumber,
                _session.Id);
            set.ValidationMessage = "Não foi possível salvar esta série. Tente novamente.";
            await TryNotifyAsync(set.ValidationMessage);
        }
        finally
        {
            set.IsBusy = false;
        }
    }

    private void UpdateRemovableSets(SessionExerciseItemViewModel exercise)
    {
        for (var index = 0; index < exercise.Sets.Count; index++)
        {
            var set = exercise.Sets[index];
            set.CanRemove = !set.IsCompleted && index == exercise.Sets.Count - 1;
        }
    }

    private void UpdateMetrics()
    {
        var sets = Exercises.SelectMany(exercise => exercise.Sets).ToList();
        TotalSetsCount = sets.Count;
        CompletedSetsCount = sets.Count(set => set.IsCompleted);
        HasProgress = TotalSetsCount > 0;
        Progress = TotalSetsCount == 0
            ? 0
            : (double)CompletedSetsCount / TotalSetsCount;
        ProgressText = $"{CompletedSetsCount} de {TotalSetsCount} séries concluídas";
        VolumeText = $"{FormatVolume(CalculateVolume())} kg de volume";
    }

    private double CalculateVolume()
    {
        return Exercises
            .SelectMany(exercise => exercise.Sets)
            .Where(set => set.IsCompleted)
            .Sum(set => (set.CompletedLoadKg ?? 0) * (set.CompletedReps ?? 0));
    }

    private static bool TryParseLoad(string? value, out double load)
    {
        var normalized = value?.Trim();
        var parsed = double.TryParse(
                normalized,
                NumberStyles.Float,
                CultureInfo.CurrentCulture,
                out load) ||
            double.TryParse(
                normalized?.Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out load);
        return parsed && double.IsFinite(load) && load >= 0;
    }

    private string FormatVolume(double volume)
    {
        return volume.ToString("0.##", CultureInfo.CurrentCulture);
    }

    private DateTime UtcNow()
    {
        return _timeProvider.GetUtcNow().UtcDateTime;
    }

    private static DateTime AsUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
    }

    private void RequestClose()
    {
        if (Interlocked.Exchange(ref _closeRequested, 1) == 0)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task TryNotifyAsync(string message)
    {
        try
        {
            await _notificationService.ShowAsync(message);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "O aviso ao usuário não pôde ser exibido.");
        }
    }
}
