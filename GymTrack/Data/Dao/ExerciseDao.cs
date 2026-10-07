using System.Globalization;
using GymTrack.Models;

namespace GymTrack.Data.Dao;

public sealed class ExerciseDao(GymTrackDatabase database) : IExerciseDao
{
    private static readonly CompareInfo PortugueseCompare =
        CultureInfo.GetCultureInfo("pt-BR").CompareInfo;
    private const CompareOptions SearchOptions =
        CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

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
        var normalizedGroup = string.IsNullOrWhiteSpace(muscleGroup) ||
                              muscleGroup.Equals("Todos", StringComparison.OrdinalIgnoreCase)
            ? null
            : muscleGroup.Trim();
        var search = searchText?.Trim() ?? string.Empty;
        var exercises = await GetAllAsync(includeInactive);
        return exercises
            .Where(exercise =>
                PortugueseCompare.IndexOf(exercise.Name, search, SearchOptions) >= 0 &&
                (normalizedGroup is null ||
                 PortugueseCompare.Compare(exercise.MuscleGroup, normalizedGroup, SearchOptions) == 0))
            .OrderBy(exercise => exercise.Name, StringComparer.Create(
                CultureInfo.GetCultureInfo("pt-BR"), ignoreCase: true))
            .ToList();
    }

    public async Task<bool> ExistsAsync(string name, string muscleGroup, int excludedId = 0)
    {
        var exercises = await GetAllAsync(includeInactive: true);
        return exercises.Any(exercise =>
            exercise.Id != excludedId &&
            PortugueseCompare.Compare(exercise.Name, name.Trim(), SearchOptions) == 0 &&
            PortugueseCompare.Compare(exercise.MuscleGroup, muscleGroup.Trim(), SearchOptions) == 0);
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

}
