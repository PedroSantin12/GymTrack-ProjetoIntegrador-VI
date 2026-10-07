using GymTrack.Data.Dao;
using GymTrack.Models;
using GymTrack.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace GymTrack.Tests;

public sealed class DashboardViewModelTests
{
    [Fact]
    public async Task LoadAsync_NotifiesStartCommandAfterBusyStateEnds()
    {
        await using var context = await TestDatabase.CreateAsync();
        IExerciseDao exerciseDao = new ExerciseDao(context.Database);
        IWorkoutDao workoutDao = new WorkoutDao(context.Database);
        ISessionDao sessionDao = new SessionDao(context.Database);
        var exercise = new Exercise { Name = "Remada", MuscleGroup = "Costas" };
        await exerciseDao.InsertAsync(exercise);
        await workoutDao.SaveAsync(new Workout { Name = "Treino A" },
            [new WorkoutExercise { ExerciseId = exercise.Id, PlannedSets = 2, PlannedReps = 10 }]);
        var viewModel = new DashboardViewModel(
            workoutDao, sessionDao, NullLogger<DashboardViewModel>.Instance);
        var states = new List<bool>();
        viewModel.StartWorkoutCommand.CanExecuteChanged += (_, _) =>
            states.Add(viewModel.StartWorkoutCommand.CanExecute(null));

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.True(viewModel.StartWorkoutCommand.CanExecute(null));
        Assert.True(states[^1]);
    }
}
