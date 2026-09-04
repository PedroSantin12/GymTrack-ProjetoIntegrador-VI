using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GymTrack.Data.Dao;
using GymTrack.Models;
using GymTrack.Services;
using Microsoft.Extensions.Logging;

namespace GymTrack.ViewModels;

public partial class WorkoutEditViewModel : BaseViewModel
{
    private readonly IWorkoutDao _workoutDao;
    private readonly IExerciseDao _exerciseDao;
    private readonly INotificationService _notificationService;
    private readonly ILogger<WorkoutEditViewModel> _logger;
    private int _workoutId;

    public WorkoutEditViewModel(
        IWorkoutDao workoutDao,
        IExerciseDao exerciseDao,
        INotificationService notificationService,
        ILogger<WorkoutEditViewModel> logger)
    {
        _workoutDao = workoutDao;
        _exerciseDao = exerciseDao;
        _notificationService = notificationService;
        _logger = logger;
        Title = "Novo treino";
    }

    public event EventHandler? AddExerciseRequested;

    public event EventHandler? CloseRequested;

    public ObservableCollection<WorkoutExerciseItemViewModel> Exercises { get; } = [];

    public IReadOnlyCollection<int> SelectedExerciseIds =>
        Exercises.Select(item => item.ExerciseId).ToArray();

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationMessage))]
    private string? validationMessage;

    [ObservableProperty]
    private bool hasExercises;

    [ObservableProperty]
    private bool canSave = true;

    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

    public async Task InitializeAsync(int? workoutId)
    {
        _workoutId = 0;
        Title = "Novo treino";
        Name = string.Empty;
        Description = string.Empty;
        ValidationMessage = null;
        StatusMessage = null;
        Exercises.Clear();
        UpdateCompositionState();
        CanSave = workoutId is not > 0;

        if (workoutId is not > 0)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var workout = await _workoutDao.GetByIdAsync(workoutId.Value);
            if (workout is null)
            {
                ValidationMessage = "Não foi possível localizar este treino.";
                return;
            }

            var plans = await _workoutDao.GetExercisesAsync(workout.Id);
            var catalog = await _exerciseDao.GetAllAsync(includeInactive: true);
            var exercisesById = catalog.ToDictionary(exercise => exercise.Id);
            var loadedItems = new List<WorkoutExerciseItemViewModel>(plans.Count);

            foreach (var plan in plans.OrderBy(item => item.OrderIndex))
            {
                if (!exercisesById.TryGetValue(plan.ExerciseId, out var exercise))
                {
                    throw new InvalidOperationException(
                        $"O exercício {plan.ExerciseId} deste treino não foi encontrado.");
                }

                loadedItems.Add(CreateItem(exercise, plan));
            }

            _workoutId = workout.Id;
            Title = "Editar treino";
            Name = workout.Name;
            Description = workout.Description ?? string.Empty;

            foreach (var item in loadedItems)
            {
                Exercises.Add(item);
            }

            UpdateCompositionState();
            CanSave = true;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao carregar treino {WorkoutId}.", workoutId);
            ValidationMessage = "Não foi possível carregar o treino. Tente novamente.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public bool AddExercise(Exercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        if (!exercise.IsActive)
        {
            ValidationMessage = "Somente exercícios ativos podem ser adicionados.";
            return false;
        }

        if (Exercises.Any(item => item.ExerciseId == exercise.Id))
        {
            ValidationMessage = $"{exercise.Name} já foi adicionado a este treino.";
            return false;
        }

        Exercises.Add(CreateItem(exercise, plan: null));
        ValidationMessage = null;
        UpdateCompositionState();
        return true;
    }

    [RelayCommand]
    private void RequestAddExercise()
    {
        if (!IsBusy)
        {
            AddExerciseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy || !CanSave)
        {
            return;
        }

        var normalizedName = (Name ?? string.Empty).Trim();
        var normalizedDescription = (Description ?? string.Empty).Trim();

        ValidationMessage = ValidateWorkout(normalizedName, normalizedDescription);
        if (HasValidationMessage)
        {
            return;
        }

        var composition = new List<WorkoutExercise>(Exercises.Count);
        for (var index = 0; index < Exercises.Count; index++)
        {
            if (!Exercises[index].TryBuildModel(
                    index,
                    out var plan,
                    out var itemValidationMessage))
            {
                ValidationMessage = itemValidationMessage;
                return;
            }

            composition.Add(plan);
        }

        IsBusy = true;
        CanSave = false;
        StatusMessage = null;
        var saved = false;

        try
        {
            var workout = new Workout
            {
                Id = _workoutId,
                Name = normalizedName,
                Description = string.IsNullOrEmpty(normalizedDescription)
                    ? null
                    : normalizedDescription
            };

            var savedId = await _workoutDao.SaveAsync(workout, composition);
            if (savedId <= 0)
            {
                throw new InvalidOperationException("O treino não foi salvo.");
            }

            _workoutId = savedId;
            saved = true;
            await TryNotifyAsync("Treino salvo.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao salvar treino {WorkoutId}.", _workoutId);
            ValidationMessage = "Não foi possível salvar o treino. Tente novamente.";
            await TryNotifyAsync(ValidationMessage);
        }
        finally
        {
            IsBusy = false;
            CanSave = !saved;
        }

        if (saved)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    private void Close()
    {
        if (!IsBusy)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private WorkoutExerciseItemViewModel CreateItem(
        Exercise exercise,
        WorkoutExercise? plan)
    {
        return new WorkoutExerciseItemViewModel(
            exercise,
            plan,
            MoveUp,
            MoveDown,
            Remove);
    }

    private void MoveUp(WorkoutExerciseItemViewModel item)
    {
        var index = Exercises.IndexOf(item);
        if (index <= 0)
        {
            return;
        }

        Exercises.Move(index, index - 1);
        UpdateCompositionState();
    }

    private void MoveDown(WorkoutExerciseItemViewModel item)
    {
        var index = Exercises.IndexOf(item);
        if (index < 0 || index >= Exercises.Count - 1)
        {
            return;
        }

        Exercises.Move(index, index + 1);
        UpdateCompositionState();
    }

    private void Remove(WorkoutExerciseItemViewModel item)
    {
        if (Exercises.Remove(item))
        {
            ValidationMessage = null;
            UpdateCompositionState();
        }
    }

    private void UpdateCompositionState()
    {
        HasExercises = Exercises.Count > 0;

        for (var index = 0; index < Exercises.Count; index++)
        {
            var item = Exercises[index];
            item.Position = index + 1;
            item.CanMoveUp = index > 0;
            item.CanMoveDown = index < Exercises.Count - 1;
        }

        OnPropertyChanged(nameof(SelectedExerciseIds));
    }

    private static string? ValidateWorkout(
        string normalizedName,
        string normalizedDescription)
    {
        if (normalizedName.Length is < 2 or > 60)
        {
            return "Informe um nome entre 2 e 60 caracteres.";
        }

        if (normalizedDescription.Length > 500)
        {
            return "A descrição deve ter no máximo 500 caracteres.";
        }

        return null;
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
