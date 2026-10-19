using GymTrack.Models;
using GymTrack.Services;

namespace GymTrack.Tests;

public sealed class AccountServiceTests
{
    [Theory]
    [InlineData("consumidor@gymtrack.app", UserRole.Consumer)]
    [InlineData("funcionario@gymtrack.app", UserRole.Employee)]
    public async Task SignInAsync_DemoCredentials_ReturnCorrectRole(string email, UserRole role)
    {
        await using var context = new TestAccounts();

        var user = await context.Service.SignInAsync(email, "GymTrack123!");

        Assert.NotNull(user);
        Assert.Equal(role, user.Role);
        Assert.True(user.IsActive);
        Assert.True(user.Id > 0);
        Assert.NotEqual(default, user.CreatedAt);
    }

    [Fact]
    public async Task SignInAsync_NormalizesEmailAndRejectsWrongCredentials()
    {
        await using var context = new TestAccounts();

        Assert.NotNull(await context.Service.SignInAsync(" CONSUMIDOR@GYMTRACK.APP ", "GymTrack123!"));
        Assert.Null(await context.Service.SignInAsync("consumidor@gymtrack.app", "incorreta"));
        Assert.Null(await context.Service.SignInAsync("desconhecido@gymtrack.app", "GymTrack123!"));
        Assert.Null(await context.Service.SignInAsync("", "GymTrack123!"));
        Assert.Null(await context.Service.SignInAsync("consumidor@gymtrack.app", ""));
    }

    [Fact]
    public async Task RegisterConsumerAsync_CreatesActiveConsumerAndAllowsSignIn()
    {
        await using var context = new TestAccounts();

        var registered = await context.Service.RegisterConsumerAsync(
            "  Ana Costa  ", "  ANA.COSTA@EXAMPLE.COM  ", "SenhaSegura123!");

        Assert.True(registered.Id > 0);
        Assert.Equal("Ana Costa", registered.Name);
        Assert.Equal("ana.costa@example.com", registered.Email);
        Assert.Equal(UserRole.Consumer, registered.Role);
        Assert.True(registered.IsActive);
        Assert.NotEqual(default, registered.CreatedAt);
        Assert.Equal(3, await context.Service.CountUsersAsync());
        Assert.Null(await context.Service.SignInAsync(registered.Email, "senha-errada"));

        var signedIn = await context.Service.SignInAsync("ANA.COSTA@EXAMPLE.COM", "SenhaSegura123!");
        Assert.NotNull(signedIn);
        Assert.Equal(registered.Id, signedIn.Id);
        Assert.Equal(registered.Name, signedIn.Name);

        await context.ReopenAsync();
        Assert.NotNull(await context.Service.SignInAsync(registered.Email, "SenhaSegura123!"));
        Assert.NotNull(await context.Service.SignInAsync("funcionario@gymtrack.app", "GymTrack123!"));
    }

    [Theory]
    [InlineData("", "ana@example.com", "SenhaSegura123!")]
    [InlineData(" A ", "ana@example.com", "SenhaSegura123!")]
    [InlineData("Ana", "", "SenhaSegura123!")]
    [InlineData("Ana", "email-invalido", "SenhaSegura123!")]
    [InlineData("Ana", "ana@example.com", "curta")]
    [InlineData("Ana", "ana@example.com", "        ")]
    public async Task RegisterConsumerAsync_RejectsInvalidFields(
        string name, string email, string password)
    {
        await using var context = new TestAccounts();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            context.Service.RegisterConsumerAsync(name, email, password));

