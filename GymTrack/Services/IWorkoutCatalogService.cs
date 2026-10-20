using GymTrack.Models;

namespace GymTrack.Services;

public interface IWorkoutCatalogService
{
    Task<IReadOnlyList<WorkoutCatalogItem>> GetTemplatesAsync(string? search = null);

    Task<IReadOnlyList<WorkoutCatalogItem>> GetPersonalAsync(int userId);

    Task<int> CreatePersonalAsync(int userId);

    Task<int> CreateTemplateAsync();

    Task<int> CopyTemplateAsync(int templateId, int userId);

    Task<int> DuplicatePersonalAsync(int workoutId, int userId);

    Task DeletePersonalAsync(int workoutId, int userId);

    IReadOnlyList<StarterWorkoutPlan> GetStarterPlans();

    Task<int> CreateFromStarterAsync(string key, int userId);

    Task DeleteTemplateAsync(int id);

    Task<int> CountTemplatesAsync();
}
