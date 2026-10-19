using GymTrack.Data.Dao;
using GymTrack.Models;
using GymTrack.Services;

namespace GymTrack.Tests;

public sealed class UserSessionDaoTests
{
    [Fact]
    public async Task Reads_ExposeOnlyCurrentConsumersPersonalHistory()
    {
        await using var context = await TestDatabase.CreateAsync();
        var workouts = new WorkoutDao(context.Database);
        var raw = new SessionDao(context.Database);
        var appSession = SignIn(1);
        var scoped = CreateScoped(context, appSession);
        var ownWorkout = await CreateWorkoutAsync(workouts, 1);
        var foreignWorkout = await CreateWorkoutAsync(workouts, 2);
        var template = await CreateWorkoutAsync(workouts, 1, isTemplate: true);
        var unassignedWorkout = await CreateWorkoutAsync(workouts, null);
        var own = await CreateSessionAsync(raw, ownWorkout.Id, 1);
        var foreign = await CreateSessionAsync(raw, foreignWorkout.Id, 2);
        var templateSession = await CreateSessionAsync(raw, template.Id, 3);
        var unassigned = await CreateSessionAsync(raw, unassignedWorkout.Id, 4);
        var exercise = await CreateExerciseAsync(context);
        await CreateSetAsync(raw, own.Id, exercise.Id, 10);
        await CreateSetAsync(raw, foreign.Id, exercise.Id, 20);
        await CreateSetAsync(raw, templateSession.Id, exercise.Id, 30);
        var foreignActive = new WorkoutSession { WorkoutId = foreignWorkout.Id };
        await raw.InsertAsync(foreignActive);

        Assert.Equal(own.Id, Assert.Single(await scoped.GetAllAsync()).Id);
        Assert.Equal(own.Id, (await scoped.GetByIdAsync(own.Id))?.Id);
        Assert.Null(await scoped.GetActiveAsync());
        Assert.Single(await scoped.GetSetsAsync(own.Id));
        Assert.Single(await scoped.GetSetDetailsAsync(own.Id));
        var summary = Assert.Single(await scoped.GetSummariesAsync());
        Assert.Equal(own.Id, summary.Id);
        Assert.Equal(100, summary.TotalVolume);
        Assert.Single(await scoped.GetSummariesAsync(
            ownWorkout.Id, own.StartedAt, own.StartedAt.AddMinutes(1)));
        Assert.Empty(await scoped.GetSummariesAsync(startedFrom: own.StartedAt.AddDays(1)));

        foreach (var hidden in new[] { foreign, foreignActive, templateSession, unassigned })
        {
            Assert.Null(await scoped.GetByIdAsync(hidden.Id));
            Assert.Empty(await scoped.GetSetsAsync(hidden.Id));
            Assert.Empty(await scoped.GetSetDetailsAsync(hidden.Id));
            Assert.Empty(await scoped.GetSummariesAsync(hidden.WorkoutId));
        }

        appSession.SignIn(new AppUser { Id = 2, Role = UserRole.Consumer });

        Assert.Equal(
            new[] { foreignActive.Id, foreign.Id },
            (await scoped.GetAllAsync()).Select(item => item.Id));
        Assert.Equal(foreignActive.Id, (await scoped.GetActiveAsync())?.Id);
        Assert.Equal(foreign.Id, Assert.Single(await scoped.GetSummariesAsync()).Id);
        Assert.Null(await scoped.GetByIdAsync(own.Id));
    }

    [Fact]
    public async Task ActiveSessions_AreIndependentForEachConsumer()
    {
        await using var context = await TestDatabase.CreateAsync();
        var workouts = new WorkoutDao(context.Database);
        var appSession = SignIn(1);
        var scoped = CreateScoped(context, appSession);
        var firstWorkout = await CreateWorkoutAsync(workouts, 1);
        var alternateWorkout = await CreateWorkoutAsync(workouts, 1);
        var secondWorkout = await CreateWorkoutAsync(workouts, 2);
        var first = new WorkoutSession { WorkoutId = firstWorkout.Id };
        await scoped.InsertAsync(first);

        appSession.SignIn(new AppUser { Id = 2, Role = UserRole.Consumer });
        Assert.Null(await scoped.GetActiveAsync());
        var second = new WorkoutSession { WorkoutId = secondWorkout.Id };
        Assert.Equal(1, await scoped.InsertAsync(second));
        Assert.Equal(second.Id, (await scoped.GetActiveAsync())?.Id);

        appSession.SignIn(new AppUser { Id = 1, Role = UserRole.Consumer });
        Assert.Equal(first.Id, (await scoped.GetActiveAsync())?.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => scoped.InsertAsync(
            new WorkoutSession { WorkoutId = alternateWorkout.Id }));
        first.FinishedAt = DateTime.UtcNow;
        await scoped.UpdateAsync(first);
        var replacement = new WorkoutSession { WorkoutId = alternateWorkout.Id };

        Assert.Equal(1, await scoped.InsertAsync(replacement));
        Assert.Equal(replacement.Id, (await scoped.GetActiveAsync())?.Id);
        Assert.Null((await new SessionDao(context.Database).GetByIdAsync(second.Id))?.FinishedAt);
    }