        Assert.Equal(2, await context.Service.CountUsersAsync());
    }

    [Fact]
    public async Task RegisterConsumerAsync_RejectsCaseInsensitiveDuplicatesIncludingDemoAccounts()
    {
        await using var context = new TestAccounts();
        var registered = await context.Service.RegisterConsumerAsync(
            "Ana Costa", "ana@example.com", "SenhaSegura123!");

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Service.RegisterConsumerAsync(
            "Outra Ana", " ANA@EXAMPLE.COM ", "OutraSenha123!"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Service.RegisterConsumerAsync(
            "Outra Maria", " CONSUMIDOR@GYMTRACK.APP ", "OutraSenha123!"));

        Assert.Equal(3, await context.Service.CountUsersAsync());
        Assert.Equal(registered.Id,
            (await context.Service.SignInAsync("ana@example.com", "SenhaSegura123!"))?.Id);
        Assert.NotNull(await context.Service.SignInAsync("consumidor@gymtrack.app", "GymTrack123!"));
    }

    [Fact]
    public async Task UpdateUserAsync_DeactivatesAccountAndBlocksSignIn()
    {
        await using var context = new TestAccounts();
        var user = await context.Service.SignInAsync("consumidor@gymtrack.app", "GymTrack123!");
        Assert.NotNull(user);

        user.IsActive = false;
        await context.Service.UpdateUserAsync(user);

        Assert.Null(await context.Service.SignInAsync(user.Email, "GymTrack123!"));
        Assert.False(Assert.Single(await context.Service.GetUsersAsync(user.Email)).IsActive);
        Assert.Equal(2, await context.Service.CountUsersAsync());
    }

    [Fact]
    public async Task GetUsersAsync_FiltersByNameAndEmailWithoutTreatingWildcardsAsInput()
    {
        await using var context = new TestAccounts();

        Assert.Equal("consumidor@gymtrack.app", Assert.Single(await context.Service.GetUsersAsync("MARIA")).Email);
        Assert.Equal(UserRole.Employee, Assert.Single(await context.Service.GetUsersAsync("funcionario@")).Role);
        Assert.Empty(await context.Service.GetUsersAsync("inexistente"));
        Assert.Empty(await context.Service.GetUsersAsync("%"));
        Assert.Empty(await context.Service.GetUsersAsync("_"));
        Assert.Equal(2, (await context.Service.GetUsersAsync()).Count);
    }

    [Fact]
    public async Task InitializeAsync_ReopenedDatabasePreservesChangesWithoutRecreatingDemoAccount()
    {
        await using var context = new TestAccounts();
        var user = await context.Service.SignInAsync("consumidor@gymtrack.app", "GymTrack123!");
        Assert.NotNull(user);
        var createdAt = user.CreatedAt;
        user.Name = "Maria atualizada";
        user.Email = "maria.atualizada@gymtrack.app";
        user.Role = UserRole.Employee;
        await context.Service.UpdateUserAsync(user);

        await context.ReopenAsync();
        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => context.Service.InitializeAsync()));

        Assert.Equal(2, await context.Service.CountUsersAsync());
        Assert.Null(await context.Service.SignInAsync("consumidor@gymtrack.app", "GymTrack123!"));
        var updated = await context.Service.SignInAsync(user.Email, "GymTrack123!");
        Assert.NotNull(updated);
        Assert.Equal("Maria atualizada", updated.Name);
        Assert.Equal(UserRole.Employee, updated.Role);
        Assert.Equal(createdAt, updated.CreatedAt);
    }

    [Fact]
    public async Task UpdateUserAsync_RejectsDuplicateEmailAndKeepsBothAccounts()
    {
        await using var context = new TestAccounts();
        var user = await context.Service.SignInAsync("consumidor@gymtrack.app", "GymTrack123!");
        Assert.NotNull(user);
        user.Email = " FUNCIONARIO@GYMTRACK.APP ";

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Service.UpdateUserAsync(user));

        Assert.Equal(2, await context.Service.CountUsersAsync());
        Assert.NotNull(await context.Service.SignInAsync("consumidor@gymtrack.app", "GymTrack123!"));
        Assert.NotNull(await context.Service.SignInAsync("funcionario@gymtrack.app", "GymTrack123!"));
    }

    [Fact]
    public async Task UpdateUserAsync_RejectsInvalidFieldsAndUnknownAccount()
    {
        await using var context = new TestAccounts();
        var user = await context.Service.SignInAsync("consumidor@gymtrack.app", "GymTrack123!");
        Assert.NotNull(user);
        user.Email = "email-invalido";
        await Assert.ThrowsAsync<ArgumentException>(() => context.Service.UpdateUserAsync(user));

        user.Email = "valido@gymtrack.app";
        user.Role = (UserRole)99;
        await Assert.ThrowsAsync<ArgumentException>(() => context.Service.UpdateUserAsync(user));

        user.Role = UserRole.Consumer;
        user.Id = int.MaxValue;
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Service.UpdateUserAsync(user));
    }

    private sealed class TestAccounts : IAsyncDisposable
    {
        private readonly string _directory = Path.Combine(
            Path.GetTempPath(), "GymTrack.AccountTests", Guid.NewGuid().ToString("N"));

        public TestAccounts()
        {
            Service = new AccountService(DatabasePath);
        }

        private string DatabasePath => Path.Combine(_directory, "accounts.db3");

        public AccountService Service { get; private set; }

        public async Task ReopenAsync()
        {
            await Service.DisposeAsync();
            Service = new AccountService(DatabasePath);
        }

        public async ValueTask DisposeAsync()
        {
            await Service.DisposeAsync();
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
    }
}
