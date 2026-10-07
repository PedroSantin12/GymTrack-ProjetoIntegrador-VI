using GymTrack.Data.Dao;
using GymTrack.Models;
using GymTrack.Services;
using GymTrack.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace GymTrack.Tests;

public sealed class WorkoutsViewModelTests
{
    [Fact]
    public async Task SearchText_FiltersWorkoutsAsUserTypesIgnoringAccents()
    {
        var dao = new FakeWorkoutDao();
        dao.Summaries.Add(new WorkoutSummary { Id = 1, Name = "Treino de Braços" });
        dao.Summaries.Add(new WorkoutSummary { Id = 2, Name = "Treino de Pernas" });
        var viewModel = CreateViewModel(dao);
        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.SearchText = "bracos";

        Assert.Equal(1, Assert.Single(viewModel.Workouts).Id);
        Assert.Equal(1, dao.SummaryCalls);
        viewModel.SearchText = string.Empty;
        Assert.Equal(2, viewModel.Workouts.Count);
    }

    [Fact]
    public async Task LoadAsync_PopulatesWorkoutSummaries()
    {
        var dao = new FakeWorkoutDao();
        dao.Summaries.Add(new WorkoutSummary
        {
            Id = 7,
            Name = "Treino A",
            Description = "Superior",
            ExerciseCount = 2,
            LastSessionAt = new DateTime(2026, 8, 31, 18, 30, 0, DateTimeKind.Utc)
        });
        var viewModel = CreateViewModel(dao);

        await viewModel.LoadCommand.ExecuteAsync(null);

        var item = Assert.Single(viewModel.Workouts);
        Assert.Equal(7, item.Id);
        Assert.Equal("Treino A", item.Name);
        Assert.Equal("Superior", item.Description);
        Assert.Equal("2 exercícios", item.ExerciseCountText);
        Assert.Equal("Última execução: 31/08/2026", item.LastSessionText);
        Assert.False(viewModel.IsBusy);
        Assert.Null(viewModel.StatusMessage);
    }

    [Fact]
    public void AddAndManageCommands_RequestTheExpectedRoutes()
    {
        var viewModel = CreateViewModel(new FakeWorkoutDao());
        int? requestedWorkoutId = -1;
        var editorRequests = 0;
        var manageRequests = 0;
        viewModel.EditorRequested += (_, eventArgs) =>
        {
            editorRequests++;
            requestedWorkoutId = eventArgs.WorkoutId;
        };
        viewModel.ManageExercisesRequested += (_, _) => manageRequests++;

        viewModel.AddWorkoutCommand.Execute(null);
        viewModel.ManageExercisesCommand.Execute(null);

        Assert.Equal(1, editorRequests);
        Assert.Null(requestedWorkoutId);
        Assert.Equal(1, manageRequests);
    }

    [Fact]
    public async Task EditCommand_RequestsExistingWorkout()
    {
        var dao = CreateDaoWithWorkout(exerciseCount: 1);
        var viewModel = CreateViewModel(dao);
        int? requestedWorkoutId = null;
        viewModel.EditorRequested += (_, eventArgs) =>
            requestedWorkoutId = eventArgs.WorkoutId;
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Single(viewModel.Workouts).EditCommand.Execute(null);

        Assert.Equal(1, requestedWorkoutId);
    }

    [Fact]
    public async Task StartDraftWorkout_ShowsGuidanceAndDoesNotStart()
    {
        var dao = CreateDaoWithWorkout(exerciseCount: 0);
        var dialogs = new FakeDialogService { NextAction = "Iniciar" };
        var viewModel = CreateViewModel(dao, dialogs);
        var startRequests = 0;
        viewModel.StartRequested += (_, _) => startRequests++;
        await viewModel.LoadCommand.ExecuteAsync(null);

        await Assert.Single(viewModel.Workouts).ActionsCommand.ExecuteAsync(null);

        var alert = Assert.Single(dialogs.Alerts);
        Assert.Equal("Treino incompleto", alert.Title);
        Assert.Contains("Adicione ao menos um exercício", alert.Message);
        Assert.Equal(0, startRequests);
        Assert.Equal(0, dao.DeleteCalls);
    }

