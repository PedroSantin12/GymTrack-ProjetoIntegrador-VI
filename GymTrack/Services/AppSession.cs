using GymTrack.Models;

namespace GymTrack.Services;

public sealed class AppSession
{
    private bool _employeePersonalMode;

    public AppUser? CurrentUser { get; private set; }

    public bool IsEmployee => CurrentUser is { Role: UserRole.Employee, IsActive: true };
    public bool IsManagementMode => IsEmployee && !_employeePersonalMode;
    public bool IsPersonalMode => CurrentUser is { Role: UserRole.Consumer, IsActive: true } ||
        IsEmployee && _employeePersonalMode;

    public void SignIn(AppUser user)
    {
        ValidateUser(user);
        CurrentUser = user;
        _employeePersonalMode = false;
    }

    internal void Refresh(AppUser user)
    {
        ValidateUser(user);
        if (CurrentUser?.Id != user.Id)
            throw new UnauthorizedAccessException("A conta atual foi alterada.");

        if (CurrentUser.Role != user.Role)
            _employeePersonalMode = false;
        CurrentUser = user;
    }

    public void SetEmployeePersonalMode(bool personalMode)
    {
        if (!IsEmployee)
            throw new UnauthorizedAccessException("A alternância de área é exclusiva de funcionários.");

        _employeePersonalMode = personalMode;
    }

    public void SignOut()
    {
        CurrentUser = null;
        _employeePersonalMode = false;
    }

    private static void ValidateUser(AppUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (!user.IsActive || !Enum.IsDefined(user.Role))
        {
            throw new InvalidOperationException("Esta conta não pode iniciar uma sessão.");
        }
    }
}
