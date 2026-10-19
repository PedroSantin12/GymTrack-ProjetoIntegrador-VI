using GymTrack.Models;

namespace GymTrack.Services;

public sealed class AppSession
{
    public AppUser? CurrentUser { get; private set; }

    public bool IsEmployee => CurrentUser is { Role: UserRole.Employee, IsActive: true };

    public void SignIn(AppUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (!user.IsActive || !Enum.IsDefined(user.Role))
        {
            throw new InvalidOperationException("Esta conta não pode iniciar uma sessão.");
        }

        CurrentUser = user;
    }

    public void SignOut()
    {
        CurrentUser = null;
    }
}
