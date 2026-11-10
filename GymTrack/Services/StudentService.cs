using GymTrack.Data;
using GymTrack.Models;
using SQLite;

namespace GymTrack.Services;

public sealed class StudentService(GymTrackDatabase database, IAccountService accounts, AppSession session)
{
    public async Task<IReadOnlyList<AppUser>> GetLinkedStudentsAsync()
    {
        var (employeeId, users) = await RequireAccountAsync(UserRole.Employee);
        var connection = await database.GetConnectionAsync();
        var studentIds = (await connection.QueryAsync<StudentLink>(
            "SELECT StudentId FROM StudentLink WHERE EmployeeId = ?", employeeId))
            .Select(link => link.StudentId).ToHashSet();
        return users.Where(user => user is { Role: UserRole.Consumer, IsActive: true } &&
            studentIds.Contains(user.Id)).ToArray();
    }

    public async Task<StudentDetails> GetDetailsAsync(int studentId)
    {
        var (employeeId, users) = await RequireAccountAsync(UserRole.Employee);
        var student = RequireStudent(users, studentId);
        var connection = await database.GetConnectionAsync();
        StudentDetails result = null!;
        await connection.RunInTransactionAsync(transaction =>
        {
            var link = RequireLink(transaction, studentId, employeeId);
            var workouts = transaction.Query<WorkoutCatalogItem>(
                """
                SELECT w.Id, w.Name, w.Description, COUNT(we.Id) AS ExerciseCount
                FROM WorkoutAssignment a
                JOIN Workout w ON w.Id = a.WorkoutId
                LEFT JOIN WorkoutExercise we ON we.WorkoutId = w.Id
                WHERE a.EmployeeId = ? AND w.OwnerUserId = ? AND w.IsArchived = 0 AND w.IsTemplate = 0
                GROUP BY w.Id ORDER BY w.UpdatedAt DESC, w.Id DESC
                """, employeeId, studentId);
            var history = link.SharesHistory ? ReadHistory(transaction, studentId) : [];
            result = new StudentDetails(student, link.SharesHistory, workouts, history);
        });
        return result;
    }

    public async Task<int> AssignWorkoutAsync(int studentId, int templateId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(templateId);
        var (employeeId, users) = await RequireAccountAsync(UserRole.Employee);
        RequireStudent(users, studentId);
        var connection = await database.GetConnectionAsync();
        var workoutId = 0;
        await connection.RunInTransactionAsync(transaction =>
        {
            RequireLink(transaction, studentId, employeeId);
            workoutId = transaction.ExecuteScalar<int>(
                """
                SELECT w.Id FROM WorkoutAssignment a JOIN Workout w ON w.Id = a.WorkoutId
                WHERE a.EmployeeId = ? AND a.TemplateId = ? AND w.OwnerUserId = ?
                  AND w.IsArchived = 0 AND w.IsTemplate = 0
                LIMIT 1
                """, employeeId, templateId, studentId);
            if (workoutId > 0) return;

            workoutId = WorkoutCatalogService.CopyTemplate(transaction, templateId, studentId);
            transaction.Execute("INSERT INTO WorkoutAssignment (WorkoutId, EmployeeId, TemplateId) VALUES (?, ?, ?)",
                workoutId, employeeId, templateId);
        });
        return workoutId;
    }

    public async Task<IReadOnlyList<StudentConnection>> GetConnectionsAsync()
    {
        var (studentId, users) = await RequireAccountAsync(UserRole.Consumer);
        var connection = await database.GetConnectionAsync();
        var links = (await connection.QueryAsync<StudentLink>(
            "SELECT EmployeeId, SharesHistory FROM StudentLink WHERE StudentId = ?", studentId))
            .ToDictionary(link => link.EmployeeId);
        return users.Where(user => links.ContainsKey(user.Id))
            .Select(user => new StudentConnection(user, links[user.Id].SharesHistory)).ToArray();
    }

