using GymTrack.Models;

namespace GymTrack.Services;

public sealed class AppSession
{
    public AppUser? CurrentUser { get; private set; }

    public bool IsEmployee => CurrentUser is { Role: UserRole.Employee, IsActive: true };
    public bool IsConsumer => CurrentUser is { Role: UserRole.Consumer, IsActive: true };

    public void SignIn(AppUser user)
    {
        ValidateUser(user);
        CurrentUser = user;
    }

    internal void Refresh(AppUser user)
    {
        ValidateUser(user);
        if (CurrentUser?.Id != user.Id)
            throw new UnauthorizedAccessException("A conta atual foi alterada.");

        CurrentUser = user;
    }

    public void SignOut() => CurrentUser = null;

    private static void ValidateUser(AppUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (!user.IsActive || !Enum.IsDefined(user.Role))
        {
            throw new InvalidOperationException("Esta conta não pode iniciar uma sessão.");
        }
    }
}
