using GymTrack.Data.Dao;
using GymTrack.Models;
using GymTrack.Services;
using GymTrack.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace GymTrack.Tests;

public sealed class WorkoutEditViewModelTests
{
    [Fact]
    public async Task InitializeAsync_LoadsCompositionInOrderIncludingArchivedExercise()
    {
        var workoutDao = new FakeWorkoutDao
        {
            StoredWorkout = new Workout
            {
                Id = 7,
                Name = "Superior",
                Description = "Treino A"
            }
        };
        workoutDao.StoredComposition.AddRange(
        [
            new WorkoutExercise
            {
                Id = 11,
                WorkoutId = 7,
                ExerciseId = 2,
                OrderIndex = 0,
                PlannedSets = 4,
                PlannedReps = 8,
                PlannedLoad = 32.5
            },
            new WorkoutExercise
            {
                Id = 12,
                WorkoutId = 7,
                ExerciseId = 1,
                OrderIndex = 1,
                PlannedSets = 3,
                PlannedReps = 12
            }
        ]);
        var exerciseDao = new FakeExerciseDao();
        exerciseDao.Stored.AddRange(
        [
            new Exercise { Id = 1, Name = "Supino", MuscleGroup = "Peito" },
            new Exercise
            {
                Id = 2,
                Name = "Remada antiga",
                MuscleGroup = "Costas",
                IsActive = false
            }
        ]);
        var viewModel = CreateViewModel(workoutDao, exerciseDao);

        await viewModel.InitializeAsync(7);

        Assert.Equal("Editar treino", viewModel.Title);
        Assert.Equal("Superior", viewModel.Name);
        Assert.Equal("Treino A", viewModel.Description);
        Assert.True(viewModel.HasExercises);
        Assert.Equal([2, 1], viewModel.Exercises.Select(item => item.ExerciseId));
        Assert.Equal("Arquivado", viewModel.Exercises[0].StateText);
        Assert.Equal([1, 2], viewModel.Exercises.Select(item => item.Position));
        Assert.False(viewModel.Exercises[0].CanMoveUp);
        Assert.True(viewModel.Exercises[0].CanMoveDown);
        Assert.True(viewModel.Exercises[1].CanMoveUp);
        Assert.False(viewModel.Exercises[1].CanMoveDown);
        Assert.True(viewModel.Exercises[0].TryBuildModel(0, out var plan, out _));
        Assert.Equal(4, plan.PlannedSets);
        Assert.Equal(8, plan.PlannedReps);
        Assert.Equal(32.5, plan.PlannedLoad);
        Assert.True(exerciseDao.LastGetAllIncludedInactive);
    }

    [Fact]
    public void AddExercise_PreventsDuplicateAndInactiveExercise()
    {
        var viewModel = CreateViewModel(
            new FakeWorkoutDao(),
            new FakeExerciseDao());
        var active = new Exercise
        {
            Id = 1,
            Name = "Supino",
            MuscleGroup = "Peito"
        };
        var inactive = new Exercise
        {
            Id = 2,
            Name = "Remada antiga",
            MuscleGroup = "Costas",
            IsActive = false
        };

        Assert.True(viewModel.AddExercise(active));
        Assert.False(viewModel.AddExercise(active));
        Assert.Contains("já foi adicionado", viewModel.ValidationMessage);
        Assert.False(viewModel.AddExercise(inactive));
        Assert.Contains("Somente exercícios ativos", viewModel.ValidationMessage);
        Assert.Equal([1], viewModel.SelectedExerciseIds);
    }

    [Fact]
    public void CompositionCommands_MoveAndRemoveExercises()
    {
        var viewModel = CreateViewModel(
            new FakeWorkoutDao(),
            new FakeExerciseDao());
        viewModel.AddExercise(CreateExercise(1, "Supino"));
        viewModel.AddExercise(CreateExercise(2, "Remada"));
        viewModel.AddExercise(CreateExercise(3, "Agachamento"));
        var remada = viewModel.Exercises[1];
        var agachamento = viewModel.Exercises[2];

        remada.MoveUpCommand.Execute(null);
        agachamento.RemoveCommand.Execute(null);

        Assert.Equal([2, 1], viewModel.Exercises.Select(item => item.ExerciseId));
        Assert.Equal([1, 2], viewModel.Exercises.Select(item => item.Position));
        Assert.False(viewModel.Exercises[0].CanMoveUp);
        Assert.True(viewModel.Exercises[0].CanMoveDown);
        Assert.Equal([2, 1], viewModel.SelectedExerciseIds);
    }

