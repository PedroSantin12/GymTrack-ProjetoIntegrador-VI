using GymTrack.Data;
using GymTrack.Data.Dao;
using GymTrack.Models;

namespace GymTrack.Services;

public sealed class StudentService(IAccountService accounts, AppSession session, GymTrackDatabase database,
    IManualWorkoutLogService manualLogs, IBodyMeasurementService measurements)
{
    private readonly SessionDao _workouts = new(database);

    public async Task<IReadOnlyList<AppUser>> GetStudentsAsync() =>
        (await GetAccountsForManagementAsync()).Users.Where(IsStudent).ToArray();

    public async Task<AppUser> GetStudentAsync(int studentId) =>
        FindStudent((await GetAccountsForManagementAsync()).Users, studentId);

    public async Task<StudentDetails> GetDetailsAsync(int studentId)
    {
        var (employeeId, users) = await GetAccountsForManagementAsync();
        var student = FindStudent(users, studentId);
        var sessions = _workouts.GetSummariesForOwnerAsync(studentId);
        var logs = manualLogs.GetForUserAsync(studentId);
        var history = measurements.GetForUserAsync(studentId);
        await Task.WhenAll(sessions, logs, history);
        RequireCurrentEmployee(employeeId);

        return new StudentDetails(student, await sessions,
            (await logs).Where(log => !log.IsExample).ToArray(), await history);
    }

    public async Task<(int Students, int RecordedWorkouts)> GetDashboardCountsAsync()
    {
        var (employeeId, users) = await GetAccountsForManagementAsync();
        var studentIds = users.Where(IsStudent).Select(user => user.Id).ToHashSet();
        var connection = await database.GetConnectionAsync();
        var counts = await connection.QueryAsync<WorkoutCount>(
            """
            SELECT OwnerUserId, COUNT(*) AS Total
            FROM (
                SELECT w.OwnerUserId
                FROM WorkoutSession ws
                INNER JOIN Workout w ON w.Id = ws.WorkoutId
                WHERE ws.FinishedAt IS NOT NULL AND w.IsTemplate = 0 AND w.OwnerUserId IS NOT NULL
                UNION ALL
                SELECT OwnerUserId
                FROM ManualWorkoutLog
                WHERE IsExample = 0
            )
            GROUP BY OwnerUserId
            """);
        RequireCurrentEmployee(employeeId);

        return (studentIds.Count, counts.Where(row => studentIds.Contains(row.OwnerUserId)).Sum(row => row.Total));
    }

    public async Task<AppUser> UpdateStudentAsync(int studentId, string name, string email, string? phone)
    {
        var (employeeId, users) = await GetAccountsForManagementAsync();
        FindStudent(users, studentId);
        return await accounts.UpdateStudentDetailsAsync(employeeId, studentId, name, email, phone);
    }

    private async Task<(int EmployeeId, IReadOnlyList<AppUser> Users)> GetAccountsForManagementAsync()
    {
        if (!session.IsEmployee || session.CurrentUser is not { Id: > 0 } employee)
            throw AccessDenied();

        var users = await accounts.GetUsersAsync();
        RequireCurrentEmployee(employee.Id);
        if (!users.Any(user => user.Id == employee.Id && user.Role == UserRole.Employee && user.IsActive))
            throw AccessDenied();

        return (employee.Id, users);
    }

    private void RequireCurrentEmployee(int employeeId)
    {
        if (!session.IsEmployee || session.CurrentUser?.Id != employeeId)
            throw AccessDenied();
    }

    private static UnauthorizedAccessException AccessDenied() =>
        new("Sua conta não tem acesso a esta ação.");

    private static AppUser FindStudent(IEnumerable<AppUser> users, int studentId) =>
        users.FirstOrDefault(user => user.Id == studentId && IsStudent(user))
        ?? throw new UnauthorizedAccessException("Este aluno não está disponível.");

    private static bool IsStudent(AppUser user) => user.Role == UserRole.Consumer;

    private sealed class WorkoutCount
    {
        public int OwnerUserId { get; set; }

        public int Total { get; set; }
    }
}

public sealed record StudentDetails(AppUser Account, IReadOnlyList<SessionSummary> Sessions,
    IReadOnlyList<ManualWorkoutLog> ManualLogs, IReadOnlyList<BodyMeasurement> Measurements);
