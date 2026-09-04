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

    [Fact]
    public async Task ActiveSessionAndPreviousPerformance_CanBeLoaded()
    {
        await using var context = await TestDatabase.CreateAsync();
        IExerciseDao exerciseDao = new ExerciseDao(context.Database);
        IWorkoutDao workoutDao = new WorkoutDao(context.Database);
        ISessionDao sessionDao = new SessionDao(context.Database);
        var exercise = new Exercise { Name = "Remada", MuscleGroup = "Costas" };
        await exerciseDao.InsertAsync(exercise);
        var workout = new Workout { Name = "Superior" };
        await workoutDao.SaveAsync(workout, []);
        var previous = new WorkoutSession
        {
            WorkoutId = workout.Id,
            StartedAt = new DateTime(2026, 8, 20, 18, 0, 0, DateTimeKind.Utc),
            FinishedAt = new DateTime(2026, 8, 20, 19, 0, 0, DateTimeKind.Utc)
        };
        await sessionDao.InsertAsync(previous);
        await sessionDao.SaveSetAsync(new SetRecord
        {
            SessionId = previous.Id,
            ExerciseId = exercise.Id,
            SetNumber = 1,
            Reps = 12,
            LoadKg = 35
        });
        var active = new WorkoutSession
        {
            WorkoutId = workout.Id,
            StartedAt = new DateTime(2026, 9, 4, 18, 0, 0, DateTimeKind.Utc)
        };
        await sessionDao.InsertAsync(active);

        var loadedActive = await sessionDao.GetActiveAsync();
        var previousSets = await sessionDao.GetPreviousSetsAsync(
            workout.Id,
            exercise.Id,
            active.Id);

        Assert.Equal(active.Id, loadedActive?.Id);
        var previousSet = Assert.Single(previousSets);
        Assert.Equal(12, previousSet.Reps);
        Assert.Equal(35, previousSet.LoadKg);
    }

    [Fact]
    public async Task SaveSetAsync_RejectsInvalidNumbers()
    {
        await using var context = await TestDatabase.CreateAsync();
        ISessionDao sessionDao = new SessionDao(context.Database);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            sessionDao.SaveSetAsync(new SetRecord { SetNumber = 0, Reps = 10, LoadKg = 1 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            sessionDao.SaveSetAsync(new SetRecord { SetNumber = 1, Reps = 0, LoadKg = 1 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            sessionDao.SaveSetAsync(new SetRecord { SetNumber = 1, Reps = 10, LoadKg = double.NaN }));
    }

    [Fact]
    public async Task HistorySummaries_AreFilteredOrderedAndCalculateVolume()
    {
        await using var context = await TestDatabase.CreateAsync();
        IExerciseDao exerciseDao = new ExerciseDao(context.Database);
        IWorkoutDao workoutDao = new WorkoutDao(context.Database);
        ISessionDao sessionDao = new SessionDao(context.Database);
        var exercise = new Exercise { Name = "Supino", MuscleGroup = "Peito" };
        await exerciseDao.InsertAsync(exercise);
        var workout = new Workout { Name = "Empurrar" };
        await workoutDao.SaveAsync(workout,
        [
            new WorkoutExercise
            {
                ExerciseId = exercise.Id,
                OrderIndex = 0,
                PlannedSets = 3,
                PlannedReps = 10
            }
        ]);
        var older = new WorkoutSession
        {
            WorkoutId = workout.Id,
            StartedAt = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc),
            FinishedAt = new DateTime(2026, 8, 1, 10, 30, 0, DateTimeKind.Utc)
        };
        var newer = new WorkoutSession
        {
            WorkoutId = workout.Id,
            StartedAt = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc),
            FinishedAt = new DateTime(2026, 9, 1, 10, 45, 0, DateTimeKind.Utc)
        };
        await sessionDao.InsertAsync(older);
        await sessionDao.InsertAsync(newer);
        await sessionDao.SaveSetAsync(new SetRecord
        {
            SessionId = newer.Id,
            ExerciseId = exercise.Id,
            SetNumber = 1,
            Reps = 10,
            LoadKg = 50
        });

        var all = await sessionDao.GetSummariesAsync();
        var filtered = await sessionDao.GetSummariesAsync(
            workout.Id,
            new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc));
        var details = await sessionDao.GetSetDetailsAsync(newer.Id);

        Assert.Equal([newer.Id, older.Id], all.Select(item => item.Id));
        var summary = Assert.Single(filtered);
        Assert.Equal("Empurrar", summary.WorkoutName);
        Assert.Equal(500, summary.TotalVolume);
        var detail = Assert.Single(details);
        Assert.Equal("Supino", detail.ExerciseName);
        Assert.Equal(10, detail.Reps);
        Assert.Equal(50, detail.LoadKg);
    }
}