    [Fact]
    public async Task StartEmptyWorkout_WithActiveSession_RequestsResume()
    {
        var dao = CreateDaoWithWorkout(exerciseCount: 0);
        dao.HasActiveSessionResult = true;
        var viewModel = CreateViewModel(dao);
        int? requestedId = null;
        viewModel.StartRequested += (_, args) => requestedId = args.WorkoutId;
        await viewModel.LoadCommand.ExecuteAsync(null);

        await Assert.Single(viewModel.Workouts).StartCommand.ExecuteAsync(null);

        Assert.Equal(1, requestedId);
    }

    [Fact]
    public async Task StartComposedWorkout_RaisesStartRequest()
    {
        var dao = CreateDaoWithWorkout(exerciseCount: 3);
        var dialogs = new FakeDialogService();
        var viewModel = CreateViewModel(dao, dialogs);
        int? requestedWorkoutId = null;
        viewModel.StartRequested += (_, eventArgs) =>
            requestedWorkoutId = eventArgs.WorkoutId;
        await viewModel.LoadCommand.ExecuteAsync(null);

        await Assert.Single(viewModel.Workouts).StartCommand.ExecuteAsync(null);

        Assert.Equal(1, requestedWorkoutId);
        Assert.Empty(dialogs.Alerts);
    }

    [Fact]
    public async Task DeleteWorkout_WithSessions_IsPreservedWithoutConfirmation()
    {
        var dao = CreateDaoWithWorkout(exerciseCount: 2);
        dao.HasSessionsResult = true;
        var dialogs = new FakeDialogService { NextAction = "Excluir" };
        var viewModel = CreateViewModel(dao, dialogs);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await Assert.Single(viewModel.Workouts).ActionsCommand.ExecuteAsync(null);

        var alert = Assert.Single(dialogs.Alerts);
        Assert.Equal("Treino preservado", alert.Title);
        Assert.Contains("Treino A", alert.Message);
        Assert.Empty(dialogs.Confirmations);
        Assert.Equal(0, dao.DeleteCalls);
        Assert.Single(viewModel.Workouts);
    }

    [Fact]
    public async Task DeleteDraftWorkout_WhenConfirmed_DeletesAndReloadsList()
    {
        var dao = CreateDaoWithWorkout(exerciseCount: 0);
        var dialogs = new FakeDialogService
        {
            NextAction = "Excluir",
            NextConfirmation = true
        };
        var notifications = new FakeNotificationService();
        var viewModel = CreateViewModel(dao, dialogs, notifications);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await Assert.Single(viewModel.Workouts).ActionsCommand.ExecuteAsync(null);

        var confirmation = Assert.Single(dialogs.Confirmations);
        Assert.Contains("Treino A", confirmation.Message);
        Assert.Equal("Excluir", confirmation.Accept);
        Assert.Equal(1, dao.DeleteCalls);
        Assert.Empty(viewModel.Workouts);
        Assert.Equal(2, dao.SummaryCalls);
        Assert.Contains("Treino excluído.", notifications.Messages);
    }

    [Fact]
    public async Task DeleteDraftWorkout_WhenCancelled_DoesNotDelete()
    {
        var dao = CreateDaoWithWorkout(exerciseCount: 0);
        var dialogs = new FakeDialogService
        {
            NextAction = "Excluir",
            NextConfirmation = false
        };
        var viewModel = CreateViewModel(dao, dialogs);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await Assert.Single(viewModel.Workouts).ActionsCommand.ExecuteAsync(null);

        Assert.Single(dialogs.Confirmations);
        Assert.Equal(0, dao.DeleteCalls);
        Assert.Single(viewModel.Workouts);
        Assert.Equal(1, dao.SummaryCalls);
    }

