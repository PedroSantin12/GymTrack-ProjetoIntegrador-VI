using GymTrack.Data.Dao;
using GymTrack.Models;

namespace GymTrack.Tests;

public sealed class WorkoutDaoTests
{
    [Fact]
    public async Task SaveAsync_AllowsEmptyDraftWorkout()
    {
        await using var context = await TestDatabase.CreateAsync();
        IWorkoutDao workoutDao = new WorkoutDao(context.Database);
        var workout = new Workout
        {
            Name = "Rascunho",
            Description = "Será montado depois"
        };

        var id = await workoutDao.SaveAsync(workout, []);

        Assert.True(id > 0);
        var inserted = await workoutDao.GetByIdAsync(id);
        Assert.NotNull(inserted);
        Assert.Equal("Rascunho", inserted.Name);
        Assert.Empty(await workoutDao.GetExercisesAsync(id));
    }

    [Fact]
    public async Task SaveAsync_PersistsWorkoutAndOrderedComposition()
    {
        await using var context = await TestDatabase.CreateAsync();
        IExerciseDao exerciseDao = new ExerciseDao(context.Database);
        IWorkoutDao workoutDao = new WorkoutDao(context.Database);
        var first = new Exercise { Name = "Supino", MuscleGroup = "Peito" };
        var second = new Exercise { Name = "Remada", MuscleGroup = "Costas" };
        await exerciseDao.InsertAsync(first);
        await exerciseDao.InsertAsync(second);

        var workout = new Workout { Name = "Superior", Description = "Treino A" };
        var id = await workoutDao.SaveAsync(
            workout,
            [
                new WorkoutExercise
                {
                    ExerciseId = second.Id,
                    PlannedSets = 3,
                    PlannedReps = 12
                },
                new WorkoutExercise
                {
                    ExerciseId = first.Id,
                    PlannedSets = 4,
                    PlannedReps = 8,
                    PlannedLoad = 40
                }
            ]);

        var inserted = await workoutDao.GetByIdAsync(id);
        var composition = await workoutDao.GetExercisesAsync(id);

        Assert.NotNull(inserted);
        Assert.Equal("Treino A", inserted.Description);
        Assert.NotEqual(default, inserted.CreatedAt);
        Assert.Equal([second.Id, first.Id], composition.Select(item => item.ExerciseId));
        Assert.Equal([0, 1], composition.Select(item => item.OrderIndex));
        Assert.Equal(40, composition[1].PlannedLoad);
    }

    [Fact]
    public async Task SaveAsync_WhenCompositionFails_RollsBackPreviousData()
    {
        await using var context = await TestDatabase.CreateAsync();
        IExerciseDao exerciseDao = new ExerciseDao(context.Database);
        IWorkoutDao workoutDao = new WorkoutDao(context.Database);
        var exercise = new Exercise { Name = "Leg press", MuscleGroup = "Pernas" };
        await exerciseDao.InsertAsync(exercise);

        var workout = new Workout { Name = "Pernas" };
        await workoutDao.SaveAsync(
            workout,
            [
                new WorkoutExercise
                {
                    ExerciseId = exercise.Id,
                    PlannedSets = 3,
                    PlannedReps = 10
                }
            ]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => workoutDao.SaveAsync(
            workout,
            [
                new WorkoutExercise
                {
                    ExerciseId = exercise.Id,
                    PlannedSets = 5,
                    PlannedReps = 5
                },
                new WorkoutExercise
                {
                    ExerciseId = 999_999,
                    PlannedSets = 3,
                    PlannedReps = 10
                }
            ]));

        var composition = await workoutDao.GetExercisesAsync(workout.Id);
        Assert.Single(composition);
        Assert.Equal(3, composition[0].PlannedSets);
    }

