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
