using GymTrack.Data;
using GymTrack.Models;

namespace GymTrack.Services;

public sealed class ManualWorkoutLogService(GymTrackDatabase database) : IManualWorkoutLogService
{
    private const string DemoEmail = "consumidor@gymtrack.app";

    public async Task<IReadOnlyList<ManualWorkoutLog>> GetForUserAsync(int userId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        var connection = await database.GetConnectionAsync();
        return await connection.QueryAsync<ManualWorkoutLog>(
            "SELECT * FROM ManualWorkoutLog WHERE OwnerUserId = ? ORDER BY PerformedOn DESC, Id DESC",
            userId);
    }

    public async Task<int> AddAsync(int userId, string workoutName, DateTime performedOn, string? notes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        var name = workoutName?.Trim() ?? string.Empty;
        var normalizedNotes = notes?.Trim();
        if (name.Length is < 2 or > 120)
            throw new ArgumentException("Informe um nome entre 2 e 120 caracteres.", nameof(workoutName));
        if (normalizedNotes?.Length > 1000)
            throw new ArgumentException("As observações devem ter no máximo 1000 caracteres.", nameof(notes));

        var entry = new ManualWorkoutLog
        {
            OwnerUserId = userId,
            WorkoutName = name,
            PerformedOn = NormalizeDate(performedOn, nameof(performedOn)),
            Notes = string.IsNullOrEmpty(normalizedNotes) ? null : normalizedNotes
        };
        var connection = await database.GetConnectionAsync();
        await connection.InsertAsync(entry);
        return entry.Id;
    }

    public async Task DeleteAsync(int userId, int id)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        var connection = await database.GetConnectionAsync();
        if (await connection.ExecuteAsync(
                "DELETE FROM ManualWorkoutLog WHERE Id = ? AND OwnerUserId = ?", id, userId) == 0)
            throw new InvalidOperationException("Treino registrado não encontrado.");
    }

    public async Task EnsureDemoExamplesAsync(AppUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (!user.IsActive || user.Role != UserRole.Consumer ||
            !string.Equals(user.Email?.Trim(), DemoEmail, StringComparison.OrdinalIgnoreCase))
            return;

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(user.Id);
        var connection = await database.GetConnectionAsync();
        await connection.RunInTransactionAsync(transaction =>
        {
            if (transaction.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM ManualWorkoutDemoSeed WHERE OwnerUserId = ?", user.Id) != 0)
                return;

            if (transaction.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM ManualWorkoutLog WHERE OwnerUserId = ?", user.Id) == 0)
            {
                var today = DateTime.Today;
                foreach (var (daysAgo, name, notes) in new[]
                {
                    (1, "Peito essencial", "Exemplo de treino registrado manualmente."),
                    (8, "Pernas completas", "Exemplo de treino registrado manualmente."),
                    (15, "Caminhada na esteira", "Exemplo de treino registrado manualmente.")
                })
                {
                    transaction.Insert(new ManualWorkoutLog
                    {
                        OwnerUserId = user.Id,
                        WorkoutName = name,
                        PerformedOn = DateTime.SpecifyKind(today.AddDays(-daysAgo), DateTimeKind.Unspecified),
                        Notes = notes,
                        IsExample = true
                    });
                }
            }

            transaction.Execute("INSERT INTO ManualWorkoutDemoSeed (OwnerUserId) VALUES (?)", user.Id);
        });
    }

    private static DateTime NormalizeDate(DateTime value, string parameterName)
    {
        if (value == default || value.Date > DateTime.Today)
            throw new ArgumentOutOfRangeException(parameterName, "A data não pode estar no futuro.");
        return DateTime.SpecifyKind(value.Date, DateTimeKind.Unspecified);
    }
}