    [Fact]
    public async Task SaveAsync_EmptyDraft_PersistsTrimmedDataAndCloses()
    {
        var workoutDao = new FakeWorkoutDao();
        var notifications = new FakeNotificationService();
        var viewModel = CreateViewModel(
            workoutDao,
            new FakeExerciseDao(),
            notifications);
        var closeRequests = 0;
        viewModel.CloseRequested += (_, _) => closeRequests++;
        viewModel.Name = "  Treino futuro  ";
        viewModel.Description = "   ";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(1, workoutDao.SaveCalls);
        Assert.NotNull(workoutDao.LastSavedWorkout);
        Assert.Equal("Treino futuro", workoutDao.LastSavedWorkout.Name);
        Assert.Null(workoutDao.LastSavedWorkout.Description);
        Assert.Empty(workoutDao.LastSavedComposition);
        Assert.Equal(1, closeRequests);
        Assert.Contains("Treino salvo.", notifications.Messages);
    }

    [Fact]
    public async Task SaveAsync_ComposedWorkout_PersistsCurrentOrderAndPlans()
    {
        var workoutDao = new FakeWorkoutDao();
        var viewModel = CreateViewModel(
            workoutDao,
            new FakeExerciseDao());
        viewModel.Name = "Treino completo";
        viewModel.AddExercise(CreateExercise(1, "Supino"));
        viewModel.AddExercise(CreateExercise(2, "Remada"));
        var supino = viewModel.Exercises[0];
        var remada = viewModel.Exercises[1];
        supino.PlannedSets = 5;
        supino.PlannedReps = "8";
        supino.PlannedLoad = "40.5";
        remada.PlannedSets = 4;
        remada.PlannedReps = "12";
        remada.PlannedLoad = string.Empty;
        remada.MoveUpCommand.Execute(null);

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(1, workoutDao.SaveCalls);
        Assert.Equal([2, 1], workoutDao.LastSavedComposition.Select(item => item.ExerciseId));
        Assert.Equal([0, 1], workoutDao.LastSavedComposition.Select(item => item.OrderIndex));
        Assert.Equal([4, 5], workoutDao.LastSavedComposition.Select(item => item.PlannedSets));
        Assert.Equal([12, 8], workoutDao.LastSavedComposition.Select(item => item.PlannedReps));
        Assert.Null(workoutDao.LastSavedComposition[0].PlannedLoad);
        Assert.Equal(40.5, workoutDao.LastSavedComposition[1].PlannedLoad);
    }

