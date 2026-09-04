using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using GymTrack.Data.Dao;
using GymTrack.Models;
using GymTrack.Services;
using Microsoft.Extensions.Logging;

namespace GymTrack.ViewModels;

public sealed class WorkoutEditorRequestedEventArgs(int? workoutId) : EventArgs
{
    public int? WorkoutId { get; } = workoutId;
}

public sealed class WorkoutStartRequestedEventArgs(int workoutId) : EventArgs
{
    public int WorkoutId { get; } = workoutId;
}

public partial class WorkoutsViewModel : BaseViewModel
{
    private readonly IWorkoutDao _workoutDao;
    private readonly IDialogService _dialogService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<WorkoutsViewModel> _logger;
    private int _latestLoadRequest;
    private int _isShowingActionMenu;

    public WorkoutsViewModel(
        IWorkoutDao workoutDao,
        IDialogService dialogService,
        INotificationService notificationService,
        ILogger<WorkoutsViewModel> logger)
    {
        _workoutDao = workoutDao;
        _dialogService = dialogService;
        _notificationService = notificationService;
        _logger = logger;
        Title = "Treinos";
    }

    public event EventHandler<WorkoutEditorRequestedEventArgs>? EditorRequested;

    public event EventHandler<WorkoutStartRequestedEventArgs>? StartRequested;

    public event EventHandler? ManageExercisesRequested;

    public ObservableCollection<WorkoutListItemViewModel> Workouts { get; } = [];

    [RelayCommand]
    private async Task LoadAsync()
    {
        var request = Interlocked.Increment(ref _latestLoadRequest);
        IsBusy = true;
        StatusMessage = null;

        try
        {
            var summaries = await _workoutDao.GetSummariesAsync();
            if (request != Volatile.Read(ref _latestLoadRequest))
            {
                return;
            }

            Workouts.Clear();
            foreach (var summary in summaries)
            {
                Workouts.Add(new WorkoutListItemViewModel(
                    summary,
                    EditWorkout,
                    StartWorkoutAsync,
                    ShowActionsAsync));
            }
        }
        catch (Exception exception)
        {
            if (request != Volatile.Read(ref _latestLoadRequest))
            {
                return;
            }

            _logger.LogError(exception, "Falha ao listar treinos.");
            StatusMessage = "Não foi possível carregar os treinos. Tente novamente.";
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
    private void AddWorkout()
    {
        if (!IsBusy)
        {
            EditorRequested?.Invoke(this, new WorkoutEditorRequestedEventArgs(null));
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

    private void EditWorkout(WorkoutSummary workout)
    {
        if (!IsBusy)
        {
            EditorRequested?.Invoke(this, new WorkoutEditorRequestedEventArgs(workout.Id));
        }
    }

    private async Task ShowActionsAsync(WorkoutSummary workout)
    {
        if (IsBusy || Interlocked.Exchange(ref _isShowingActionMenu, 1) == 1)
        {
            return;
        }

        string? selectedAction = null;
        IsBusy = true;

        try
        {
            selectedAction = await _dialogService.ChooseActionAsync(
                workout.Name,
                "Cancelar",
                "Editar",
                "Iniciar",
                "Excluir");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao abrir ações do treino {WorkoutId}.", workout.Id);
            StatusMessage = "Não foi possível abrir as ações do treino.";
            await TryNotifyAsync(StatusMessage);
        }
        finally
        {
            IsBusy = false;
            Volatile.Write(ref _isShowingActionMenu, 0);
        }

        if (selectedAction == "Editar")
        {
            EditWorkout(workout);
        }
        else if (selectedAction == "Iniciar")
        {
            await StartWorkoutAsync(workout);
        }
        else if (selectedAction == "Excluir")
        {
            await DeleteWorkoutAsync(workout);
        }
    }

    private async Task StartWorkoutAsync(WorkoutSummary workout)
    {
        if (workout.ExerciseCount == 0)
        {
            await _dialogService.AlertAsync(
                "Treino incompleto",
                "Adicione ao menos um exercício para iniciar o treino.",
                "Entendi");
            return;
        }

        StartRequested?.Invoke(this, new WorkoutStartRequestedEventArgs(workout.Id));
    }

    private async Task DeleteWorkoutAsync(WorkoutSummary workout)
    {
        IsBusy = true;
        var deleted = false;

        try
        {
            if (await _workoutDao.HasSessionsAsync(workout.Id))
            {
                await _dialogService.AlertAsync(
                    "Treino preservado",
                    $"{workout.Name} possui sessões no histórico e não pode ser excluído.",
                    "Entendi");
                return;
            }

            var confirmed = await _dialogService.ConfirmAsync(
                "Excluir treino?",
                $"Excluir {workout.Name} permanentemente? Esta ação não pode ser desfeita.",
                "Excluir",
                "Cancelar");
            if (!confirmed)
            {
                return;
            }

            if (await _workoutDao.DeleteAsync(workout.Id) != 1)
            {
                throw new InvalidOperationException("O treino não foi excluído.");
            }

            deleted = true;
            await TryNotifyAsync("Treino excluído.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao excluir treino {WorkoutId}.", workout.Id);
            StatusMessage = "Não foi possível excluir o treino. Tente novamente.";
            await TryNotifyAsync(StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }

        if (deleted)
        {
            await LoadAsync();
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
