using GymTrack.Data.Dao;
using GymTrack.Models;
using GymTrack.Services;

namespace GymTrack.Tests;

public sealed class AnalyticsServiceTests
{
    [Fact]
    public async Task Progress_CalculatesMaxLoadAndVolumeByFinishedSession()
    {
        await using var context = await TestDatabase.CreateAsync();
        IExerciseDao exerciseDao = new ExerciseDao(context.Database);
        IWorkoutDao workoutDao = new WorkoutDao(context.Database);
        ISessionDao sessionDao = new SessionDao(context.Database);
        var exercise = new Exercise { Name = "Supino Reto", MuscleGroup = "Peito" };
        await exerciseDao.InsertAsync(exercise);
        var workout = new Workout { Name = "Treino A" };
        await workoutDao.SaveAsync(workout, []);

        var first = new WorkoutSession
        {
            WorkoutId = workout.Id,
            StartedAt = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc),
            FinishedAt = new DateTime(2026, 8, 1, 11, 0, 0, DateTimeKind.Utc)
        };
        var second = new WorkoutSession
        {
            WorkoutId = workout.Id,
            StartedAt = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc),
            FinishedAt = new DateTime(2026, 9, 1, 11, 0, 0, DateTimeKind.Utc)
        };
        var unfinished = new WorkoutSession
        {
            WorkoutId = workout.Id,
            StartedAt = new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc)
        };
        await sessionDao.InsertAsync(first);
        await sessionDao.InsertAsync(second);
        await sessionDao.InsertAsync(unfinished);
        foreach (var set in new[]
                 {
                     new SetRecord { SessionId = first.Id, ExerciseId = exercise.Id, SetNumber = 1, Reps = 10, LoadKg = 60 },
                     new SetRecord { SessionId = first.Id, ExerciseId = exercise.Id, SetNumber = 2, Reps = 8, LoadKg = 65 },
                     new SetRecord { SessionId = first.Id, ExerciseId = exercise.Id, SetNumber = 3, Reps = 6, LoadKg = 70 },
                     new SetRecord { SessionId = second.Id, ExerciseId = exercise.Id, SetNumber = 1, Reps = 8, LoadKg = 72.5 },
                     new SetRecord { SessionId = unfinished.Id, ExerciseId = exercise.Id, SetNumber = 1, Reps = 20, LoadKg = 100 }
                 })
        {
            await sessionDao.SaveSetAsync(set);
        }

        var points = await new AnalyticsService(sessionDao).GetExerciseProgressAsync(exercise.Id);

        Assert.Equal(2, points.Count);
        Assert.Equal(70, points[0].MaxLoad);
        Assert.Equal(1540, points[0].Volume);
        Assert.Equal(72.5, points[1].MaxLoad);
        Assert.Equal(580, points[1].Volume);
    }
}