    [Theory]
    [InlineData("A", 3, "10", "", "nome")]
    [InlineData("Treino", 0, "10", "", "séries")]
    [InlineData("Treino", 21, "10", "", "séries")]
    [InlineData("Treino", 3, "0", "", "repetições")]
    [InlineData("Treino", 3, "101", "", "repetições")]
    [InlineData("Treino", 3, "10", "-1", "carga")]
    public async Task SaveAsync_InvalidInput_DoesNotCallDao(
        string name,
        double plannedSets,
        string plannedReps,
        string plannedLoad,
        string expectedMessage)
    {
        var workoutDao = new FakeWorkoutDao();
        var viewModel = CreateViewModel(
            workoutDao,
            new FakeExerciseDao());
        viewModel.Name = name;
        viewModel.AddExercise(CreateExercise(1, "Supino"));
        var item = Assert.Single(viewModel.Exercises);
        item.PlannedSets = plannedSets;
        item.PlannedReps = plannedReps;
        item.PlannedLoad = plannedLoad;

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(0, workoutDao.SaveCalls);
        Assert.Contains(expectedMessage, viewModel.ValidationMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveAsync_ExistingWorkout_KeepsItsIdentity()
    {
        var workoutDao = new FakeWorkoutDao
        {
            StoredWorkout = new Workout { Id = 42, Name = "Treino antigo" }
        };
        var exerciseDao = new FakeExerciseDao();
        var viewModel = CreateViewModel(workoutDao, exerciseDao);
        await viewModel.InitializeAsync(42);
        viewModel.Name = "Treino atualizado";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.NotNull(workoutDao.LastSavedWorkout);
        Assert.Equal(42, workoutDao.LastSavedWorkout.Id);
        Assert.Equal("Treino atualizado", workoutDao.LastSavedWorkout.Name);
    }

    [Fact]
    public async Task InitializeAsync_MissingWorkout_DisablesSave()
    {
        var workoutDao = new FakeWorkoutDao();
        var viewModel = CreateViewModel(
            workoutDao,
            new FakeExerciseDao());

        await viewModel.InitializeAsync(999);
        viewModel.Name = "Não deve ser criado";
        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.False(viewModel.CanSave);
        Assert.Contains("localizar", viewModel.ValidationMessage);
        Assert.Equal(0, workoutDao.SaveCalls);
    }

    private static WorkoutEditViewModel CreateViewModel(
        FakeWorkoutDao workoutDao,
        FakeExerciseDao exerciseDao,
        FakeNotificationService? notifications = null)
    {
        return new WorkoutEditViewModel(
            workoutDao,
            exerciseDao,
            notifications ?? new FakeNotificationService(),
            NullLogger<WorkoutEditViewModel>.Instance);
    }

    private static Exercise CreateExercise(int id, string name)
    {
        return new Exercise
        {
            Id = id,
            Name = name,
            MuscleGroup = "Grupo"
        };
    }

    private sealed class FakeWorkoutDao : IWorkoutDao
    {
        private int _nextId = 100;

        public Workout? StoredWorkout { get; set; }

        public List<WorkoutExercise> StoredComposition { get; } = [];

        public int SaveCalls { get; private set; }

        public Workout? LastSavedWorkout { get; private set; }

        public IReadOnlyList<WorkoutExercise> LastSavedComposition { get; private set; } = [];

        public Task<IReadOnlyList<Workout>> GetAllAsync()
        {
            IReadOnlyList<Workout> result = StoredWorkout is null
                ? []
                : [Clone(StoredWorkout)];
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<WorkoutSummary>> GetSummariesAsync()
        {
            return Task.FromResult<IReadOnlyList<WorkoutSummary>>([]);
        }

        public Task<Workout?> GetByIdAsync(int id)
        {
            return Task.FromResult(
                StoredWorkout?.Id == id ? Clone(StoredWorkout) : null);
        }

        public Task<IReadOnlyList<WorkoutExercise>> GetExercisesAsync(int workoutId)
        {
            return Task.FromResult<IReadOnlyList<WorkoutExercise>>(
                StoredComposition
                    .Where(item => item.WorkoutId == workoutId)
                    .Select(Clone)
                    .ToList());
        }

        public Task<int> SaveAsync(
            Workout workout,
            IReadOnlyList<WorkoutExercise> exercises)
        {
            SaveCalls++;
            if (workout.Id == 0)
            {
                workout.Id = _nextId++;
            }

            LastSavedWorkout = Clone(workout);
            LastSavedComposition = exercises.Select(Clone).ToList();
            StoredWorkout = Clone(workout);
            StoredComposition.Clear();
            StoredComposition.AddRange(exercises.Select(item =>
            {
                var clone = Clone(item);
                clone.WorkoutId = workout.Id;
                return clone;
            }));
            return Task.FromResult(workout.Id);
        }

        public Task<bool> HasSessionsAsync(int id)
        {
            return Task.FromResult(false);
        }

        public Task<int> DeleteAsync(int id)
        {
            if (StoredWorkout?.Id != id)
            {
                return Task.FromResult(0);
            }

            StoredWorkout = null;
            StoredComposition.Clear();
            return Task.FromResult(1);
        }

        private static Workout Clone(Workout workout)
        {
            return new Workout
            {
                Id = workout.Id,
                Name = workout.Name,
                Description = workout.Description,
                CreatedAt = workout.CreatedAt,
                UpdatedAt = workout.UpdatedAt
            };
        }

        private static WorkoutExercise Clone(WorkoutExercise item)
        {
            return new WorkoutExercise
            {
                Id = item.Id,
                WorkoutId = item.WorkoutId,
                ExerciseId = item.ExerciseId,
                OrderIndex = item.OrderIndex,
                PlannedSets = item.PlannedSets,
                PlannedReps = item.PlannedReps,
                PlannedLoad = item.PlannedLoad
            };
        }
    }

    private sealed class FakeExerciseDao : IExerciseDao
    {
        public List<Exercise> Stored { get; } = [];

        public bool LastGetAllIncludedInactive { get; private set; }

        public Task<IReadOnlyList<Exercise>> GetAllAsync(bool includeInactive = false)
        {
            LastGetAllIncludedInactive = includeInactive;
            return Task.FromResult<IReadOnlyList<Exercise>>(
                Stored.Where(item => includeInactive || item.IsActive).ToList());
        }

        public Task<Exercise?> GetByIdAsync(int id)
        {
            return Task.FromResult(Stored.SingleOrDefault(item => item.Id == id));
        }

        public Task<IReadOnlyList<Exercise>> SearchAsync(
            string? searchText,
            string? muscleGroup = null,
            bool includeInactive = false)
        {
            return Task.FromResult<IReadOnlyList<Exercise>>([]);
        }

        public Task<bool> ExistsAsync(
            string name,
            string muscleGroup,
            int excludedId = 0)
        {
            return Task.FromResult(false);
        }

        public Task<int> InsertAsync(Exercise exercise)
        {
            Stored.Add(exercise);
            return Task.FromResult(1);
        }

        public Task<int> UpdateAsync(Exercise exercise)
        {
            return Task.FromResult(1);
        }

        public Task<int> ArchiveAsync(int id)
        {
            return Task.FromResult(1);
        }

        public Task<int> DeleteAsync(int id)
        {
            return Task.FromResult(1);
        }

        public Task<bool> HasHistoryAsync(int id)
        {
            return Task.FromResult(false);
        }

        public Task<bool> HasReferencesAsync(int id)
        {
            return Task.FromResult(false);
        }
    }

    private sealed class FakeNotificationService : INotificationService
    {
        public List<string> Messages { get; } = [];

        public Task ShowAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
