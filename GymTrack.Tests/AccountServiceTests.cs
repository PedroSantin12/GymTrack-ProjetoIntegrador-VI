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

    [Fact]
    public async Task InitializeAsync_AddsProfileFieldsToExistingDatabase()
    {
        await using var context = new TestAccounts();
        var original = await context.Service.RegisterConsumerAsync("Ana Costa", "ana@example.com", "MinhaSenha123!");
        await context.ReopenAsync();

        using (var connection = new SQLite.SQLiteConnection(context.DatabasePath))
        {
            connection.Execute("ALTER TABLE Account DROP COLUMN Phone");
            connection.Execute("ALTER TABLE Account DROP COLUMN JobTitle");
            connection.Execute("ALTER TABLE Account DROP COLUMN GymName");
            connection.Execute("ALTER TABLE Account DROP COLUMN NotificationsEnabled");
        }

        var migrated = await context.Service.SignInAsync(original.Email, "MinhaSenha123!");
        Assert.NotNull(migrated);
        Assert.Equal(original.Id, migrated.Id);
        Assert.Equal(original.Name, migrated.Name);
        Assert.Empty(migrated.Phone);
        Assert.Empty(migrated.JobTitle);
        Assert.Empty(migrated.GymName);
        Assert.True(migrated.NotificationsEnabled);
        Assert.Equal(3, await context.Service.CountUsersAsync());
    }

    [Theory]
    [InlineData("consumidor@gymtrack.app", UserRole.Consumer)]
    [InlineData("funcionario@gymtrack.app", UserRole.Employee)]
    public async Task UpdatePersonalDetailsAsync_ChangesContactDetailsWithoutChangingRoleOrPassword(
        string originalEmail, UserRole role)
    {
        await using var context = new TestAccounts();
        var user = await context.Service.SignInAsync(originalEmail, "GymTrack123!");
        Assert.NotNull(user);

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Service.UpdatePersonalDetailsAsync(
            user.Id, "Novo Nome", role == UserRole.Consumer ? "funcionario@gymtrack.app" : "consumidor@gymtrack.app", ""));
        var updated = await context.Service.UpdatePersonalDetailsAsync(
            user.Id, " Novo Nome ", " NOVO@EXAMPLE.COM ", " 11 98888-5555 ");
        Assert.Equal("Novo Nome", updated.Name);
        Assert.Equal("novo@example.com", updated.Email);
        Assert.Equal("11 98888-5555", updated.Phone);
        Assert.Equal(role, updated.Role);
        Assert.Equal(user.CreatedAt, updated.CreatedAt);
        Assert.Null(await context.Service.SignInAsync(originalEmail, "GymTrack123!"));
        Assert.Equal(user.Id, (await context.Service.SignInAsync(updated.Email, "GymTrack123!"))?.Id);
    }

    [Fact]
    public async Task ProfessionalDetailsAndNotifications_AreSavedForTheCurrentAccount()
    {
        await using var context = new TestAccounts();
        var employee = await context.Service.SignInAsync("funcionario@gymtrack.app", "GymTrack123!");
        var consumer = await context.Service.SignInAsync("consumidor@gymtrack.app", "GymTrack123!");
        Assert.NotNull(employee);
        Assert.NotNull(consumer);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.Service.UpdateProfessionalDetailsAsync(consumer.Id, "Instrutor", "Academia"));

        var profile = await context.Service.UpdateProfessionalDetailsAsync(employee.Id, " Instrutor ", " Academia Central ");
        Assert.Equal("Instrutor", profile.JobTitle);
        Assert.Equal("Academia Central", profile.GymName);
        Assert.False((await context.Service.SetNotificationsEnabledAsync(employee.Id, false)).NotificationsEnabled);

        await context.ReopenAsync();
        var saved = await context.Service.SignInAsync(employee.Email, "GymTrack123!");
        Assert.NotNull(saved);
        Assert.Equal("Instrutor", saved.JobTitle);
        Assert.Equal("Academia Central", saved.GymName);
        Assert.False(saved.NotificationsEnabled);
        Assert.True((await context.Service.SetNotificationsEnabledAsync(employee.Id, true)).NotificationsEnabled);
        var other = await context.Service.SignInAsync(consumer.Email, "GymTrack123!");
        Assert.NotNull(other);
        Assert.True(other.NotificationsEnabled);
        Assert.Empty(other.GymName);
    }

    [Fact]
    public async Task ChangePasswordAsync_RequiresCurrentPasswordAndPersistsReplacement()
    {
        await using var context = new TestAccounts();
        var user = await context.Service.SignInAsync("consumidor@gymtrack.app", "GymTrack123!");
        Assert.NotNull(user);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            context.Service.ChangePasswordAsync(user.Id, "GymTrack123!", "curta"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.Service.ChangePasswordAsync(user.Id, "incorreta", "NovaSenha123!"));
        Assert.NotNull(await context.Service.SignInAsync(user.Email, "GymTrack123!"));

        await context.Service.ChangePasswordAsync(user.Id, "GymTrack123!", "NovaSenha123!");
        await context.ReopenAsync();
        Assert.Null(await context.Service.SignInAsync(user.Email, "GymTrack123!"));
        Assert.Equal(user.Id, (await context.Service.SignInAsync(user.Email, "NovaSenha123!"))?.Id);
        Assert.NotNull(await context.Service.SignInAsync("funcionario@gymtrack.app", "GymTrack123!"));
    }

    private sealed class TestAccounts : IAsyncDisposable
    {
        private readonly string _directory = Path.Combine(
            Path.GetTempPath(), "GymTrack.AccountTests", Guid.NewGuid().ToString("N"));

        public TestAccounts()
        {
            Service = new AccountService(DatabasePath);
        }

        public string DatabasePath => Path.Combine(_directory, "accounts.db3");

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
