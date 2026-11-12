using GymTrack.Data.Dao;
using GymTrack.Models;
using GymTrack.Services;

namespace GymTrack.Tests;

public sealed class StudentServiceTests
{
    [Fact]
    public async Task StudentDirectoryRequiresAnEmployeeAndListsRegisteredConsumers()
    {
        await using var context = await TestDatabase.CreateAsync();
        await using var accounts = new AccountService(Path.Combine(context.DirectoryPath, "accounts.db3"));
        var users = await accounts.GetUsersAsync();
        var student = users.Single(user => user.Role == UserRole.Consumer);
        var employee = users.Single(user => user.Role == UserRole.Employee);
        var inactiveStudent = await accounts.RegisterConsumerAsync("Outro aluno", "outro@example.test", "GymTrack123!");
        inactiveStudent.IsActive = false;
        await accounts.UpdateUserAsync(inactiveStudent);
        var session = new AppSession();
        var service = CreateService(context, accounts, session);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetStudentsAsync());
        session.SignIn(student);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetStudentsAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetStudentAsync(student.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetDetailsAsync(student.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetDashboardCountsAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.UpdateStudentAsync(student.Id, "Outro nome", student.Email, null));

        session.SignIn(employee);
        var listed = await service.GetStudentsAsync();
        Assert.Equal(new[] { student.Id, inactiveStudent.Id }.Order(), listed.Select(user => user.Id).Order());
        var account = await service.GetStudentAsync(student.Id);
        Assert.Equal(student.Name, account.Name);
        Assert.Equal(student.Email, account.Email);
        Assert.False((await service.GetStudentAsync(inactiveStudent.Id)).IsActive);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetStudentAsync(employee.Id));
    }

    [Fact]
    public async Task StudentDirectoryRevalidatesTheSignedInEmployee()
    {
        await using var context = await TestDatabase.CreateAsync();
        await using var accounts = new AccountService(Path.Combine(context.DirectoryPath, "accounts.db3"));
        var users = await accounts.GetUsersAsync();
        var student = users.Single(user => user.Role == UserRole.Consumer);
        var employee = users.Single(user => user.Role == UserRole.Employee);
        var session = new AppSession();
        var service = CreateService(context, accounts, session);
        session.SignIn(employee);
        var employeeToDeactivate = (await accounts.GetUsersAsync()).Single(user => user.Id == employee.Id);
        employeeToDeactivate.IsActive = false;
        await accounts.UpdateUserAsync(employeeToDeactivate);

        Assert.True(session.IsEmployee);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetStudentsAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetStudentAsync(student.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetDetailsAsync(student.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.UpdateStudentAsync(student.Id, "Outro nome", student.Email, null));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            accounts.UpdateStudentDetailsAsync(employee.Id, student.Id, "Outro nome", student.Email, null));
    }

    [Fact]
    public async Task StudentDetailsReturnOnlyOwnedCompletedSessionsRealLogsAndMeasurements()
    {
        await using var context = await TestDatabase.CreateAsync();
        await using var accounts = new AccountService(Path.Combine(context.DirectoryPath, "accounts.db3"));
        var users = await accounts.GetUsersAsync();
        var student = users.Single(user => user.Role == UserRole.Consumer);
        var employee = users.Single(user => user.Role == UserRole.Employee);
        var otherStudent = await accounts.RegisterConsumerAsync("Outro aluno", "outro@example.test", "GymTrack123!");
        var session = new AppSession();
        session.SignIn(employee);
        var service = CreateService(context, accounts, session);
        var sessions = new SessionDao(context.Database);
        var workouts = new WorkoutDao(context.Database);
        var ownerWorkout = await workouts.SaveAsync(new Workout { Name = "Treino do aluno", OwnerUserId = student.Id }, []);
        var otherWorkout = await workouts.SaveAsync(new Workout { Name = "Treino de outro aluno", OwnerUserId = otherStudent.Id }, []);
        var template = await workouts.SaveAsync(new Workout { Name = "Modelo", IsTemplate = true }, []);
        var start = DateTime.UtcNow.AddDays(-1);
        var finished = start.AddMinutes(35);
        await sessions.InsertAsync(new WorkoutSession { WorkoutId = ownerWorkout, StartedAt = start, FinishedAt = finished });
        await sessions.InsertAsync(new WorkoutSession { WorkoutId = otherWorkout, StartedAt = start, FinishedAt = finished });
        await sessions.InsertAsync(new WorkoutSession { WorkoutId = template, StartedAt = start, FinishedAt = finished });
        await sessions.InsertAsync(new WorkoutSession { WorkoutId = ownerWorkout, StartedAt = start });

        var logs = new ManualWorkoutLogService(context.Database);
        await logs.EnsureDemoExamplesAsync(student);
        await logs.AddAsync(student.Id, "Corrida na rua", DateTime.Today, "Treino manual");
        await logs.AddAsync(otherStudent.Id, "Outro treino manual", DateTime.Today, null);
        var measurements = new BodyMeasurementService(context.Database);
        await measurements.SaveAsync(student.Id, DateTime.Today, 75.3, 175, 80, null, null);
        await measurements.SaveAsync(otherStudent.Id, DateTime.Today, 82, null, null, null, null);

        var details = await service.GetDetailsAsync(student.Id);
        Assert.Equal(student.Id, details.Account.Id);
        Assert.Equal("Treino do aluno", Assert.Single(details.Sessions).WorkoutName);
        Assert.Equal("Corrida na rua", Assert.Single(details.ManualLogs).WorkoutName);
        Assert.Equal(75.3, Assert.Single(details.Measurements).WeightKg);
        Assert.Equal((2, 4), await service.GetDashboardCountsAsync());
    }

    [Fact]
    public async Task EmployeeCanCorrectStudentContactDetailsWithoutChangingAccessOrPassword()
    {
        await using var context = await TestDatabase.CreateAsync();
        await using var accounts = new AccountService(Path.Combine(context.DirectoryPath, "accounts.db3"));
        var users = await accounts.GetUsersAsync();
        var student = users.Single(user => user.Role == UserRole.Consumer);
        var employee = users.Single(user => user.Role == UserRole.Employee);
        var session = new AppSession();
        session.SignIn(employee);
        var service = CreateService(context, accounts, session);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateStudentAsync(student.Id, "Nome corrigido", employee.Email, null));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.UpdateStudentAsync(employee.Id, "Nome corrigido", "gerencia@example.test", null));

        var updated = await service.UpdateStudentAsync(student.Id, "Nome corrigido", "novo@example.test", "11 98888-5555");
        Assert.Equal("Nome corrigido", updated.Name);
        Assert.Equal("11 98888-5555", updated.Phone);
        Assert.Equal(UserRole.Consumer, updated.Role);
        Assert.Equal(student.Id, (await accounts.SignInAsync(updated.Email, "GymTrack123!"))?.Id);
        Assert.Equal(employee.Id, session.CurrentUser?.Id);

        updated.IsActive = false;
        await accounts.UpdateUserAsync(updated);
        updated = await service.UpdateStudentAsync(student.Id, "Nome atualizado", updated.Email, null);
        Assert.False(updated.IsActive);
        Assert.Empty(updated.Phone);
        Assert.Null(await accounts.SignInAsync(updated.Email, "GymTrack123!"));
    }

    private static StudentService CreateService(TestDatabase context, IAccountService accounts, AppSession session) =>
        new(accounts, session, context.Database, new ManualWorkoutLogService(context.Database),
            new BodyMeasurementService(context.Database));
}
