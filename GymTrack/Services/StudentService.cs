using GymTrack.Models;

namespace GymTrack.Services;

public sealed class StudentService(IAccountService accounts, AppSession session)
{
    public async Task<IReadOnlyList<AppUser>> GetStudentsAsync() =>
        (await GetAccountsForManagementAsync()).Where(IsActiveStudent).ToArray();

    public async Task<AppUser> GetStudentAsync(int studentId) =>
        (await GetAccountsForManagementAsync())
            .FirstOrDefault(user => user.Id == studentId && IsActiveStudent(user))
        ?? throw new UnauthorizedAccessException("Este aluno não está disponível.");

    private async Task<IReadOnlyList<AppUser>> GetAccountsForManagementAsync()
    {
        if (!session.IsEmployee || session.CurrentUser is not { Id: > 0 } employee)
            throw new UnauthorizedAccessException("Sua conta não tem acesso a esta ação.");

        var users = await accounts.GetUsersAsync();
        if (!session.IsEmployee || session.CurrentUser?.Id != employee.Id ||
            !users.Any(user => user.Id == employee.Id && user.Role == UserRole.Employee && user.IsActive))
            throw new UnauthorizedAccessException("Sua conta não tem acesso a esta ação.");

        return users;
    }

    private static bool IsActiveStudent(AppUser user) =>
        user is { Role: UserRole.Consumer, IsActive: true };
}