    [Fact]
    public async Task SaveAsync_UpdatingWorkout_ReplacesCompositionAndPreservesOrder()
    {
        await using var context = await TestDatabase.CreateAsync();
        IExerciseDao exerciseDao = new ExerciseDao(context.Database);
        IWorkoutDao workoutDao = new WorkoutDao(context.Database);
        var first = new Exercise { Name = "Agachamento", MuscleGroup = "Pernas" };
        var second = new Exercise { Name = "Cadeira extensora", MuscleGroup = "Pernas" };
        await exerciseDao.InsertAsync(first);
        await exerciseDao.InsertAsync(second);
        var workout = new Workout { Name = "Treino inicial" };
        await workoutDao.SaveAsync(
            workout,
            [
                new WorkoutExercise
                {
                    ExerciseId = first.Id,
                    PlannedSets = 3,
                    PlannedReps = 10
                }
            ]);
        var originalCreatedAt = (await workoutDao.GetByIdAsync(workout.Id))!.CreatedAt;

        workout.Name = "Pernas completo";
        workout.Description = "Ênfase em quadríceps";
        await workoutDao.SaveAsync(
            workout,
            [
                new WorkoutExercise
                {
                    ExerciseId = second.Id,
                    PlannedSets = 4,
                    PlannedReps = 12,
                    PlannedLoad = 25.5
                },
                new WorkoutExercise
                {
                    ExerciseId = first.Id,
                    PlannedSets = 5,
                    PlannedReps = 8,
                    PlannedLoad = 60
                }
            ]);

        var updated = await workoutDao.GetByIdAsync(workout.Id);
        var composition = await workoutDao.GetExercisesAsync(workout.Id);
        Assert.NotNull(updated);
        Assert.Equal("Pernas completo", updated.Name);
        Assert.Equal("Ênfase em quadríceps", updated.Description);
        Assert.Equal(originalCreatedAt, updated.CreatedAt);
        Assert.Equal([second.Id, first.Id], composition.Select(item => item.ExerciseId));
        Assert.Equal([0, 1], composition.Select(item => item.OrderIndex));
        Assert.Equal([4, 5], composition.Select(item => item.PlannedSets));
        Assert.Equal([12, 8], composition.Select(item => item.PlannedReps));
        Assert.Equal(25.5, composition[0].PlannedLoad);
        Assert.Equal(60, composition[1].PlannedLoad);
    }

