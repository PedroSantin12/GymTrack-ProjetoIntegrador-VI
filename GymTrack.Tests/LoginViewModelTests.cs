using GymTrack.Models;
using GymTrack.Services;
using GymTrack.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace GymTrack.Tests;

public sealed class LoginViewModelTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("   ", "senha")]
    [InlineData("conta@gymtrack.app", " ")]
    public async Task SignInAsync_MissingCredentialsShowsErrorWithoutAuthenticating(string email, string password)
    {
        var accounts = new StubAccountService();
        var session = new AppSession();
        var navigator = new StubNavigator(session);
        var viewModel = CreateViewModel(accounts, session, navigator);
        viewModel.Email = email;
        viewModel.Password = password;

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
        Assert.Equal(0, accounts.SignInCalls);
        Assert.Equal(0, navigator.OpenWorkspaceCalls);
        Assert.Null(session.CurrentUser);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task SignInAsync_InvalidCredentialsDoesNotOpenWorkspace()
    {
        var accounts = new StubAccountService();
        var session = new AppSession();
        var navigator = new StubNavigator(session);
        var viewModel = CreateViewModel(accounts, session, navigator);

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
        Assert.Equal(1, accounts.SignInCalls);
        Assert.Equal(0, navigator.OpenWorkspaceCalls);
        Assert.Null(session.CurrentUser);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task SignInAsync_InactiveAccountDoesNotStartSessionOrOpenWorkspace()
    {
        var accounts = new StubAccountService
        {
            User = new AppUser { Id = 1, Role = UserRole.Employee, IsActive = false }
        };
        var session = new AppSession();
        var navigator = new StubNavigator(session);
        var viewModel = CreateViewModel(accounts, session, navigator);

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.Null(session.CurrentUser);
        Assert.Equal(0, navigator.OpenWorkspaceCalls);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
        Assert.False(viewModel.IsBusy);
    }

    [Theory]
    [InlineData(UserRole.Consumer)]
    [InlineData(UserRole.Employee)]
    public async Task SignInAsync_ValidAccountOpensWorkspaceWithAuthenticatedRoleAndClearsPassword(UserRole role)
    {
        var user = new AppUser { Id = 7, Name = "Pessoa de teste", Role = role };
        var accounts = new StubAccountService { User = user };
        var session = new AppSession();
        var navigator = new StubNavigator(session);
        var viewModel = CreateViewModel(accounts, session, navigator);

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.Same(user, session.CurrentUser);
        Assert.Same(user, navigator.UserWhenWorkspaceOpened);
        Assert.Equal(role == UserRole.Employee, session.IsEmployee);
        Assert.Equal(1, navigator.OpenWorkspaceCalls);
        Assert.Equal("conta@gymtrack.app", accounts.LastEmail);
        Assert.Equal("senha-de-teste", accounts.LastPassword);
        Assert.Empty(viewModel.Password);
        Assert.Null(viewModel.ErrorMessage);
        Assert.False(viewModel.IsBusy);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SignInAsync_AuthenticationOrNavigationExceptionClearsSession(bool failNavigation)
    {
        var user = new AppUser { Id = 7, Role = UserRole.Consumer };
        var accounts = new StubAccountService
        {
            User = user,
            Failure = failNavigation ? null : new InvalidOperationException("Falha de armazenamento")
        };
        var session = new AppSession();
        session.SignIn(new AppUser { Id = 99, Role = UserRole.Employee });
        var navigator = new StubNavigator(session)
        {
            Failure = failNavigation ? new InvalidOperationException("Falha de navegação") : null
        };
        var viewModel = CreateViewModel(accounts, session, navigator);

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.Null(session.CurrentUser);
        Assert.False(session.IsEmployee);
        Assert.Equal(failNavigation ? 1 : 0, navigator.OpenWorkspaceCalls);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
        Assert.False(viewModel.IsBusy);
    }

    private static LoginViewModel CreateViewModel(
        StubAccountService accounts, AppSession session, StubNavigator navigator) =>
        new(accounts, session, navigator, NullLogger<LoginViewModel>.Instance)
        {
            Email = "conta@gymtrack.app",
            Password = "senha-de-teste"
        };

    private sealed class StubAccountService : IAccountService
    {
        public AppUser? User { get; init; }

        public Exception? Failure { get; init; }

        public int SignInCalls { get; private set; }

        public string? LastEmail { get; private set; }

        public string? LastPassword { get; private set; }

        public Task<AppUser?> SignInAsync(string email, string password)
        {
            SignInCalls++;
            LastEmail = email;
            LastPassword = password;
            return Failure is null ? Task.FromResult(User) : Task.FromException<AppUser?>(Failure);
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public Task<AppUser> RegisterConsumerAsync(string name, string email, string password) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<AppUser>> GetUsersAsync(string? search = null) =>
            throw new NotSupportedException();

        public Task UpdateUserAsync(AppUser user) => throw new NotSupportedException();

        public Task<int> CountUsersAsync() => throw new NotSupportedException();
    }

    private sealed class StubNavigator(AppSession session) : IAppNavigator
    {
        public Exception? Failure { get; init; }

        public int OpenWorkspaceCalls { get; private set; }

        public AppUser? UserWhenWorkspaceOpened { get; private set; }

        public Task OpenWorkspaceAsync()
        {
            OpenWorkspaceCalls++;
            UserWhenWorkspaceOpened = session.CurrentUser;
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }

        public Task ShowLoginAsync() => throw new NotSupportedException();

        public Task NavigateAsync(AppSection section) => throw new NotSupportedException();

        public Task OpenWorkoutEditorAsync(int? workoutId) => throw new NotSupportedException();

        public Task StartWorkoutAsync(int workoutId) => throw new NotSupportedException();

        public Task OpenHistoryAsync() => throw new NotSupportedException();
    }
}
