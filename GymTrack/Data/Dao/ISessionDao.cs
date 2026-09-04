using GymTrack.Models;

namespace GymTrack.Data.Dao;

public interface ISessionDao
{
    Task<IReadOnlyList<WorkoutSession>> GetAllAsync();

    Task<WorkoutSession?> GetByIdAsync(int id);

    Task<IReadOnlyList<SetRecord>> GetSetsAsync(int sessionId);

    Task<int> InsertAsync(WorkoutSession session);

    Task<int> UpdateAsync(WorkoutSession session);

    Task<int> SaveSetAsync(SetRecord setRecord);

    Task<int> DeleteSetAsync(int id);

    Task<int> DeleteAsync(int id);
}
