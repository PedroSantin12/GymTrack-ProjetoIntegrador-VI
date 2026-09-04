using GymTrack.Data.Dao;
using GymTrack.Models;

namespace GymTrack.Tests;

public sealed class WorkoutDaoTests
{
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
