using GymTrack.Data.Dao;
using GymTrack.Models;
using GymTrack.Services;
using GymTrack.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace GymTrack.Tests;

public sealed class HistoryAccountFilterTests
{
    [Fact]
    public async Task LoadAsync_ConsumerSeesOnlyOwnPersonalWorkoutsAndHistory()
    {
        await using var context = await TestDatabase.CreateAsync();
        var workouts = new WorkoutDao(context.Database);
        var rawSessions = new SessionDao(context.Database);
        var ownedWorkout = await CreateWorkoutsAndSessionsAsync(workouts, rawSessions);
        var appSession = new AppSession();
        appSession.SignIn(new AppUser { Id = 7, Role = UserRole.Consumer });
        var sessions = new UserSessionDao(rawSessions, workouts, appSession);
        var viewModel = new HistoryViewModel(
            sessions, workouts, NullLogger<HistoryViewModel>.Instance, appSession);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Null(viewModel.StatusMessage);
        Assert.Equal(2, viewModel.Workouts.Count);
        Assert.Null(viewModel.Workouts[0].Id);
        Assert.Equal(ownedWorkout.Id, viewModel.Workouts[1].Id);
        Assert.Equal(ownedWorkout.Name, viewModel.Workouts[1].Name);
        Assert.Equal(ownedWorkout.Name, Assert.Single(viewModel.Sessions).WorkoutName);
    }

    [Fact]
    public async Task LoadAsync_EmployeeAndSignedOutSessionCannotSeeConsumerWorkoutNamesOrRetainSelection()
    {
        await using var context = await TestDatabase.CreateAsync();
        var workouts = new WorkoutDao(context.Database);
        var rawSessions = new SessionDao(context.Database);
        var ownedWorkout = await CreateWorkoutsAndSessionsAsync(workouts, rawSessions);
        var appSession = new AppSession();
        appSession.SignIn(new AppUser { Id = 7, Role = UserRole.Consumer });
        var sessions = new UserSessionDao(rawSessions, workouts, appSession);
        var viewModel = new HistoryViewModel(
            sessions, workouts, NullLogger<HistoryViewModel>.Instance, appSession);
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.SelectedWorkout = Assert.Single(viewModel.Workouts, item => item.Id == ownedWorkout.Id);

        appSession.SignIn(new AppUser { Id = 7, Role = UserRole.Employee });
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Null(viewModel.StatusMessage);
        Assert.Null(Assert.Single(viewModel.Workouts).Id);
        Assert.Equal(viewModel.Workouts[0], viewModel.SelectedWorkout);
        Assert.Empty(viewModel.Sessions);

        appSession.SignOut();
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Null(viewModel.StatusMessage);
        Assert.Null(Assert.Single(viewModel.Workouts).Id);
        Assert.Equal(viewModel.Workouts[0], viewModel.SelectedWorkout);
        Assert.Empty(viewModel.Sessions);
    }

    private static async Task<Workout> CreateWorkoutsAndSessionsAsync(
        IWorkoutDao workouts, ISessionDao sessions)
    {
        var owned = new Workout { Name = "Meu treino pessoal", OwnerUserId = 7 };
        var other = new Workout { Name = "Treino privado de outra pessoa", OwnerUserId = 8 };
        var ownedTemplate = new Workout { Name = "Modelo associado à minha conta", OwnerUserId = 7, IsTemplate = true };
        var publicTemplate = new Workout { Name = "Modelo público", IsTemplate = true };
        var startedAt = new DateTime(2026, 10, 19, 10, 0, 0, DateTimeKind.Utc);

        foreach (var workout in new[] { owned, other, ownedTemplate, publicTemplate })
        {
            await workouts.SaveAsync(workout, []);
            await sessions.InsertAsync(new WorkoutSession
            {
                WorkoutId = workout.Id,
                StartedAt = startedAt,
                FinishedAt = startedAt.AddMinutes(30)
            });
        }

        return owned;
    }
}
