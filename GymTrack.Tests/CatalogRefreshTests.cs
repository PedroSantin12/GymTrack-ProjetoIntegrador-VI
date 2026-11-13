using GymTrack.Data.Dao;
using GymTrack.Models;
using GymTrack.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace GymTrack.Tests;

public sealed class CatalogRefreshTests
{
    [Fact]
    public async Task History_ReloadsWorkoutOptionsWithoutLosingSelection()
    {
        await using var context = await TestDatabase.CreateAsync();
        IWorkoutDao workouts = new WorkoutDao(context.Database);
        ISessionDao sessions = new SessionDao(context.Database);
        var first = new Workout { Name = "Treino A" };
        await workouts.SaveAsync(first, []);
        var viewModel = new HistoryViewModel(
            sessions, workouts, NullLogger<HistoryViewModel>.Instance);

        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.SelectedWorkout = viewModel.Workouts.Single(item => item.Id == first.Id);
        var second = new Workout { Name = "Treino B" };
        await workouts.SaveAsync(second, []);
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Equal(3, viewModel.Workouts.Count);
        Assert.Equal(first.Id, viewModel.SelectedWorkout?.Id);
    }
}
