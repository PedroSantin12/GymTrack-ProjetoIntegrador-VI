using GymTrack.Models;

namespace GymTrack.Data.Dao;

public sealed class ExerciseDao(GymTrackDatabase database) : IExerciseDao
{
    public async Task<IReadOnlyList<Exercise>> GetAllAsync(bool includeInactive = false)
    {
        var connection = await database.GetConnectionAsync();
        var query = connection.Table<Exercise>();

        if (!includeInactive)
        {
            query = query.Where(exercise => exercise.IsActive);
        }

        return await query.OrderBy(exercise => exercise.Name).ToListAsync();
    }

    public async Task<Exercise?> GetByIdAsync(int id)
    {
        var connection = await database.GetConnectionAsync();
        return await connection.FindAsync<Exercise>(id);
    }

    public async Task<IReadOnlyList<Exercise>> SearchAsync(
        string? searchText,
        string? muscleGroup = null,
        bool includeInactive = false)
    {
        var connection = await database.GetConnectionAsync();
        var normalizedGroup = string.IsNullOrWhiteSpace(muscleGroup) ||
                              muscleGroup.Equals("Todos", StringComparison.OrdinalIgnoreCase)
            ? null
            : muscleGroup.Trim();

        return await connection.QueryAsync<Exercise>(
            """
            SELECT *
            FROM Exercise
            WHERE (? = 1 OR IsActive = 1)
              AND Name LIKE ? ESCAPE '\' COLLATE NOCASE
              AND (? IS NULL OR MuscleGroup = ? COLLATE NOCASE)
            ORDER BY Name COLLATE NOCASE
            """,
            includeInactive ? 1 : 0,
            ToLikePattern(searchText),
            normalizedGroup,
            normalizedGroup);
    }

    public async Task<bool> ExistsAsync(string name, string muscleGroup, int excludedId = 0)
    {
        var connection = await database.GetConnectionAsync();
        var count = await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(1)
            FROM Exercise
            WHERE Name = ? COLLATE NOCASE
              AND MuscleGroup = ? COLLATE NOCASE
              AND Id <> ?
            """,
            name.Trim(),
            muscleGroup.Trim(),
            excludedId);

        return count > 0;
    }

    public async Task<int> InsertAsync(Exercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        var connection = await database.GetConnectionAsync();
        return await connection.InsertAsync(exercise);
    }

    public async Task<int> UpdateAsync(Exercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        var connection = await database.GetConnectionAsync();
        return await connection.UpdateAsync(exercise);
    }

    public async Task<int> ArchiveAsync(int id)
    {
        var connection = await database.GetConnectionAsync();
        return await connection.ExecuteAsync(
            "UPDATE Exercise SET IsActive = 0 WHERE Id = ?",
            id);
    }

    public async Task<int> DeleteAsync(int id)
    {
        var connection = await database.GetConnectionAsync();
        return await connection.DeleteAsync<Exercise>(id);
    }

    public async Task<bool> HasHistoryAsync(int id)
    {
        var connection = await database.GetConnectionAsync();
        var count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM SetRecord WHERE ExerciseId = ?",
            id);

        return count > 0;
    }

    public async Task<bool> HasReferencesAsync(int id)
    {
        var connection = await database.GetConnectionAsync();
        var count = await connection.ExecuteScalarAsync<int>(
            """
            SELECT
                (SELECT COUNT(1) FROM WorkoutExercise WHERE ExerciseId = ?) +
                (SELECT COUNT(1) FROM SetRecord WHERE ExerciseId = ?)
            """,
            id,
            id);

        return count > 0;
    }

    private static string ToLikePattern(string? value)
    {
        var escaped = (value ?? string.Empty)
            .Trim()
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

        return $"%{escaped}%";
    }
}
