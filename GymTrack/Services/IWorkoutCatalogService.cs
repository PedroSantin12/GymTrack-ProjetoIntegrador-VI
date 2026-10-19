using GymTrack.Models;

namespace GymTrack.Services;

public interface IWorkoutCatalogService
{
    Task<IReadOnlyList<WorkoutCatalogItem>> GetTemplatesAsync(string? search = null);

    Task<IReadOnlyList<WorkoutCatalogItem>> GetPersonalAsync(int userId);

    Task<int> CreatePersonalAsync(int userId);

    Task<int> CreateTemplateAsync();

    Task<int> CopyTemplateAsync(int templateId, int userId);

    Task DeleteTemplateAsync(int id);

    Task<int> CountTemplatesAsync();
}
