using GymTrack.Models;

namespace GymTrack.Services;

public interface IManualWorkoutLogService
{
    Task<IReadOnlyList<ManualWorkoutLog>> GetForUserAsync(int userId);

    Task<int> AddAsync(int userId, string workoutName, DateTime performedOn, string? notes);

    Task DeleteAsync(int userId, int id);

    Task EnsureDemoExamplesAsync(AppUser user);
}
