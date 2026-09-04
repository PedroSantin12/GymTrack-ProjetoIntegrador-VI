using GymTrack.Data.Dao;

namespace GymTrack.Services;

public sealed record ExerciseProgressPoint(
    DateTime SessionDate,
    double MaxLoad,
    double Volume);

public interface IAnalyticsService
{
    Task<IReadOnlyList<ExerciseProgressPoint>> GetExerciseProgressAsync(
        int exerciseId,
        DateTime? startedFrom = null);
}

public sealed class AnalyticsService(ISessionDao sessionDao) : IAnalyticsService
{
    public async Task<IReadOnlyList<ExerciseProgressPoint>> GetExerciseProgressAsync(
        int exerciseId,
        DateTime? startedFrom = null)
    {
        var sessions = (await sessionDao.GetAllAsync())
            .Where(session => session.FinishedAt is not null &&
                              (startedFrom is null || session.StartedAt >= startedFrom))
            .OrderBy(session => session.StartedAt);
        var points = new List<ExerciseProgressPoint>();

        foreach (var session in sessions)
        {
            var sets = (await sessionDao.GetSetsAsync(session.Id))
                .Where(set => set.ExerciseId == exerciseId &&
                              set.Reps > 0 &&
                              double.IsFinite(set.LoadKg) &&
                              set.LoadKg >= 0)
                .ToList();
            if (sets.Count == 0)
            {
                continue;
            }

            points.Add(new(
                session.StartedAt,
                sets.Max(set => set.LoadKg),
                sets.Sum(set => set.Reps * set.LoadKg)));
        }

        return points;
    }
}