    [Fact]
    public async Task SessionMutations_RejectForeignIdsAndForgedWorkoutReassignment()
    {
        await using var context = await TestDatabase.CreateAsync();
        var workouts = new WorkoutDao(context.Database);
        var raw = new SessionDao(context.Database);
        var scoped = CreateScoped(context, SignIn(1));
        var ownWorkout = await CreateWorkoutAsync(workouts, 1);
        var foreignWorkout = await CreateWorkoutAsync(workouts, 2);
        var template = await CreateWorkoutAsync(workouts, 1, isTemplate: true);
        var own = await CreateSessionAsync(raw, ownWorkout.Id, 1);
        var foreign = await CreateSessionAsync(raw, foreignWorkout.Id, 2);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scoped.UpdateAsync(
            new WorkoutSession
            {
                Id = foreign.Id,
                WorkoutId = ownWorkout.Id,
                StartedAt = foreign.StartedAt,
                FinishedAt = foreign.FinishedAt,
                Notes = "Alteração indevida"
            }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scoped.UpdateAsync(
            new WorkoutSession
            {
                Id = own.Id,
                WorkoutId = foreignWorkout.Id,
                StartedAt = own.StartedAt,
                FinishedAt = own.FinishedAt
            }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scoped.DeleteAsync(foreign.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scoped.InsertAsync(
            new WorkoutSession { WorkoutId = foreignWorkout.Id }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scoped.InsertAsync(
            new WorkoutSession { WorkoutId = template.Id }));

        Assert.Equal(foreignWorkout.Id, (await raw.GetByIdAsync(foreign.Id))?.WorkoutId);
        Assert.Null((await raw.GetByIdAsync(foreign.Id))?.Notes);
        Assert.Equal(ownWorkout.Id, (await raw.GetByIdAsync(own.Id))?.WorkoutId);

        own.Notes = "Treino concluído";
        Assert.Equal(1, await scoped.UpdateAsync(own));
        Assert.Equal(own.Notes, (await raw.GetByIdAsync(own.Id))?.Notes);
        Assert.Equal(1, await scoped.DeleteAsync(own.Id));
        Assert.Null(await raw.GetByIdAsync(own.Id));
    }

    [Fact]
    public async Task SetMutations_RejectForgedRecordsAndForeignDeletes()
    {
        await using var context = await TestDatabase.CreateAsync();
        var workouts = new WorkoutDao(context.Database);
        var raw = new SessionDao(context.Database);
        var scoped = CreateScoped(context, SignIn(1));
        var ownWorkout = await CreateWorkoutAsync(workouts, 1);
        var foreignWorkout = await CreateWorkoutAsync(workouts, 2);
        var ownSession = await CreateSessionAsync(raw, ownWorkout.Id, 1);
        var foreignSession = await CreateSessionAsync(raw, foreignWorkout.Id, 2);
        var exercise = await CreateExerciseAsync(context);
        var ownSet = await CreateSetAsync(raw, ownSession.Id, exercise.Id, 10);
        var foreignSet = await CreateSetAsync(raw, foreignSession.Id, exercise.Id, 20);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scoped.SaveSetAsync(
            new SetRecord
            {
                Id = foreignSet.Id,
                SessionId = ownSession.Id,
                ExerciseId = exercise.Id,
                SetNumber = 1,
                Reps = 50,
                LoadKg = 10
            }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scoped.SaveSetAsync(
            new SetRecord
            {
                Id = ownSet.Id,
                SessionId = foreignSession.Id,
                ExerciseId = exercise.Id,
                SetNumber = 1,
                Reps = 50,
                LoadKg = 10
            }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scoped.DeleteSetAsync(foreignSet.Id));

        Assert.Equal(20, Assert.Single(await raw.GetSetsAsync(foreignSession.Id)).Reps);
        ownSet.Reps = 12;
        Assert.Equal(1, await scoped.SaveSetAsync(ownSet));
        Assert.Equal(12, Assert.Single(await scoped.GetSetsAsync(ownSession.Id)).Reps);
        Assert.Equal(1, await scoped.DeleteSetAsync(ownSet.Id));
        Assert.Empty(await raw.GetSetsAsync(ownSession.Id));
        Assert.Single(await raw.GetSetsAsync(foreignSession.Id));
    }

    [Fact]
    public async Task PreviousPerformance_RejectsForeignAndTemplateWorkouts()
    {
        await using var context = await TestDatabase.CreateAsync();
        var workouts = new WorkoutDao(context.Database);
        var raw = new SessionDao(context.Database);
        var scoped = CreateScoped(context, SignIn(1));
        var ownWorkout = await CreateWorkoutAsync(workouts, 1);
        var foreignWorkout = await CreateWorkoutAsync(workouts, 2);
        var template = await CreateWorkoutAsync(workouts, 1, isTemplate: true);
        var exercise = await CreateExerciseAsync(context);
        var own = await CreateSessionAsync(raw, ownWorkout.Id, 1);
        var foreign = await CreateSessionAsync(raw, foreignWorkout.Id, 2);
        var templateSession = await CreateSessionAsync(raw, template.Id, 3);
        await CreateSetAsync(raw, own.Id, exercise.Id, 10);
        await CreateSetAsync(raw, foreign.Id, exercise.Id, 20);
        await CreateSetAsync(raw, templateSession.Id, exercise.Id, 30);

        Assert.Equal(10, Assert.Single(await scoped.GetPreviousSetsAsync(
            ownWorkout.Id, exercise.Id, 0)).Reps);
        Assert.Empty(await scoped.GetPreviousSetsAsync(ownWorkout.Id, exercise.Id, own.Id));
        Assert.Empty(await scoped.GetPreviousSetsAsync(foreignWorkout.Id, exercise.Id, 0));
        Assert.Empty(await scoped.GetPreviousSetsAsync(template.Id, exercise.Id, 0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SignedOutAndEmployeeAccounts_CannotReadOrChangePersonalSessions(bool employee)
    {
        await using var context = await TestDatabase.CreateAsync();
        var workouts = new WorkoutDao(context.Database);
        var raw = new SessionDao(context.Database);
        var appSession = new AppSession();
        if (employee)
        {
            appSession.SignIn(new AppUser { Id = 1, Role = UserRole.Employee });
        }

        var scoped = CreateScoped(context, appSession);
        var workout = await CreateWorkoutAsync(workouts, 1);
        var stored = await CreateSessionAsync(raw, workout.Id, 1);
        var exercise = await CreateExerciseAsync(context);
        var set = await CreateSetAsync(raw, stored.Id, exercise.Id, 10);

        Assert.Empty(await scoped.GetAllAsync());
        Assert.Null(await scoped.GetByIdAsync(stored.Id));
        Assert.Null(await scoped.GetActiveAsync());
        Assert.Empty(await scoped.GetSetsAsync(stored.Id));
        Assert.Empty(await scoped.GetSetDetailsAsync(stored.Id));
        Assert.Empty(await scoped.GetSummariesAsync());
        Assert.Empty(await scoped.GetPreviousSetsAsync(workout.Id, exercise.Id, 0));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scoped.InsertAsync(
            new WorkoutSession { WorkoutId = workout.Id }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scoped.UpdateAsync(stored));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scoped.SaveSetAsync(set));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scoped.DeleteSetAsync(set.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scoped.DeleteAsync(stored.Id));

        Assert.NotNull(await raw.GetByIdAsync(stored.Id));
        Assert.Single(await raw.GetSetsAsync(stored.Id));
    }

    private static UserSessionDao CreateScoped(TestDatabase context, AppSession appSession) =>
        new(new SessionDao(context.Database, appSession), new WorkoutDao(context.Database), appSession);

    private static AppSession SignIn(int userId)
    {
        var appSession = new AppSession();
        appSession.SignIn(new AppUser { Id = userId, Role = UserRole.Consumer });
        return appSession;
    }

    private static async Task<Workout> CreateWorkoutAsync(
        IWorkoutDao workouts,
        int? ownerId,
        bool isTemplate = false)
    {
        var workout = new Workout
        {
            Name = "Treino",
            OwnerUserId = ownerId,
            IsTemplate = isTemplate
        };
        await workouts.SaveAsync(workout, []);
        return workout;
    }

    private static async Task<WorkoutSession> CreateSessionAsync(ISessionDao sessions, int workoutId, int day)
    {
        var startedAt = new DateTime(2026, 9, day, 10, 0, 0, DateTimeKind.Utc);
        var session = new WorkoutSession
        {
            WorkoutId = workoutId,
            StartedAt = startedAt,
            FinishedAt = startedAt.AddMinutes(30)
        };
        await sessions.InsertAsync(session);
        return session;
    }

    private static async Task<Exercise> CreateExerciseAsync(TestDatabase context)
    {
        var exercise = new Exercise { Name = "Remada de teste", MuscleGroup = "Costas" };
        await new ExerciseDao(context.Database).InsertAsync(exercise);
        return exercise;
    }

    private static async Task<SetRecord> CreateSetAsync(
        ISessionDao sessions,
        int sessionId,
        int exerciseId,
        int reps)
    {
        var set = new SetRecord
        {
            SessionId = sessionId,
            ExerciseId = exerciseId,
            SetNumber = 1,
            Reps = reps,
            LoadKg = 10
        };
        await sessions.SaveSetAsync(set);
        return set;
    }
}