    public async Task LinkAsync(string? employeeEmail)
    {
        var (studentId, users) = await RequireAccountAsync(UserRole.Consumer);
        var employee = users.FirstOrDefault(user => user is { Role: UserRole.Employee, IsActive: true } &&
            string.Equals(user.Email, employeeEmail?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Confira o e-mail. Nenhum funcionário ativo foi encontrado.");
        var connection = await database.GetConnectionAsync();
        if (await connection.ExecuteAsync(
                "INSERT OR IGNORE INTO StudentLink (StudentId, EmployeeId) VALUES (?, ?)", studentId, employee.Id) == 0)
            throw new InvalidOperationException("Você já está vinculado a esse funcionário.");
    }

    public async Task SetHistorySharingAsync(int employeeId, bool sharesHistory)
    {
        var (studentId, _) = await RequireAccountAsync(UserRole.Consumer);
        var connection = await database.GetConnectionAsync();
        if (await connection.ExecuteAsync(
                "UPDATE StudentLink SET SharesHistory = ? WHERE StudentId = ? AND EmployeeId = ?",
                sharesHistory, studentId, employeeId) == 0)
            throw new InvalidOperationException("O vínculo foi removido.");
    }

    public async Task UnlinkAsync(int employeeId)
    {
        var (studentId, _) = await RequireAccountAsync(UserRole.Consumer);
        var connection = await database.GetConnectionAsync();
        await connection.ExecuteAsync(
            "DELETE FROM StudentLink WHERE StudentId = ? AND EmployeeId = ?", studentId, employeeId);
    }

    private async Task<(int Id, IReadOnlyList<AppUser> Users)> RequireAccountAsync(UserRole role)
    {
        var actor = session.CurrentUser;
        var hasAccess = role == UserRole.Employee ? session.IsManagementMode : session.IsPersonalMode;
        if (!hasAccess || actor is not { Id: > 0 } || actor.Role != role)
            throw new UnauthorizedAccessException("Sua conta não tem acesso a esta ação.");

        var users = await accounts.GetUsersAsync();
        if (session.CurrentUser?.Id != actor.Id ||
            !(role == UserRole.Employee ? session.IsManagementMode : session.IsPersonalMode) ||
            !users.Any(user => user.Id == actor.Id && user.Role == role && user.IsActive))
            throw new UnauthorizedAccessException("Sua conta não tem acesso a esta ação.");

        return (actor.Id, users);
    }

    private static AppUser RequireStudent(IReadOnlyList<AppUser> users, int studentId) =>
        users.FirstOrDefault(user => user.Id == studentId &&
            user is { Role: UserRole.Consumer, IsActive: true })
        ?? throw new UnauthorizedAccessException("Este aluno não está disponível.");

    private static StudentLink RequireLink(SQLiteConnection transaction, int studentId, int employeeId) =>
        transaction.FindWithQuery<StudentLink>(
            "SELECT * FROM StudentLink WHERE StudentId = ? AND EmployeeId = ?", studentId, employeeId)
        ?? throw new UnauthorizedAccessException("Este aluno não está vinculado à sua conta.");

    private static StudentActivity[] ReadHistory(SQLiteConnection transaction, int studentId) =>
        transaction.Query<StudentActivity>(
            """
            SELECT w.Name AS WorkoutName, ws.StartedAt AS Date, 0 AS IsManual
            FROM WorkoutSession ws JOIN Workout w ON w.Id = ws.WorkoutId
            WHERE w.OwnerUserId = ? AND w.IsTemplate = 0 AND ws.FinishedAt IS NOT NULL
            UNION ALL
            SELECT WorkoutName, PerformedOn AS Date, 1 AS IsManual
            FROM ManualWorkoutLog
            WHERE OwnerUserId = ? AND IsExample = 0
            """, studentId, studentId).OrderByDescending(item => item.LocalDate).ToArray();

    private sealed class StudentLink
    {
        public int StudentId { get; set; }
        public int EmployeeId { get; set; }
        public bool SharesHistory { get; set; }
    }
}
