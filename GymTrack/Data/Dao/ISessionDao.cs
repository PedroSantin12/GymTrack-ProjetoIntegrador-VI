using GymTrack.Models;

namespace GymTrack.Data.Dao;

public interface ISessionDao
{
    Task<IReadOnlyList<WorkoutSession>> GetAllAsync();

    Task<WorkoutSession?> GetByIdAsync(int id);

    Task<WorkoutSession?> GetActiveAsync();

    Task<IReadOnlyList<SetRecord>> GetSetsAsync(int sessionId);

    Task<IReadOnlyList<SetRecord>> GetPreviousSetsAsync(
        int workoutId,
        int exerciseId,
        int excludedSessionId);

    Task<IReadOnlyList<SessionSummary>> GetSummariesAsync(
        int? workoutId = null,
        DateTime? startedFrom = null,
        DateTime? startedUntil = null) =>
        Task.FromResult<IReadOnlyList<SessionSummary>>([]);

    Task<IReadOnlyList<SessionSetDetail>> GetSetDetailsAsync(int sessionId) =>
        Task.FromResult<IReadOnlyList<SessionSetDetail>>([]);

    Task<int> InsertAsync(WorkoutSession session);

    Task<int> UpdateAsync(WorkoutSession session);

    Task<int> SaveSetAsync(SetRecord setRecord);

    Task<int> DeleteSetAsync(int id);

    Task<int> DeleteAsync(int id);
}
