using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GymTrack.Data.Dao;
using GymTrack.Models;
using GymTrack.Services;
using Microsoft.Extensions.Logging;

namespace GymTrack.ViewModels;

public sealed class ExercisePickerSelectionEventArgs(Exercise exercise) : EventArgs
{
    public Exercise Exercise { get; } = exercise;
}

public partial class ExercisePickerViewModel : BaseViewModel
{
    private const int SearchDebounceMilliseconds = 250;

    private readonly IExerciseDao _exerciseDao;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ExercisePickerViewModel> _logger;
    private readonly HashSet<int> _excludedExerciseIds = [];
    private CancellationTokenSource? _searchDebounce;
    private int _latestLoadRequest;
    private int _selectionSent;

    public ExercisePickerViewModel(
        IExerciseDao exerciseDao,
        INotificationService notificationService,
        ILogger<ExercisePickerViewModel> logger)
    {
        _exerciseDao = exerciseDao;
        _notificationService = notificationService;
        _logger = logger;
        Title = "Selecionar exercício";
    }

    public event EventHandler<ExercisePickerSelectionEventArgs>? ExerciseSelected;

    public event EventHandler? ManageExercisesRequested;

    public event EventHandler? CloseRequested;

    public ObservableCollection<ExercisePickerItemViewModel> Exercises { get; } = [];

    [ObservableProperty]
    private string searchText = string.Empty;

    partial void OnSearchTextChanged(string value)
    {
        ScheduleLoad();
    }

    public async Task InitializeAsync(IReadOnlyCollection<int> excludedIds)
    {
        ArgumentNullException.ThrowIfNull(excludedIds);

        CancelPendingDebounce();
        Volatile.Write(ref _selectionSent, 0);
        _excludedExerciseIds.Clear();

        foreach (var exerciseId in excludedIds)
        {
            if (exerciseId > 0)
            {
                _excludedExerciseIds.Add(exerciseId);
            }
        }

        SearchText = string.Empty;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        CancelPendingDebounce();
        var request = Interlocked.Increment(ref _latestLoadRequest);
        IsBusy = true;
        StatusMessage = null;

        try
        {
            var exercises = await _exerciseDao.SearchAsync(
                SearchText,
                muscleGroup: null,
                includeInactive: false);

            if (request != Volatile.Read(ref _latestLoadRequest))
            {
                return;
            }

            Exercises.Clear();
            foreach (var exercise in exercises)
            {
                if (!_excludedExerciseIds.Contains(exercise.Id))
                {
                    Exercises.Add(new ExercisePickerItemViewModel(
                        exercise,
                        SelectExercise));
                }
            }
        }
        catch (Exception exception)
        {
            if (request != Volatile.Read(ref _latestLoadRequest))
            {
                return;
            }

            _logger.LogError(exception, "Falha ao listar exercícios no seletor.");
            StatusMessage = "Não foi possível carregar os exercícios. Tente novamente.";
            await TryNotifyAsync(StatusMessage);
        }
        finally
        {
            if (request == Volatile.Read(ref _latestLoadRequest))
            {
                IsBusy = false;
            }
        }
    }

    [RelayCommand]
    private void ManageExercises()
    {
        if (!IsBusy)
        {
            ManageExercisesRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    private void Close()
    {
        CancelPendingDebounce();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SelectExercise(Exercise exercise)
    {
        if (IsBusy ||
            _excludedExerciseIds.Contains(exercise.Id) ||
            Interlocked.Exchange(ref _selectionSent, 1) == 1)
        {
            return;
        }

        CancelPendingDebounce();
        ExerciseSelected?.Invoke(
            this,
            new ExercisePickerSelectionEventArgs(exercise));
    }

    private void ScheduleLoad()
    {
        Interlocked.Increment(ref _latestLoadRequest);
        CancelPendingDebounce();
        var cancellation = new CancellationTokenSource();
        _searchDebounce = cancellation;
        _ = DebounceAndLoadAsync(cancellation);
    }

    private async Task DebounceAndLoadAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(SearchDebounceMilliseconds, cancellation.Token);
            await LoadAsync();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao atualizar a busca do seletor de exercícios.");
            StatusMessage = "Não foi possível atualizar a busca.";
        }
        finally
        {
            Interlocked.CompareExchange(ref _searchDebounce, null, cancellation);
            cancellation.Dispose();
        }
    }

    private void CancelPendingDebounce()
    {
        var cancellation = Interlocked.Exchange(ref _searchDebounce, null);
        cancellation?.Cancel();
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
