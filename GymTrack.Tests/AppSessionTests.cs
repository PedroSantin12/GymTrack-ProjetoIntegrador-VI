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
        Assert.True(session.IsConsumer);
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
        Assert.False(session.IsConsumer);
    }

    [Fact]
    public void SignIn_ReplacesThePreviousAccountAndRole()
    {
        var session = new AppSession();
        var employee = new AppUser { Id = 7, Role = UserRole.Employee };
        var consumer = new AppUser { Id = 8, Role = UserRole.Consumer };
        session.SignIn(employee);

        Assert.Same(employee, session.CurrentUser);
        Assert.True(session.IsEmployee);
        Assert.False(session.IsConsumer);

        session.SignIn(consumer);
        Assert.Same(consumer, session.CurrentUser);
        Assert.True(session.IsConsumer);
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
