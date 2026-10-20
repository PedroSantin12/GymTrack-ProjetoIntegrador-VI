using GymTrack.Models;
using GymTrack.Services;
using GymTrack.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace GymTrack.Tests;

public sealed class RegisterViewModelTests
{
    [Fact]
    public async Task RegisterAsync_MismatchedPasswordsDoNotCreateAccount()
    {
        var accounts = new StubAccountService();
        var session = new AppSession();
        var navigator = new StubNavigator(session);
        var viewModel = CreateViewModel(accounts, session, navigator);
        viewModel.ConfirmPassword = "senha-diferente";

        await viewModel.RegisterCommand.ExecuteAsync(null);

        Assert.Equal(0, accounts.RegisterCalls);
        Assert.Equal(0, navigator.OpenWorkspaceCalls);
        Assert.Null(session.CurrentUser);
        Assert.Contains("senhas", viewModel.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RegisterAsync_SuccessStartsConsumerSessionAndOpensWorkspace()
    {
        var user = new AppUser
        {
            Id = 27,
            Name = "Nova pessoa",
            Email = "nova@gymtrack.app",
            Role = UserRole.Consumer,
            IsActive = true
        };
        var accounts = new StubAccountService { User = user };
        var session = new AppSession();
        var navigator = new StubNavigator(session);
        var viewModel = CreateViewModel(accounts, session, navigator);

        await viewModel.RegisterCommand.ExecuteAsync(null);

        Assert.Equal(1, accounts.RegisterCalls);
        Assert.Equal(("Nova pessoa", "nova@gymtrack.app", "senha-de-teste"), accounts.LastRegistration);
        Assert.Same(user, session.CurrentUser);
        Assert.False(session.IsEmployee);
        Assert.Equal(1, navigator.OpenWorkspaceCalls);
        Assert.Same(user, navigator.UserWhenWorkspaceOpened);
        Assert.Empty(viewModel.Password);
        Assert.Empty(viewModel.ConfirmPassword);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmailShowsErrorWithoutStartingSession()
    {
        var accounts = new StubAccountService
        {
            Failure = new InvalidOperationException("Este e-mail já está cadastrado.")
        };
        var session = new AppSession();
        var navigator = new StubNavigator(session);
        var viewModel = CreateViewModel(accounts, session, navigator);

        await viewModel.RegisterCommand.ExecuteAsync(null);

        Assert.Equal(1, accounts.RegisterCalls);
        Assert.Contains("já está cadastrado", viewModel.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.Null(session.CurrentUser);
        Assert.Equal(0, navigator.OpenWorkspaceCalls);
    }

    private static RegisterViewModel CreateViewModel(
        StubAccountService accounts, AppSession session, StubNavigator navigator) =>
        new(accounts, session, navigator, NullLogger<RegisterViewModel>.Instance)
        {
            Name = "Nova pessoa",
            Email = "nova@gymtrack.app",
            Password = "senha-de-teste",
            ConfirmPassword = "senha-de-teste"
        };

    private sealed class StubAccountService : IAccountService
    {
        public AppUser? User { get; init; }

        public Exception? Failure { get; init; }

        public int RegisterCalls { get; private set; }

        public (string Name, string Email, string Password)? LastRegistration { get; private set; }

        public Task<AppUser> RegisterConsumerAsync(string name, string email, string password)
        {
            RegisterCalls++;
            LastRegistration = (name, email, password);
            return Failure is null
                ? Task.FromResult(User ?? throw new InvalidOperationException("Usuário de teste ausente."))
                : Task.FromException<AppUser>(Failure);
        }

        public Task InitializeAsync() => throw new NotSupportedException();

        public Task<AppUser?> SignInAsync(string email, string password) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<AppUser>> GetUsersAsync(string? search = null) =>
            throw new NotSupportedException();

        public Task UpdateUserAsync(AppUser user) => throw new NotSupportedException();

        public Task<int> CountUsersAsync() => throw new NotSupportedException();
    }

    private sealed class StubNavigator(AppSession session) : IAppNavigator
    {
        public int OpenWorkspaceCalls { get; private set; }

        public AppUser? UserWhenWorkspaceOpened { get; private set; }

        public Task OpenWorkspaceAsync()
        {
            OpenWorkspaceCalls++;
            UserWhenWorkspaceOpened = session.CurrentUser;
            return Task.CompletedTask;
        }

        public Task ShowLoginAsync() => throw new NotSupportedException();

        public Task NavigateAsync(AppSection section) => throw new NotSupportedException();

        public Task OpenWorkoutEditorAsync(int? workoutId) => throw new NotSupportedException();

        public Task StartWorkoutAsync(int workoutId) => throw new NotSupportedException();

        public Task OpenExerciseDemoAsync(int exerciseId) => throw new NotSupportedException();

        public Task OpenHistoryAsync() => throw new NotSupportedException();
    }
}
