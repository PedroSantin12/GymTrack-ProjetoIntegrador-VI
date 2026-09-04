using GymTrack.Models;
using SQLite;

namespace GymTrack.Data.Dao;

public sealed class WorkoutDao(GymTrackDatabase database) : IWorkoutDao
{
    public async Task<IReadOnlyList<Workout>> GetAllAsync()
    {
        var connection = await database.GetConnectionAsync();
        return await connection.Table<Workout>()
            .OrderByDescending(workout => workout.UpdatedAt)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<WorkoutSummary>> GetSummariesAsync()
    {
        var connection = await database.GetConnectionAsync();
        return await connection.QueryAsync<WorkoutSummary>(
            """
            SELECT
                w.Id,
                w.Name,
                w.Description,
                w.CreatedAt,
                w.UpdatedAt,
                CAST(COUNT(DISTINCT we.Id) AS INTEGER) AS ExerciseCount,
                MAX(ws.StartedAt) AS LastSessionAt
            FROM Workout w
            LEFT JOIN WorkoutExercise we ON we.WorkoutId = w.Id
            LEFT JOIN WorkoutSession ws ON ws.WorkoutId = w.Id
            GROUP BY w.Id, w.Name, w.Description, w.CreatedAt, w.UpdatedAt
            ORDER BY w.UpdatedAt DESC
            """);
    }

    public async Task<Workout?> GetByIdAsync(int id)
    {
        var connection = await database.GetConnectionAsync();
        return await connection.FindAsync<Workout>(id);
    }

    public async Task<IReadOnlyList<WorkoutExercise>> GetExercisesAsync(int workoutId)
    {
        var connection = await database.GetConnectionAsync();
        return await connection.Table<WorkoutExercise>()
            .Where(item => item.WorkoutId == workoutId)
            .OrderBy(item => item.OrderIndex)
            .ToListAsync();
    }

    public async Task<int> SaveAsync(
        Workout workout,
        IReadOnlyList<WorkoutExercise> exercises)
    {
        ArgumentNullException.ThrowIfNull(workout);
        ArgumentNullException.ThrowIfNull(exercises);

        var connection = await database.GetConnectionAsync();
        await connection.RunInTransactionAsync(transaction =>
        {
            ValidateComposition(transaction, exercises);
            SaveWorkout(transaction, workout);
            ReplaceExercises(transaction, workout.Id, exercises);
        });

        return workout.Id;
    }

    public async Task<bool> HasSessionsAsync(int id)
    {
        var connection = await database.GetConnectionAsync();
        var count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM WorkoutSession WHERE WorkoutId = ?",
            id);

        return count > 0;
    }

    public async Task<int> DeleteAsync(int id)
    {
        var connection = await database.GetConnectionAsync();
        var deleted = 0;

        await connection.RunInTransactionAsync(transaction =>
        {
            if (transaction.ExecuteScalar<int>(
                    "SELECT COUNT(1) FROM WorkoutSession WHERE WorkoutId = ?",
                    id) > 0)
            {
                throw new InvalidOperationException(
                    "Treinos com sessões registradas não podem ser excluídos.");
            }

            transaction.Execute(
                "DELETE FROM WorkoutExercise WHERE WorkoutId = ?",
                id);
            deleted = transaction.Delete<Workout>(id);
        });

        return deleted;
    }

    private static void SaveWorkout(SQLiteConnection transaction, Workout workout)
    {
        var now = DateTime.UtcNow;

        if (workout.Id == 0)
        {
            workout.CreatedAt = workout.CreatedAt == default ? now : workout.CreatedAt;
            workout.UpdatedAt = now;
            transaction.Insert(workout);
            return;
        }

        var existing = transaction.Find<Workout>(workout.Id) ??
                       throw new InvalidOperationException("Treino não encontrado.");

        workout.CreatedAt = existing.CreatedAt;
        workout.UpdatedAt = now;
        transaction.Update(workout);
    }

    private static void ReplaceExercises(
        SQLiteConnection transaction,
        int workoutId,
        IReadOnlyList<WorkoutExercise> exercises)
    {
        transaction.Execute(
            "DELETE FROM WorkoutExercise WHERE WorkoutId = ?",
            workoutId);

        for (var index = 0; index < exercises.Count; index++)
        {
            var item = exercises[index];

            item.Id = 0;
            item.WorkoutId = workoutId;
            item.OrderIndex = index;
            transaction.Insert(item);
        }
    }

    private static void ValidateComposition(
        SQLiteConnection transaction,
        IReadOnlyList<WorkoutExercise> exercises)
    {
        var exerciseIds = new HashSet<int>();

        foreach (var item in exercises)
        {
            if (!exerciseIds.Add(item.ExerciseId))
            {
                throw new InvalidOperationException(
                    "Um exercício não pode aparecer duas vezes no mesmo treino.");
            }

            if (transaction.Find<Exercise>(item.ExerciseId) is null)
            {
                throw new InvalidOperationException(
                    $"Exercício {item.ExerciseId} não encontrado.");
            }

            if (item.PlannedSets is < 1 or > 20)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(item.PlannedSets),
                    "A quantidade de séries deve ficar entre 1 e 20.");
            }

            if (item.PlannedReps is < 1 or > 100)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(item.PlannedReps),
                    "A quantidade de repetições deve ficar entre 1 e 100.");
            }

            if (item.PlannedLoad is double plannedLoad &&
                (!double.IsFinite(plannedLoad) || plannedLoad < 0))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(item.PlannedLoad),
                    "A carga planejada não pode ser negativa.");
            }
        }
    }
}
