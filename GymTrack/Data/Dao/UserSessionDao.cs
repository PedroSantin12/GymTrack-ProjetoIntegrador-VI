using GymTrack.Models;
using GymTrack.Services;

namespace GymTrack.Data.Dao;

public sealed class UserSessionDao(
    ISessionDao inner,
    IWorkoutDao workouts,
    AppSession appSession) : ISessionDao
{
    public async Task<IReadOnlyList<WorkoutSession>> GetAllAsync()
    {
        var workoutIds = await GetOwnedWorkoutIdsAsync();
        if (workoutIds.Count == 0)
        {
            return [];
        }

        return (await inner.GetAllAsync())
            .Where(item => workoutIds.Contains(item.WorkoutId))
            .ToList();
    }

    public async Task<WorkoutSession?> GetByIdAsync(int id)
    {
        if (CurrentConsumerId is null)
        {
            return null;
        }

        var stored = await inner.GetByIdAsync(id);
        return stored is not null && await OwnsWorkoutAsync(stored.WorkoutId)
            ? stored
            : null;
    }

    public async Task<WorkoutSession?> GetActiveAsync() =>
        (await GetAllAsync())
            .Where(item => item.FinishedAt is null)
            .OrderByDescending(item => item.StartedAt)
            .FirstOrDefault();

    public async Task<IReadOnlyList<SetRecord>> GetSetsAsync(int sessionId) =>
        await GetByIdAsync(sessionId) is null ? [] : await inner.GetSetsAsync(sessionId);

    public async Task<IReadOnlyList<SetRecord>> GetPreviousSetsAsync(
        int workoutId,
        int exerciseId,
        int excludedSessionId) =>
        await OwnsWorkoutAsync(workoutId)
            ? await inner.GetPreviousSetsAsync(workoutId, exerciseId, excludedSessionId)
            : [];

    public async Task<IReadOnlyList<SessionSummary>> GetSummariesAsync(
        int? workoutId = null,
        DateTime? startedFrom = null,
        DateTime? startedUntil = null)
    {
        var workoutIds = await GetOwnedWorkoutIdsAsync();
        if (workoutIds.Count == 0 || workoutId is int id && !workoutIds.Contains(id))
        {
            return [];
        }

        return (await inner.GetSummariesAsync(workoutId, startedFrom, startedUntil))
            .Where(item => workoutIds.Contains(item.WorkoutId))
            .ToList();
    }

    public async Task<IReadOnlyList<SessionSetDetail>> GetSetDetailsAsync(int sessionId) =>
        await GetByIdAsync(sessionId) is null ? [] : await inner.GetSetDetailsAsync(sessionId);

    public async Task<int> InsertAsync(WorkoutSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        await RequireOwnedWorkoutAsync(session.WorkoutId);
        if (session.Id != 0)
        {
            await RequireOwnedSessionAsync(session.Id);
        }

        return await inner.InsertAsync(session);
    }

    public async Task<int> UpdateAsync(WorkoutSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        await RequireOwnedSessionAsync(session.Id);
        await RequireOwnedWorkoutAsync(session.WorkoutId);
        return await inner.UpdateAsync(session);
    }

    public async Task<int> SaveSetAsync(SetRecord setRecord)
    {
        ArgumentNullException.ThrowIfNull(setRecord);
        await RequireOwnedSessionAsync(setRecord.SessionId);
        if (setRecord.Id != 0 &&
            !(await inner.GetSetsAsync(setRecord.SessionId)).Any(item => item.Id == setRecord.Id))
        {
            throw AccessDenied();
        }

        return await inner.SaveSetAsync(setRecord);
    }

    public async Task<int> DeleteSetAsync(int id)
    {
        foreach (var ownedSession in await GetAllAsync())
        {
            if ((await inner.GetSetsAsync(ownedSession.Id)).Any(item => item.Id == id))
            {
                return await inner.DeleteSetAsync(id);
            }
        }

        throw AccessDenied();
    }

    public async Task<int> DeleteAsync(int id)
    {
        await RequireOwnedSessionAsync(id);
        return await inner.DeleteAsync(id);
    }

    private int? CurrentConsumerId => appSession.CurrentUser is
        { Id: > 0, Role: UserRole.Consumer, IsActive: true } user ? user.Id : null;

    private async Task<HashSet<int>> GetOwnedWorkoutIdsAsync()
    {
        if (CurrentConsumerId is not int userId)
        {
            return [];
        }

        return (await workouts.GetAllAsync())
            .Where(workout => workout.OwnerUserId == userId && !workout.IsTemplate)
            .Select(workout => workout.Id)
            .ToHashSet();
    }

    private async Task<bool> OwnsWorkoutAsync(int workoutId)
    {
        if (CurrentConsumerId is not int userId)
        {
            return false;
        }

        return await workouts.GetByIdAsync(workoutId) is
            { IsTemplate: false } workout && workout.OwnerUserId == userId;
    }

    private async Task RequireOwnedWorkoutAsync(int workoutId)
    {
        if (!await OwnsWorkoutAsync(workoutId))
        {
            throw AccessDenied();
        }
    }

    private async Task RequireOwnedSessionAsync(int sessionId)
    {
        if (await GetByIdAsync(sessionId) is null)
        {
            throw AccessDenied();
        }
    }

    private static UnauthorizedAccessException AccessDenied() =>
        new("Esta sessão de treino não pertence à sua conta.");
}
