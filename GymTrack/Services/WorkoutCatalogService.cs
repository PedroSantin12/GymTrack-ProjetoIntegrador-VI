using GymTrack.Data;
using GymTrack.Data.Dao;
using GymTrack.Models;

namespace GymTrack.Services;

public sealed class WorkoutCatalogService(
    GymTrackDatabase database,
    IWorkoutDao workoutDao) : IWorkoutCatalogService
{
    public async Task<IReadOnlyList<WorkoutCatalogItem>> GetTemplatesAsync(string? search = null)
    {
        var connection = await database.GetConnectionAsync();
        var items = await connection.QueryAsync<WorkoutCatalogItem>(
            CatalogQuery + " WHERE w.IsTemplate = 1 GROUP BY w.Id ORDER BY w.UpdatedAt DESC, w.Id DESC");
        var term = search?.Trim();

        return string.IsNullOrEmpty(term)
            ? items
            : items.Where(item => item.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
                                  (item.Description?.Contains(term, StringComparison.CurrentCultureIgnoreCase) ?? false))
                .ToArray();
    }

    public async Task<IReadOnlyList<WorkoutCatalogItem>> GetPersonalAsync(int userId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        var connection = await database.GetConnectionAsync();
        return await connection.QueryAsync<WorkoutCatalogItem>(
            CatalogQuery +
            " WHERE w.OwnerUserId = ? AND w.IsTemplate = 0 GROUP BY w.Id ORDER BY w.UpdatedAt DESC, w.Id DESC",
            userId);
    }

    public Task<int> CreatePersonalAsync(int userId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        return workoutDao.SaveAsync(new Workout
        {
            Name = "Meu novo treino",
            OwnerUserId = userId
        }, []);
    }

    public Task<int> CreateTemplateAsync()
    {
        return workoutDao.SaveAsync(new Workout
        {
            Name = "Novo treino pronto",
            IsTemplate = true
        }, []);
    }

    public async Task<int> CopyTemplateAsync(int templateId, int userId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(templateId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);

        var connection = await database.GetConnectionAsync();
        var copiedId = 0;
        await connection.RunInTransactionAsync(transaction =>
        {
            var template = transaction.Find<Workout>(templateId);
            if (template is null || !template.IsTemplate)
            {
                throw new InvalidOperationException("Treino pronto não encontrado.");
            }

            var plans = transaction.Table<WorkoutExercise>()
                .Where(item => item.WorkoutId == templateId)
                .OrderBy(item => item.OrderIndex)
                .ToList();
            if (plans.Count == 0)
            {
                throw new InvalidOperationException("Este treino pronto ainda não possui exercícios.");
            }

            var now = DateTime.UtcNow;
            var personal = new Workout
            {
                Name = template.Name,
                Description = template.Description,
                OwnerUserId = userId,
                CreatedAt = now,
                UpdatedAt = now
            };
            transaction.Insert(personal);

            foreach (var plan in plans)
            {
                transaction.Insert(new WorkoutExercise
                {
                    WorkoutId = personal.Id,
                    ExerciseId = plan.ExerciseId,
                    OrderIndex = plan.OrderIndex,
                    PlannedSets = plan.PlannedSets,
                    PlannedReps = plan.PlannedReps,
                    PlannedLoad = plan.PlannedLoad
                });
            }

            copiedId = personal.Id;
        });

        return copiedId;
    }

    public async Task DeleteTemplateAsync(int id)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        var workout = await workoutDao.GetByIdAsync(id);
        if (workout is null || !workout.IsTemplate)
        {
            throw new InvalidOperationException("Treino pronto não encontrado.");
        }

        await workoutDao.DeleteAsync(id);
    }

    public async Task<int> CountTemplatesAsync()
    {
        var connection = await database.GetConnectionAsync();
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Workout WHERE IsTemplate = 1");
    }

    private const string CatalogQuery =
        """
        SELECT w.Id, w.Name, w.Description, COUNT(we.Id) AS ExerciseCount
        FROM Workout w
        LEFT JOIN WorkoutExercise we ON we.WorkoutId = w.Id
        """;
}
