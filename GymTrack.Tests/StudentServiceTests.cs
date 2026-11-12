using GymTrack.Models;
using GymTrack.Services;

namespace GymTrack.Tests;

public sealed class StudentServiceTests
{
    [Fact]
    public async Task StudentDirectoryRequiresManagementModeAndListsActiveConsumers()
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
        var service = new StudentService(accounts, session);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetStudentsAsync());
        session.SignIn(student);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetStudentsAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetStudentAsync(student.Id));

        session.SignIn(employee);
        session.SetEmployeePersonalMode(true);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetStudentsAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetStudentAsync(student.Id));

        session.SetEmployeePersonalMode(false);
        Assert.Equal(student.Id, Assert.Single(await service.GetStudentsAsync()).Id);
        var account = await service.GetStudentAsync(student.Id);
        Assert.Equal(student.Name, account.Name);
        Assert.Equal(student.Email, account.Email);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetStudentAsync(inactiveStudent.Id));
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
        var service = new StudentService(accounts, session);
        session.SignIn(employee);
        var employeeToDeactivate = (await accounts.GetUsersAsync()).Single(user => user.Id == employee.Id);
        employeeToDeactivate.IsActive = false;
        await accounts.UpdateUserAsync(employeeToDeactivate);

        Assert.True(session.IsManagementMode);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetStudentsAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetStudentAsync(student.Id));
    }
}
