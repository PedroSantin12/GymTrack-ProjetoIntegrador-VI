using GymTrack.Models;

namespace GymTrack.Data.Dao;

public sealed class SessionDao(GymTrackDatabase database) : ISessionDao
{
    public async Task<IReadOnlyList<WorkoutSession>> GetAllAsync()
    {
        var connection = await database.GetConnectionAsync();
        return await connection.Table<WorkoutSession>()
            .OrderByDescending(session => session.StartedAt)
            .ToListAsync();
    }

    public async Task<WorkoutSession?> GetByIdAsync(int id)
    {
        var connection = await database.GetConnectionAsync();
        return await connection.FindAsync<WorkoutSession>(id);
    }

    public async Task<WorkoutSession?> GetActiveAsync()
    {
        var connection = await database.GetConnectionAsync();
        return await connection.FindWithQueryAsync<WorkoutSession>(
            """
            SELECT *
            FROM WorkoutSession
            WHERE FinishedAt IS NULL
            ORDER BY StartedAt DESC
            LIMIT 1
            """);
    }

    public async Task<IReadOnlyList<SetRecord>> GetSetsAsync(int sessionId)
    {
        var connection = await database.GetConnectionAsync();
        return await connection.Table<SetRecord>()
            .Where(setRecord => setRecord.SessionId == sessionId)
            .OrderBy(setRecord => setRecord.ExerciseId)
            .ThenBy(setRecord => setRecord.SetNumber)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<SetRecord>> GetPreviousSetsAsync(
        int workoutId,
        int exerciseId,
        int excludedSessionId)
    {
        var connection = await database.GetConnectionAsync();
        return await connection.QueryAsync<SetRecord>(
            """
            SELECT sr.*
            FROM SetRecord sr
            WHERE sr.ExerciseId = ?
              AND sr.SessionId = (
                  SELECT ws.Id
                  FROM WorkoutSession ws
                  INNER JOIN SetRecord previousSet
                      ON previousSet.SessionId = ws.Id
                     AND previousSet.ExerciseId = ?
                  WHERE ws.WorkoutId = ?
                    AND ws.Id <> ?
                    AND ws.FinishedAt IS NOT NULL
                  ORDER BY ws.StartedAt DESC
                  LIMIT 1
              )
            ORDER BY sr.SetNumber
            """,
            exerciseId,
            exerciseId,
            workoutId,
            excludedSessionId);
    }

    public async Task<int> InsertAsync(WorkoutSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var connection = await database.GetConnectionAsync();
        session.StartedAt = session.StartedAt == default
            ? DateTime.UtcNow
            : session.StartedAt;

        return await connection.InsertAsync(session);
    }

    public async Task<int> UpdateAsync(WorkoutSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.FinishedAt < session.StartedAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(session.FinishedAt),
                "O término da sessão não pode ocorrer antes do início.");
        }

        var connection = await database.GetConnectionAsync();
        return await connection.UpdateAsync(session);
    }

    public async Task<int> SaveSetAsync(SetRecord setRecord)
    {
        ArgumentNullException.ThrowIfNull(setRecord);

        if (setRecord.SetNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(setRecord.SetNumber),
                "O número da série deve ser positivo.");
        }

        if (setRecord.Reps <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(setRecord.Reps),
                "A quantidade de repetições deve ser positiva.");
        }

        if (!double.IsFinite(setRecord.LoadKg) || setRecord.LoadKg < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(setRecord.LoadKg),
                "A carga deve ser um número não negativo.");
        }

        var connection = await database.GetConnectionAsync();
        var affectedRows = 0;

        await connection.RunInTransactionAsync(transaction =>
        {
            if (setRecord.Id == 0)
            {
                var existing = transaction.FindWithQuery<SetRecord>(
                    """
                    SELECT *
                    FROM SetRecord
                    WHERE SessionId = ? AND ExerciseId = ? AND SetNumber = ?
                    """,
                    setRecord.SessionId,
                    setRecord.ExerciseId,
                    setRecord.SetNumber);

                if (existing is not null)
                {
                    setRecord.Id = existing.Id;
                }
            }

            affectedRows = setRecord.Id == 0
                ? transaction.Insert(setRecord)
                : transaction.Update(setRecord);
        });

        return affectedRows;
    }

    public async Task<int> DeleteSetAsync(int id)
    {
        var connection = await database.GetConnectionAsync();
        return await connection.DeleteAsync<SetRecord>(id);
    }

    public async Task<int> DeleteAsync(int id)
    {
        var connection = await database.GetConnectionAsync();
        var deleted = 0;

        await connection.RunInTransactionAsync(transaction =>
        {
            transaction.Execute("DELETE FROM SetRecord WHERE SessionId = ?", id);
            deleted = transaction.Delete<WorkoutSession>(id);
        });

        return deleted;
    }
}
