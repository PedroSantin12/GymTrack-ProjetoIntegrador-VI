using GymTrack.Models;

namespace GymTrack.Data.Dao;

public interface IExerciseDao
{
    Task<IReadOnlyList<Exercise>> GetAllAsync(bool includeInactive = false);

    Task<Exercise?> GetByIdAsync(int id);

    Task<IReadOnlyList<Exercise>> SearchAsync(
        string? searchText,
        string? muscleGroup = null,
        bool includeInactive = false);

    Task<bool> ExistsAsync(string name, string muscleGroup, int excludedId = 0);

    Task<int> InsertAsync(Exercise exercise);

    Task<int> UpdateAsync(Exercise exercise);

    Task<int> ArchiveAsync(int id);

    Task<int> DeleteAsync(int id);

    Task<bool> HasHistoryAsync(int id);

    Task<bool> HasReferencesAsync(int id);
}
