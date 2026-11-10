using GymTrack.Models;
using GymTrack.Services;

namespace GymTrack.Tests;

public sealed class AppSessionTests
{
    [Fact]
    public void SignIn_ConsumerDoesNotReceiveEmployeeRole()
    {
        var session = new AppSession();
        var user = new AppUser { Id = 1, Role = UserRole.Consumer };

        session.SignIn(user);

        Assert.Same(user, session.CurrentUser);
        Assert.False(session.IsEmployee);
    }

    [Fact]
    public void SignOut_ClearsEmployeeSession()
    {
        var session = new AppSession();
        session.SignIn(new AppUser { Id = 1, Role = UserRole.Employee });
        Assert.True(session.IsEmployee);

        session.SignOut();

        Assert.Null(session.CurrentUser);
        Assert.False(session.IsEmployee);
    }

    [Fact]
    public void PersonalMode_KeepsEmployeeIdentityAndResetsAtSignIn()
    {
        var session = new AppSession();
        var employee = new AppUser { Id = 7, Role = UserRole.Employee };
        session.SignIn(employee);
        session.SetEmployeePersonalMode(true);

        Assert.Same(employee, session.CurrentUser);
        Assert.True(session.IsEmployee);
        Assert.True(session.IsPersonalMode);
        Assert.False(session.IsManagementMode);

        session.SignOut();
        session.SignIn(employee);
        Assert.True(session.IsManagementMode);
        Assert.False(session.IsPersonalMode);

        session.SignIn(new AppUser { Id = 8, Role = UserRole.Consumer });
        Assert.Throws<UnauthorizedAccessException>(() => session.SetEmployeePersonalMode(false));
        Assert.True(session.IsPersonalMode);
    }

    [Fact]
    public void SignIn_InactiveAccountCannotStartSession()
    {
        var session = new AppSession();

        Assert.Throws<InvalidOperationException>(() => session.SignIn(new AppUser
        {
            Id = 1,
            Role = UserRole.Employee,
            IsActive = false
        }));

        Assert.Null(session.CurrentUser);
        Assert.False(session.IsEmployee);
    }
}