    [Fact]
    public async Task SaveAsync_DuplicateExercise_RollsBackNewWorkout()
    {
        await using var context = await TestDatabase.CreateAsync();
        IExerciseDao exerciseDao = new ExerciseDao(context.Database);
        IWorkoutDao workoutDao = new WorkoutDao(context.Database);
        var exercise = new Exercise { Name = "Puxada", MuscleGroup = "Costas" };
        await exerciseDao.InsertAsync(exercise);
        var workout = new Workout { Name = "Duplicado" };

        await Assert.ThrowsAsync<InvalidOperationException>(() => workoutDao.SaveAsync(
            workout,
            [
                new WorkoutExercise
                {
                    ExerciseId = exercise.Id,
                    PlannedSets = 3,
                    PlannedReps = 10
                },
                new WorkoutExercise
                {
                    ExerciseId = exercise.Id,
                    PlannedSets = 4,
                    PlannedReps = 8
                }
            ]));

        Assert.Equal(0, workout.Id);
        Assert.Empty(await workoutDao.GetAllAsync());
    }

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(21, 10, 0)]
    [InlineData(3, 0, 0)]
    [InlineData(3, 101, 0)]
    [InlineData(3, 10, -0.5)]
    public async Task SaveAsync_InvalidPlan_RollsBackNewWorkout(
        int plannedSets,
        int plannedReps,
        double plannedLoad)
    {
        await using var context = await TestDatabase.CreateAsync();
        IExerciseDao exerciseDao = new ExerciseDao(context.Database);
        IWorkoutDao workoutDao = new WorkoutDao(context.Database);
        var exercise = new Exercise { Name = "Rosca direta", MuscleGroup = "Braços" };
        await exerciseDao.InsertAsync(exercise);
        var workout = new Workout { Name = "Plano inválido" };

        await Assert.ThrowsAnyAsync<ArgumentOutOfRangeException>(() => workoutDao.SaveAsync(
            workout,
            [
                new WorkoutExercise
                {
                    ExerciseId = exercise.Id,
                    PlannedSets = plannedSets,
                    PlannedReps = plannedReps,
                    PlannedLoad = plannedLoad
                }
            ]));

        Assert.Equal(0, workout.Id);
        Assert.Empty(await workoutDao.GetAllAsync());
    }

    [Fact]
    public async Task GetSummariesAsync_ReturnsExerciseCountAndLatestSessionStart()
    {
        await using var context = await TestDatabase.CreateAsync();
        IExerciseDao exerciseDao = new ExerciseDao(context.Database);
        IWorkoutDao workoutDao = new WorkoutDao(context.Database);
        ISessionDao sessionDao = new SessionDao(context.Database);
        var first = new Exercise { Name = "Supino", MuscleGroup = "Peito" };
        var second = new Exercise { Name = "Crucifixo", MuscleGroup = "Peito" };
        await exerciseDao.InsertAsync(first);
        await exerciseDao.InsertAsync(second);
        var workout = new Workout { Name = "Peito" };
        await workoutDao.SaveAsync(
            workout,
            [
                new WorkoutExercise
                {
                    ExerciseId = first.Id,
                    PlannedSets = 3,
                    PlannedReps = 10
                },
                new WorkoutExercise
                {
                    ExerciseId = second.Id,
                    PlannedSets = 3,
                    PlannedReps = 12
                }
            ]);
        var earlier = new DateTime(2026, 8, 20, 20, 0, 0, DateTimeKind.Utc);
        var latest = new DateTime(2026, 8, 28, 21, 30, 0, DateTimeKind.Utc);
        await sessionDao.InsertAsync(new WorkoutSession
        {
            WorkoutId = workout.Id,
            StartedAt = earlier.AddHours(-1),
            FinishedAt = earlier
        });
        await sessionDao.InsertAsync(new WorkoutSession
        {
            WorkoutId = workout.Id,
            StartedAt = latest.AddHours(-1),
            FinishedAt = latest
        });
        var latestSessionStart = latest.AddDays(1);
        await sessionDao.InsertAsync(new WorkoutSession
        {
            WorkoutId = workout.Id,
            StartedAt = latestSessionStart
        });

        var summary = Assert.Single(await workoutDao.GetSummariesAsync());

        Assert.Equal(workout.Id, summary.Id);
        Assert.Equal("Peito", summary.Name);
        Assert.Equal(2, summary.ExerciseCount);
        Assert.Equal(latestSessionStart, summary.LastSessionAt);
    }

    [Fact]
    public async Task DeleteAsync_RemovesDraftAndComposition()
    {
        await using var context = await TestDatabase.CreateAsync();
        IExerciseDao exerciseDao = new ExerciseDao(context.Database);
        IWorkoutDao workoutDao = new WorkoutDao(context.Database);
        var exercise = new Exercise { Name = "Elevação lateral", MuscleGroup = "Ombros" };
        await exerciseDao.InsertAsync(exercise);
        var workout = new Workout { Name = "Ombros" };
        await workoutDao.SaveAsync(
            workout,
            [
                new WorkoutExercise
                {
                    ExerciseId = exercise.Id,
                    PlannedSets = 4,
                    PlannedReps = 12
                }
            ]);

        var affectedRows = await workoutDao.DeleteAsync(workout.Id);

        Assert.Equal(1, affectedRows);
        Assert.Null(await workoutDao.GetByIdAsync(workout.Id));
        Assert.Empty(await workoutDao.GetExercisesAsync(workout.Id));
    }

    [Fact]
    public async Task DeleteAsync_PreservesWorkoutThatHasSession()
    {
        await using var context = await TestDatabase.CreateAsync();
        IWorkoutDao workoutDao = new WorkoutDao(context.Database);
        ISessionDao sessionDao = new SessionDao(context.Database);
        var workout = new Workout { Name = "Treino com histórico" };
        await workoutDao.SaveAsync(workout, []);
        await sessionDao.InsertAsync(new WorkoutSession { WorkoutId = workout.Id });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => workoutDao.DeleteAsync(workout.Id));

        Assert.NotNull(await workoutDao.GetByIdAsync(workout.Id));
    }
}
