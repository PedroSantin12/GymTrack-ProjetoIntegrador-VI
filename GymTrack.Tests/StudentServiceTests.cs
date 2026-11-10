using GymTrack.Data.Dao;
using GymTrack.Models;
using GymTrack.Services;

namespace GymTrack.Tests;

public sealed class StudentServiceTests
{
    [Fact]
    public async Task StudentControlsLinkAndHistoryWithoutExposingOtherUsers()
    {
        await using var context = await TestDatabase.CreateAsync();
        await using var accounts = new AccountService(Path.Combine(context.DirectoryPath, "accounts.db3"));
        var users = await accounts.GetUsersAsync();
        var student = users.Single(user => user.Role == UserRole.Consumer);
        var employee = users.Single(user => user.Role == UserRole.Employee);
        var otherStudent = await accounts.RegisterConsumerAsync("Outro aluno", "outro@example.test", "GymTrack123!");
        var session = new AppSession();
        var service = new StudentService(context.Database, accounts, session);
        var logs = new ManualWorkoutLogService(context.Database);
        var workouts = new WorkoutDao(context.Database);

        session.SignIn(employee);
        Assert.Empty(await service.GetLinkedStudentsAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetDetailsAsync(student.Id));

        session.SignIn(student);
        await service.LinkAsync(employee.Email.ToUpperInvariant());
        await logs.EnsureDemoExamplesAsync(student);
        await logs.AddAsync(student.Id, "Treino do aluno", DateTime.Today, "Observação privada");
        await logs.AddAsync(otherStudent.Id, "Treino de outra conta", DateTime.Today, null);
        var workoutId = await workouts.SaveAsync(new Workout { Name = "Corrida", OwnerUserId = student.Id }, []);
        var workoutSessions = new SessionDao(context.Database);
        var now = DateTime.UtcNow;
        await workoutSessions.InsertAsync(new WorkoutSession
        {
            WorkoutId = workoutId, StartedAt = now.AddMinutes(-30), FinishedAt = now
        });
        await workoutSessions.InsertAsync(new WorkoutSession { WorkoutId = workoutId, StartedAt = now });

        session.SignIn(employee);
        Assert.Equal(student.Id, Assert.Single(await service.GetLinkedStudentsAsync()).Id);
        Assert.Empty((await service.GetDetailsAsync(student.Id)).History);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetDetailsAsync(otherStudent.Id));

        session.SignIn(student);
        await service.SetHistorySharingAsync(employee.Id, true);
        Assert.True(Assert.Single(await service.GetConnectionsAsync()).SharesHistory);
        session.SignIn(employee);
        var history = (await service.GetDetailsAsync(student.Id)).History;
        Assert.Equal(2, history.Count);
        Assert.Contains(history, item => item.WorkoutName == "Treino do aluno" && item.IsManual);
        Assert.Contains(history, item => item.WorkoutName == "Corrida" && !item.IsManual);

        session.SignIn(student);
        await service.SetHistorySharingAsync(employee.Id, false);
        session.SignIn(employee);
        Assert.Empty((await service.GetDetailsAsync(student.Id)).History);

        session.SignIn(student);
        await service.UnlinkAsync(employee.Id);
        session.SignIn(employee);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetDetailsAsync(student.Id));
    }

    [Fact]
    public async Task AssignmentCreatesOneIndependentCopyAndRequiresAnActiveLink()
    {
        await using var context = await TestDatabase.CreateAsync();
        await using var accounts = new AccountService(Path.Combine(context.DirectoryPath, "accounts.db3"));
        var users = await accounts.GetUsersAsync();
        var student = users.Single(user => user.Role == UserRole.Consumer);
        var employee = users.Single(user => user.Role == UserRole.Employee);
        var session = new AppSession();
        var service = new StudentService(context.Database, accounts, session);
        var dao = new WorkoutDao(context.Database);
        var catalog = new WorkoutCatalogService(context.Database, dao);
        var exercise = new Exercise { Name = "Supino", MuscleGroup = "Peito" };
        await new ExerciseDao(context.Database).InsertAsync(exercise);
        var template = new Workout { Name = "Peito A", IsTemplate = true };
        await dao.SaveAsync(template, [new WorkoutExercise
        {
            ExerciseId = exercise.Id, PlannedSets = 3, PlannedReps = 12, PlannedLoad = 20
        }]);

        session.SignIn(employee);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.AssignWorkoutAsync(student.Id, template.Id));
        Assert.Empty(await catalog.GetPersonalAsync(student.Id));

        session.SignIn(student);
        await service.LinkAsync(employee.Email);
        session.SignIn(employee);
        var empty = await catalog.CreateTemplateAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignWorkoutAsync(student.Id, empty));
        Assert.Empty(await catalog.GetPersonalAsync(student.Id));

        var copies = await Task.WhenAll(service.AssignWorkoutAsync(student.Id, template.Id),
            service.AssignWorkoutAsync(student.Id, template.Id));
        var copyId = copies[0];
        Assert.Equal(copyId, copies[1]);
        Assert.NotEqual(template.Id, copyId);
        Assert.True(Assert.Single(await catalog.GetPersonalAsync(student.Id)).IsAssigned);
        Assert.Equal(copyId, Assert.Single((await service.GetDetailsAsync(student.Id)).Workouts).Id);
        Assert.Equal(12, Assert.Single(await dao.GetExercisesAsync(copyId)).PlannedReps);

        await catalog.DeleteTemplateAsync(template.Id);
        session.SignIn(student);
        await service.UnlinkAsync(employee.Id);
        Assert.Equal(copyId, Assert.Single(await catalog.GetPersonalAsync(student.Id)).Id);

        session.SignIn(employee);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetDetailsAsync(student.Id));
    }
}
