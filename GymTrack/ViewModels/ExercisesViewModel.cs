using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GymTrack.Data.Dao;
using GymTrack.Models;
using GymTrack.Services;
using Microsoft.Extensions.Logging;

namespace GymTrack.ViewModels;

public sealed class ExerciseEditorRequestedEventArgs(int? exerciseId) : EventArgs
{
    public int? ExerciseId { get; } = exerciseId;
}

public partial class ExercisesViewModel : BaseViewModel
{
    private const int SearchDebounceMilliseconds = 250;

    private readonly IExerciseDao _exerciseDao;
    private readonly IDialogService _dialogService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ExercisesViewModel> _logger;
    private CancellationTokenSource? _searchDebounce;
    private int _latestLoadRequest;
    private int _isShowingActionMenu;

    public ExercisesViewModel(
        IExerciseDao exerciseDao,
        IDialogService dialogService,
        INotificationService notificationService,
        ILogger<ExercisesViewModel> logger)
    {
        _exerciseDao = exerciseDao;
        _dialogService = dialogService;
        _notificationService = notificationService;
        _logger = logger;
        Title = "Exercícios";
    }

    public event EventHandler<ExerciseEditorRequestedEventArgs>? EditorRequested;

    public ObservableCollection<ExerciseListItemViewModel> Exercises { get; } = [];

    public IReadOnlyList<string> MuscleGroups { get; } =
    [
        "Todos",
        "Peito",
        "Costas",
        "Pernas",
        "Ombros",
        "Braços",
        "Core"
    ];

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string selectedMuscleGroup = "Todos";

    [ObservableProperty]
    private bool showInactive;

    partial void OnSearchTextChanged(string value)
    {
        ScheduleLoad();
    }

    partial void OnSelectedMuscleGroupChanged(string value)
    {
        ScheduleLoad();
    }

    partial void OnShowInactiveChanged(bool value)
    {
        ScheduleLoad();
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
            var muscleGroup = SelectedMuscleGroup == "Todos"
                ? null
                : SelectedMuscleGroup;
            var exercises = await _exerciseDao.SearchAsync(
                SearchText,
                muscleGroup,
                ShowInactive);

            if (request != Volatile.Read(ref _latestLoadRequest))
            {
                return;
            }

            Exercises.Clear();
            foreach (var exercise in exercises)
            {
                Exercises.Add(new ExerciseListItemViewModel(
                    exercise,
                    EditExercise,
                    ShowActionsAsync));
            }
        }
        catch (Exception exception)
        {
            if (request != Volatile.Read(ref _latestLoadRequest))
            {
                return;
            }

            _logger.LogError(exception, "Falha ao listar exercícios.");
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
    private void AddExercise()
    {
        EditorRequested?.Invoke(this, new ExerciseEditorRequestedEventArgs(null));
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedMuscleGroup = "Todos";
        ShowInactive = false;
        ScheduleLoad();
    }

    private void EditExercise(Exercise exercise)
    {
        if (!IsBusy)
        {
            EditorRequested?.Invoke(this, new ExerciseEditorRequestedEventArgs(exercise.Id));
        }
    }

    private async Task ShowActionsAsync(Exercise exercise)
    {
        if (IsBusy || Interlocked.Exchange(ref _isShowingActionMenu, 1) == 1)
        {
            return;
        }

        string? selectedAction = null;
        IsBusy = true;

        try
        {
            var actions = exercise.IsActive
                ? new[] { "Editar", "Remover" }
                : new[] { "Editar" };
            selectedAction = await _dialogService.ChooseActionAsync(
                exercise.Name,
                "Cancelar",
                actions);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao abrir ações do exercício {ExerciseId}.", exercise.Id);
            StatusMessage = "Não foi possível abrir as ações do exercício.";
            await TryNotifyAsync(StatusMessage);
        }
        finally
        {
            IsBusy = false;
            Volatile.Write(ref _isShowingActionMenu, 0);
        }

        if (selectedAction == "Editar")
        {
            EditExercise(exercise);
        }
        else if (selectedAction == "Remover")
        {
            await RemoveExerciseAsync(exercise);
        }
    }

    private async Task RemoveExerciseAsync(Exercise exercise)
    {
        if (IsBusy || !exercise.IsActive)
        {
            return;
        }

        var changed = false;
        IsBusy = true;
        StatusMessage = null;

        try
        {
            var hasReferences = await _exerciseDao.HasReferencesAsync(exercise.Id);
            var confirmed = hasReferences
                ? await _dialogService.ConfirmAsync(
                    "Arquivar exercício?",
                    $"{exercise.Name} está vinculado a um treino ou histórico. Ele será arquivado para preservar os registros.",
                    "Arquivar",
                    "Cancelar")
                : await _dialogService.ConfirmAsync(
                    "Excluir exercício?",
                    $"Excluir {exercise.Name} permanentemente? Esta ação não pode ser desfeita.",
                    "Excluir",
                    "Cancelar");

            if (!confirmed)
            {
                return;
            }

            if (hasReferences)
            {
                var archived = await _exerciseDao.ArchiveAsync(exercise.Id);
                if (archived != 1)
                {
                    throw new InvalidOperationException("O exercício não foi arquivado.");
                }

                changed = true;
                await TryNotifyAsync("Exercício arquivado.");
            }
            else
            {
                var deleted = await _exerciseDao.DeleteAsync(exercise.Id);
                if (deleted != 1)
                {
                    throw new InvalidOperationException("O exercício não foi excluído.");
                }

                changed = true;
                await TryNotifyAsync("Exercício excluído.");
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao remover exercício {ExerciseId}.", exercise.Id);
            StatusMessage = "Não foi possível remover o exercício. Tente novamente.";
            await TryNotifyAsync(StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }

        if (changed)
        {
            await LoadAsync();
        }
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
            _logger.LogError(exception, "Falha ao atualizar a busca de exercícios.");
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
