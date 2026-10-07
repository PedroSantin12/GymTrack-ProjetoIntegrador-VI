using GymTrack.Data.Dao;
using GymTrack.Models;
using GymTrack.Services;
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

    [Fact]
    public async Task Progress_ReloadsExerciseOptionsWithoutLosingSelection()
    {
        await using var context = await TestDatabase.CreateAsync();
        IExerciseDao exercises = new ExerciseDao(context.Database);
        ISessionDao sessions = new SessionDao(context.Database);
        var first = new Exercise { Name = "Remada", MuscleGroup = "Costas" };
        await exercises.InsertAsync(first);
        var viewModel = new ProgressViewModel(
            exercises,
            new AnalyticsService(sessions),
            NullLogger<ProgressViewModel>.Instance);

        await viewModel.LoadCommand.ExecuteAsync(null);
        var second = new Exercise { Name = "Supino", MuscleGroup = "Peito" };
        await exercises.InsertAsync(second);
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Equal(2, viewModel.Exercises.Count);
        Assert.Equal(first.Id, viewModel.SelectedExercise?.Id);
    }

    [Fact]
    public async Task Progress_IgnoresOlderResultAfterExerciseSelectionChanges()
    {
        await using var context = await TestDatabase.CreateAsync();
        IExerciseDao exercises = new ExerciseDao(context.Database);
        var first = new Exercise { Name = "Remada", MuscleGroup = "Costas" };
        var second = new Exercise { Name = "Supino", MuscleGroup = "Peito" };
        await exercises.InsertAsync(first);
        await exercises.InsertAsync(second);
        var firstResult = new TaskCompletionSource<IReadOnlyList<ExerciseProgressPoint>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondResult = new TaskCompletionSource<IReadOnlyList<ExerciseProgressPoint>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var viewModel = new ProgressViewModel(
            exercises,
            new DelayedAnalyticsService(first.Id, firstResult.Task, secondResult.Task),
            NullLogger<ProgressViewModel>.Instance);
        var catalogLoaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.Exercises.CollectionChanged += (_, _) =>
        {
            if (viewModel.Exercises.Count == 2)
            {
                catalogLoaded.TrySetResult(true);
            }
        };

        var loadingFirst = viewModel.LoadCommand.ExecuteAsync(null);
        await catalogLoaded.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var secondLoaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(viewModel.SessionCountText) &&
                viewModel.SessionCountText == "1")
            {
                secondLoaded.TrySetResult(true);
            }
        };
        viewModel.SelectedExercise = viewModel.Exercises.Single(item => item.Id == second.Id);
        secondResult.SetResult(
            [new ExerciseProgressPoint(DateTime.UtcNow, 30, 300)]);
        await secondLoaded.Task.WaitAsync(TimeSpan.FromSeconds(3));

        firstResult.SetResult(
        [
            new ExerciseProgressPoint(DateTime.UtcNow.AddDays(-1), 10, 100),
            new ExerciseProgressPoint(DateTime.UtcNow, 20, 200)
        ]);
        await loadingFirst;

        Assert.Equal(second.Id, viewModel.SelectedExercise?.Id);
        Assert.Equal("1", viewModel.SessionCountText);
        Assert.Contains("30", viewModel.LastLoadText);
    }

    private sealed class DelayedAnalyticsService(
        int firstExerciseId,
        Task<IReadOnlyList<ExerciseProgressPoint>> firstResult,
        Task<IReadOnlyList<ExerciseProgressPoint>> secondResult) : IAnalyticsService
    {
        public Task<IReadOnlyList<ExerciseProgressPoint>> GetExerciseProgressAsync(
            int exerciseId,
            DateTime? startedFrom = null) =>
            exerciseId == firstExerciseId ? firstResult : secondResult;
    }
}
