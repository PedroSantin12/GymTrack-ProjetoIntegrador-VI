using GymTrack.Data;
using GymTrack.Data.Dao;
using GymTrack.Models;

namespace GymTrack.Services;

public sealed class WorkoutCatalogService(
    GymTrackDatabase database,
    IWorkoutDao workoutDao) : IWorkoutCatalogService
{
    private static readonly IReadOnlyList<StarterWorkoutPlan> StarterPlans =
    [
        new("full-body", "Full Body", "Corpo inteiro · 3 dias por semana",
        [
            new("Agachamento livre", "Pernas", 3, 10), new("Supino reto", "Peito", 3, 10),
            new("Remada curvada", "Costas", 3, 10), new("Desenvolvimento militar", "Ombros", 3, 10)
        ]),
        new("upper", "Upper Body", "Membros superiores",
        [
            new("Supino reto", "Peito", 3, 10), new("Remada curvada", "Costas", 3, 10),
            new("Desenvolvimento militar", "Ombros", 3, 10), new("Rosca direta", "Bíceps", 3, 12),
            new("Tríceps na polia", "Tríceps", 3, 12)
        ]),
        new("lower", "Lower Body", "Membros inferiores",
        [
            new("Agachamento livre", "Pernas", 4, 8), new("Leg press", "Pernas", 3, 12),
            new("Mesa flexora", "Posterior de coxa", 3, 12), new("Elevação de panturrilha", "Panturrilhas", 3, 15)
        ]),
        new("push", "Push", "Peito, ombros e tríceps",
        [
            new("Supino reto", "Peito", 4, 8), new("Supino inclinado", "Peito", 3, 10),
            new("Desenvolvimento militar", "Ombros", 3, 10), new("Elevação lateral", "Ombros", 3, 12),
            new("Tríceps na polia", "Tríceps", 3, 12)
        ]),
        new("pull", "Pull", "Costas e bíceps",
        [
            new("Puxada frontal", "Costas", 4, 10), new("Remada curvada", "Costas", 3, 10),
            new("Remada baixa", "Costas", 3, 12), new("Rosca direta", "Bíceps", 3, 12),
            new("Rosca martelo", "Bíceps", 3, 12)
        ]),
        new("legs", "Legs", "Quadríceps, posterior e panturrilhas",
        [
            new("Agachamento livre", "Pernas", 4, 8), new("Leg press", "Pernas", 3, 12),
            new("Cadeira extensora", "Quadríceps", 3, 12), new("Mesa flexora", "Posterior de coxa", 3, 12),
            new("Elevação de panturrilha", "Panturrilhas", 4, 15)
        ])
    ];

    public IReadOnlyList<StarterWorkoutPlan> GetStarterPlans() => StarterPlans;

    public async Task<int> CreateFromStarterAsync(string key, int userId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        var plan = StarterPlans.FirstOrDefault(item => item.Key == key)
            ?? throw new InvalidOperationException("Modelo de treino não encontrado.");
        var connection = await database.GetConnectionAsync();
        var copiedId = 0;
        await connection.RunInTransactionAsync(transaction =>
        {
            var available = transaction.Table<Exercise>().Where(item => item.IsActive).ToList();
            var now = DateTime.UtcNow;
            var workout = new Workout
            {
                Name = plan.Name, Description = plan.Description, OwnerUserId = userId,
                CreatedAt = now, UpdatedAt = now
            };
            transaction.Insert(workout);
            for (var index = 0; index < plan.Exercises.Count; index++)
            {
                var item = plan.Exercises[index];
                var exercise = available.FirstOrDefault(existing =>
                    string.Equals(existing.Name, item.Name, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(existing.MuscleGroup, item.MuscleGroup, StringComparison.OrdinalIgnoreCase));
                if (exercise is null)
                {
                    exercise = new Exercise { Name = item.Name, MuscleGroup = item.MuscleGroup };
                    transaction.Insert(exercise);
                    available.Add(exercise);
                }
                transaction.Insert(new WorkoutExercise
                {
                    WorkoutId = workout.Id, ExerciseId = exercise.Id, OrderIndex = index,
                    PlannedSets = item.Sets, PlannedReps = item.Reps
                });
            }
            copiedId = workout.Id;
        });
        return copiedId;
    }

    public async Task<int> DuplicatePersonalAsync(int workoutId, int userId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workoutId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        var connection = await database.GetConnectionAsync();
        var copiedId = 0;
        await connection.RunInTransactionAsync(transaction =>
        {
            var source = transaction.Find<Workout>(workoutId);
            if (source is null || source.IsTemplate || source.IsArchived || source.OwnerUserId != userId)
                throw new InvalidOperationException("Treino não encontrado.");
            copiedId = CloneWorkout(transaction, source, userId, source.Name + " (cópia)", false);
        });
        return copiedId;
    }

    public async Task DeletePersonalAsync(int workoutId, int userId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workoutId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        var connection = await database.GetConnectionAsync();
        await connection.RunInTransactionAsync(transaction =>
        {
            var workout = transaction.Find<Workout>(workoutId);
            if (workout is null || workout.IsTemplate || workout.IsArchived ||
                workout.OwnerUserId != userId)
                throw new InvalidOperationException("Treino não encontrado.");
            var active = transaction.ExecuteScalar<int>(
                "SELECT COUNT(1) FROM WorkoutSession WHERE WorkoutId = ? AND FinishedAt IS NULL",
                workoutId);
            if (active > 0)
                throw new InvalidOperationException("Finalize o treino em andamento antes de excluí-lo.");
            var sessionCount = transaction.ExecuteScalar<int>(
                "SELECT COUNT(1) FROM WorkoutSession WHERE WorkoutId = ?", workoutId);
            if (sessionCount > 0)
            {
                workout.IsArchived = true;
                workout.UpdatedAt = DateTime.UtcNow;
                transaction.Update(workout);
            }
            else
            {
                transaction.Execute("DELETE FROM WorkoutExercise WHERE WorkoutId = ?", workoutId);
                transaction.Delete(workout);
            }
        });
    }

    public async Task<IReadOnlyList<WorkoutCatalogItem>> GetTemplatesAsync(string? search = null)
    {
        var connection = await database.GetConnectionAsync();
        var items = await connection.QueryAsync<WorkoutCatalogItem>(
            CatalogQuery + " WHERE w.IsTemplate = 1 AND w.IsArchived = 0 GROUP BY w.Id ORDER BY w.UpdatedAt DESC, w.Id DESC");
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
            " WHERE w.OwnerUserId = ? AND w.IsTemplate = 0 AND w.IsArchived = 0 GROUP BY w.Id ORDER BY w.UpdatedAt DESC, w.Id DESC",
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
            if (template is null || !template.IsTemplate || template.IsArchived)
            {
                throw new InvalidOperationException("Treino pronto não encontrado.");
            }

            copiedId = CloneWorkout(transaction, template, userId, template.Name, true);
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
            "SELECT COUNT(1) FROM Workout WHERE IsTemplate = 1 AND IsArchived = 0");
    }

    private const string CatalogQuery =
        """
        SELECT w.Id, w.Name, w.Description, COUNT(we.Id) AS ExerciseCount
        FROM Workout w
        LEFT JOIN WorkoutExercise we ON we.WorkoutId = w.Id
        """;

    private static int CloneWorkout(SQLite.SQLiteConnection transaction, Workout source,
        int userId, string name, bool requireExercises)
    {
        var plans = transaction.Table<WorkoutExercise>()
            .Where(item => item.WorkoutId == source.Id).OrderBy(item => item.OrderIndex).ToList();
        if (requireExercises && plans.Count == 0)
            throw new InvalidOperationException("Este treino pronto ainda não possui exercícios.");
        var now = DateTime.UtcNow;
        var copy = new Workout
        {
            Name = name, Description = source.Description, OwnerUserId = userId,
            CreatedAt = now, UpdatedAt = now
        };
        transaction.Insert(copy);
        foreach (var plan in plans)
            transaction.Insert(new WorkoutExercise
            {
                WorkoutId = copy.Id, ExerciseId = plan.ExerciseId, OrderIndex = plan.OrderIndex,
                PlannedSets = plan.PlannedSets, PlannedReps = plan.PlannedReps,
                PlannedLoad = plan.PlannedLoad
            });
        return copy.Id;
    }
}
