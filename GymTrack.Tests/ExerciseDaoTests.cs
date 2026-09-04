using GymTrack.Data.Dao;
using GymTrack.Models;

namespace GymTrack.Tests;

public sealed class ExerciseDaoTests
{
    [Fact]
    public async Task CrudAndSearch_RoundTripExerciseData()
    {
        await using var context = await TestDatabase.CreateAsync();
        IExerciseDao dao = new ExerciseDao(context.Database);
        var exercise = new Exercise
        {
            Name = "Supino Reto",
            MuscleGroup = "Peito",
            Notes = "Barra livre",
            IsActive = true
        };

        Assert.Equal(1, await dao.InsertAsync(exercise));
        Assert.True(exercise.Id > 0);

        var inserted = await dao.GetByIdAsync(exercise.Id);
        Assert.NotNull(inserted);
        Assert.Equal("Barra livre", inserted.Notes);
        Assert.True(inserted.IsActive);
        Assert.True(await dao.ExistsAsync("supino reto", "peito"));

        exercise.Notes = "Pegada média";
        Assert.Equal(1, await dao.UpdateAsync(exercise));

        var search = await dao.SearchAsync("PINO", "Peito");
        Assert.Single(search);
        Assert.Equal("Pegada média", search[0].Notes);

        Assert.Equal(1, await dao.ArchiveAsync(exercise.Id));
        Assert.Empty(await dao.GetAllAsync());
        Assert.Single(await dao.GetAllAsync(includeInactive: true));

        Assert.Equal(1, await dao.DeleteAsync(exercise.Id));
        Assert.Null(await dao.GetByIdAsync(exercise.Id));
    }

    [Fact]
    public async Task DeleteAsync_WithWorkoutReference_IsRejectedAndArchivePreservesExercise()
    {
        await using var context = await TestDatabase.CreateAsync();
        IExerciseDao exerciseDao = new ExerciseDao(context.Database);
        IWorkoutDao workoutDao = new WorkoutDao(context.Database);
        var exercise = new Exercise { Name = "Agachamento", MuscleGroup = "Pernas" };
        await exerciseDao.InsertAsync(exercise);

        var workout = new Workout { Name = "Treino de pernas" };
        await workoutDao.SaveAsync(
            workout,
            [
                new WorkoutExercise
                {
                    ExerciseId = exercise.Id,
                    PlannedSets = 4,
                    PlannedReps = 8,
                    PlannedLoad = 60
                }
            ]);

        Assert.True(await exerciseDao.HasReferencesAsync(exercise.Id));
        await Assert.ThrowsAsync<SQLite.SQLiteException>(
            () => exerciseDao.DeleteAsync(exercise.Id));

        Assert.Equal(1, await exerciseDao.ArchiveAsync(exercise.Id));
        var archived = await exerciseDao.GetByIdAsync(exercise.Id);
        Assert.NotNull(archived);
        Assert.False(archived.IsActive);
    }
}