    private static WorkoutsViewModel CreateViewModel(
        FakeWorkoutDao dao,
        FakeDialogService? dialogs = null,
        FakeNotificationService? notifications = null)
    {
        return new WorkoutsViewModel(
            dao,
            dialogs ?? new FakeDialogService(),
            notifications ?? new FakeNotificationService(),
            NullLogger<WorkoutsViewModel>.Instance);
    }

    private static FakeWorkoutDao CreateDaoWithWorkout(int exerciseCount)
    {
        var dao = new FakeWorkoutDao();
        dao.Summaries.Add(new WorkoutSummary
        {
            Id = 1,
            Name = "Treino A",
            ExerciseCount = exerciseCount
        });
        return dao;
    }

    private sealed class FakeWorkoutDao : IWorkoutDao
    {
        public List<WorkoutSummary> Summaries { get; } = [];

        public bool HasSessionsResult { get; set; }

        public bool HasActiveSessionResult { get; set; }

        public int SummaryCalls { get; private set; }

        public int DeleteCalls { get; private set; }

        public Task<IReadOnlyList<Workout>> GetAllAsync()
        {
            return Task.FromResult<IReadOnlyList<Workout>>([]);
        }

        public Task<IReadOnlyList<WorkoutSummary>> GetSummariesAsync()
        {
            SummaryCalls++;
            return Task.FromResult<IReadOnlyList<WorkoutSummary>>(
                Summaries.ToList());
        }

        public Task<Workout?> GetByIdAsync(int id)
        {
            var summary = Summaries.SingleOrDefault(item => item.Id == id);
            return Task.FromResult(summary is null
                ? null
                : new Workout
                {
                    Id = summary.Id,
                    Name = summary.Name,
                    Description = summary.Description
                });
        }

        public Task<IReadOnlyList<WorkoutExercise>> GetExercisesAsync(int workoutId)
        {
            return Task.FromResult<IReadOnlyList<WorkoutExercise>>([]);
        }

        public Task<int> SaveAsync(
            Workout workout,
            IReadOnlyList<WorkoutExercise> exercises)
        {
            throw new NotSupportedException();
        }

        public Task<bool> HasSessionsAsync(int id)
        {
            return Task.FromResult(HasSessionsResult);
        }

        public Task<bool> HasActiveSessionAsync(int id) =>
            Task.FromResult(HasActiveSessionResult);

        public Task<int> DeleteAsync(int id)
        {
            DeleteCalls++;
            return Task.FromResult(Summaries.RemoveAll(item => item.Id == id));
        }
    }

    private sealed class FakeDialogService : IDialogService
    {
        public string? NextAction { get; set; }

        public bool NextConfirmation { get; set; } = true;

        public List<ActionRequest> Actions { get; } = [];

        public List<AlertRequest> Alerts { get; } = [];

        public List<ConfirmationRequest> Confirmations { get; } = [];

        public Task AlertAsync(string title, string message, string button)
        {
            Alerts.Add(new AlertRequest(title, message, button));
            return Task.CompletedTask;
        }

        public Task<string?> ChooseActionAsync(
            string title,
            string cancel,
            params string[] actions)
        {
            Actions.Add(new ActionRequest(title, cancel, actions));
            return Task.FromResult(NextAction);
        }

        public Task<bool> ConfirmAsync(
            string title,
            string message,
            string accept,
            string cancel)
        {
            Confirmations.Add(new ConfirmationRequest(
                title,
                message,
                accept,
                cancel));
            return Task.FromResult(NextConfirmation);
        }
    }

    private sealed class FakeNotificationService : INotificationService
    {
        public List<string> Messages { get; } = [];

        public Task ShowAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed record ActionRequest(
        string Title,
        string Cancel,
        IReadOnlyList<string> Actions);

    private sealed record AlertRequest(
        string Title,
        string Message,
        string Button);

    private sealed record ConfirmationRequest(
        string Title,
        string Message,
        string Accept,
        string Cancel);
}
