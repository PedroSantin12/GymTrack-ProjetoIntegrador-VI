using GymTrack.Data.Dao;
using GymTrack.Models;

namespace GymTrack.Tests;

public sealed class SessionDaoTests
{
    [Fact]
    public async Task SessionAndSets_CanBeInsertedUpdatedAndRead()
    {
        await using var context = await TestDatabase.CreateAsync();
        IExerciseDao exerciseDao = new ExerciseDao(context.Database);
        IWorkoutDao workoutDao = new WorkoutDao(context.Database);
        ISessionDao sessionDao = new SessionDao(context.Database);
        var exercise = new Exercise { Name = "Rosca direta", MuscleGroup = "Braços" };
        await exerciseDao.InsertAsync(exercise);
        var workout = new Workout { Name = "Braços" };
        await workoutDao.SaveAsync(workout, []);

        var session = new WorkoutSession { WorkoutId = workout.Id };
        await sessionDao.InsertAsync(session);
        var setRecord = new SetRecord
        {
            SessionId = session.Id,
            ExerciseId = exercise.Id,
            SetNumber = 1,
            Reps = 10,
            LoadKg = 20
        };
        await sessionDao.SaveSetAsync(setRecord);

        var repeatedConfirmation = new SetRecord
        {
            SessionId = session.Id,
            ExerciseId = exercise.Id,
            SetNumber = 1,
            Reps = 12,
            LoadKg = 20
        };
        await sessionDao.SaveSetAsync(repeatedConfirmation);
        session.FinishedAt = DateTime.UtcNow;
        await sessionDao.UpdateAsync(session);

        var storedSession = await sessionDao.GetByIdAsync(session.Id);
        var sets = await sessionDao.GetSetsAsync(session.Id);

        Assert.NotNull(storedSession?.FinishedAt);
        Assert.Single(sets);
        Assert.Equal(setRecord.Id, repeatedConfirmation.Id);
        Assert.Equal(12, sets[0].Reps);
        Assert.Equal(20, sets[0].LoadKg);
    }

    [Fact]
    public async Task ForeignKeys_RejectOrphanSetRecord()
    {
        await using var context = await TestDatabase.CreateAsync();
        ISessionDao sessionDao = new SessionDao(context.Database);

        await Assert.ThrowsAsync<SQLite.SQLiteException>(() => sessionDao.SaveSetAsync(
            new SetRecord
            {
                SessionId = 123_456,
                ExerciseId = 654_321,
                SetNumber = 1,
                Reps = 10,
                LoadKg = 20
            }));
    }
}
