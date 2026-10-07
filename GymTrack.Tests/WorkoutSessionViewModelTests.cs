using GymTrack.Data.Dao;
using GymTrack.Models;
using GymTrack.Services;
using GymTrack.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace GymTrack.Tests;

public sealed class WorkoutSessionViewModelTests
{
    [Fact]
    public async Task InitializeAsync_CreatesSessionAndLoadsPlannedAndPreviousValues()
    {
        var data = CreateData();
        var time = new ManualTimeProvider(
            new DateTimeOffset(2026, 9, 4, 18, 0, 0, TimeSpan.Zero));
        var viewModel = CreateViewModel(data, time: time);

        var initialized = await viewModel.InitializeAsync(1);

        Assert.True(initialized);
        Assert.Equal(1, data.Sessions.InsertCalls);
        Assert.Equal(time.UtcNow.UtcDateTime, data.Sessions.ActiveSession?.StartedAt);
        var exercise = Assert.Single(viewModel.Exercises);
        Assert.Equal("Remada", exercise.Name);
        Assert.Equal("Anterior: 35 kg x 12", exercise.PreviousPerformanceText);
        Assert.Equal(2, exercise.Sets.Count);
        Assert.All(exercise.Sets, set =>
        {
            Assert.Equal("30", set.LoadText);
            Assert.Equal("10", set.RepsText);
            Assert.False(set.IsCompleted);
        });
        Assert.Equal("35 x 12", exercise.Sets[0].PreviousText);
        Assert.Equal("0 de 2 séries concluídas", viewModel.ProgressText);
    }

    [Fact]
    public async Task CompleteSet_ValidatesAndPersistsWithProgressAndVolume()
    {
        var data = CreateData();
        var notifications = new FakeNotificationService();
        var viewModel = CreateViewModel(data, notifications: notifications);
        await viewModel.InitializeAsync(1);
        var exercise = Assert.Single(viewModel.Exercises);
        var first = exercise.Sets[0];
        first.LoadText = "22.5";
        first.RepsText = "8";

        await first.CompleteCommand.ExecuteAsync(null);

        Assert.True(first.IsCompleted);
        var saved = Assert.Single(data.Sessions.Sets);
        Assert.Equal(22.5, saved.LoadKg);
        Assert.Equal(8, saved.Reps);
        Assert.Equal(0.5, viewModel.Progress);
        Assert.Equal("180 kg de volume", viewModel.VolumeText);
        Assert.Contains("Série concluída.", notifications.Messages);

        var second = exercise.Sets[1];
        second.LoadText = "-1";
        await second.CompleteCommand.ExecuteAsync(null);
        Assert.False(second.IsCompleted);
        Assert.Contains("não negativa", second.ValidationMessage);
        Assert.Single(data.Sessions.Sets);
    }

    [Fact]
    public async Task AddAndRemoveSet_UpdatesPlannedProgress()
    {
        var data = CreateData();
        var viewModel = CreateViewModel(data);
        await viewModel.InitializeAsync(1);
        var exercise = Assert.Single(viewModel.Exercises);

        exercise.AddSetCommand.Execute(null);
        var added = exercise.Sets[^1];

        Assert.Equal(3, exercise.Sets.Count);
        Assert.True(added.CanRemove);
        Assert.Equal("0 de 3 séries concluídas", viewModel.ProgressText);

        added.RemoveCommand.Execute(null);
        Assert.Equal(2, exercise.Sets.Count);
        Assert.Equal("0 de 2 séries concluídas", viewModel.ProgressText);
    }

    [Fact]
    public async Task PendingSetSave_CannotBeRemovedOrFinished()
    {
        var data = CreateData();
        var pendingSave = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        data.Sessions.SaveSetGate = pendingSave;
        var viewModel = CreateViewModel(data);
        await viewModel.InitializeAsync(1);
        var exercise = Assert.Single(viewModel.Exercises);
        var lastSet = exercise.Sets[^1];

        var saving = lastSet.CompleteCommand.ExecuteAsync(null);
        Assert.True(lastSet.IsBusy);
        Assert.False(lastSet.CanRemove);
        lastSet.RemoveCommand.Execute(null);
        Assert.Equal(2, exercise.Sets.Count);

        await viewModel.FinishCommand.ExecuteAsync(null);
        Assert.Contains("Aguarde", viewModel.ValidationMessage);
        Assert.Null(data.Sessions.ActiveSession?.FinishedAt);

        pendingSave.SetResult(true);
        await saving;
        Assert.True(lastSet.IsCompleted);
        Assert.Equal(1, viewModel.CompletedSetsCount);
        await viewModel.FinishCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsFinished);
    }

    [Fact]
    public async Task ResumeAfterOldCompositionChange_ShowsPersistedSets()
    {
        var data = CreateData();
        var original = CreateViewModel(data);
        await original.InitializeAsync(1);
        await original.Exercises[0].Sets[0].CompleteCommand.ExecuteAsync(null);
        data.Workouts.Composition.Clear();

        var resumed = CreateViewModel(data);
        Assert.True(await resumed.InitializeAsync(1));
        var exercise = Assert.Single(resumed.Exercises);
        Assert.True(Assert.Single(exercise.Sets).IsCompleted);
        Assert.Equal(1, resumed.CompletedSetsCount);
        Assert.Equal("300 kg de volume", resumed.VolumeText);
    }

    [Fact]
    public async Task ResumeEmptyOldComposition_AllowsSessionToBeFinished()
    {
        var data = CreateData();
        await CreateViewModel(data).InitializeAsync(1);
        data.Workouts.Composition.Clear();

        var resumed = CreateViewModel(data);
        Assert.True(await resumed.InitializeAsync(1));
        Assert.Empty(resumed.Exercises);
        await resumed.FinishCommand.ExecuteAsync(null);
        Assert.True(resumed.IsFinished);
    }

    [Fact]
    public async Task FinishAsync_SetsFinishedAtShowsSummaryAndCloses()
    {
        var data = CreateData();
        var dialogs = new FakeDialogService { NextConfirmation = true };
        var time = new ManualTimeProvider(
            new DateTimeOffset(2026, 9, 4, 18, 0, 0, TimeSpan.Zero));
        var viewModel = CreateViewModel(data, dialogs, time: time);
        var closeRequests = 0;
        viewModel.CloseRequested += (_, _) => closeRequests++;
        await viewModel.InitializeAsync(1);
        var set = Assert.Single(viewModel.Exercises).Sets[0];
        set.LoadText = "40";
        set.RepsText = "10";
        await set.CompleteCommand.ExecuteAsync(null);
        time.UtcNow = time.UtcNow.AddMinutes(45);

        await viewModel.FinishCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsFinished);
        Assert.Equal(time.UtcNow.UtcDateTime, data.Sessions.ActiveSession?.FinishedAt);
        Assert.Equal(1, data.Sessions.UpdateCalls);
        Assert.Equal(1, closeRequests);
        Assert.Contains(dialogs.Alerts, alert =>
            alert.Title == "Treino finalizado" &&
            alert.Message.Contains("00:45:00") &&
            alert.Message.Contains("400 kg"));
    }

    [Fact]
    public async Task RequestExit_OnlyClosesAfterConfirmationAndKeepsSessionActive()
    {
        var data = CreateData();
        var dialogs = new FakeDialogService { NextConfirmation = false };
        var viewModel = CreateViewModel(data, dialogs);
        var closeRequests = 0;
        viewModel.CloseRequested += (_, _) => closeRequests++;
        await viewModel.InitializeAsync(1);

        await viewModel.RequestExitCommand.ExecuteAsync(null);
        Assert.Equal(0, closeRequests);

        dialogs.NextConfirmation = true;
        await viewModel.RequestExitCommand.ExecuteAsync(null);

        Assert.Equal(1, closeRequests);
        Assert.Null(data.Sessions.ActiveSession?.FinishedAt);
        Assert.Equal(0, data.Sessions.UpdateCalls);
    }

    private static TestData CreateData()
    {
        var workouts = new FakeWorkoutDao
        {
            Workout = new Workout { Id = 1, Name = "Treino A" }
        };
        workouts.Composition.Add(new WorkoutExercise
        {
            WorkoutId = 1,
            ExerciseId = 10,
            OrderIndex = 0,
            PlannedSets = 2,
            PlannedReps = 10,
            PlannedLoad = 30
        });
        var exercises = new FakeExerciseDao();
        exercises.Items.Add(new Exercise
        {
            Id = 10,
            Name = "Remada",
            MuscleGroup = "Costas"
        });
        var sessions = new FakeSessionDao();
        sessions.PreviousSets.Add(new SetRecord
        {
            Id = 1,
            SessionId = 20,
            ExerciseId = 10,
            SetNumber = 1,
            Reps = 12,
            LoadKg = 35
        });
        return new TestData(workouts, exercises, sessions);
    }

    private static WorkoutSessionViewModel CreateViewModel(
        TestData data,
        FakeDialogService? dialogs = null,
        FakeNotificationService? notifications = null,
        ManualTimeProvider? time = null)
    {
        return new WorkoutSessionViewModel(
            data.Workouts,
            data.Exercises,
            data.Sessions,
            dialogs ?? new FakeDialogService(),
            notifications ?? new FakeNotificationService(),
            time ?? new ManualTimeProvider(DateTimeOffset.UtcNow),
            NullLogger<WorkoutSessionViewModel>.Instance);
    }

    private sealed record TestData(
        FakeWorkoutDao Workouts,
        FakeExerciseDao Exercises,
        FakeSessionDao Sessions);

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class FakeWorkoutDao : IWorkoutDao
    {
        public Workout? Workout { get; set; }

        public List<WorkoutExercise> Composition { get; } = [];

        public Task<IReadOnlyList<Workout>> GetAllAsync() =>
            Task.FromResult<IReadOnlyList<Workout>>(Workout is null ? [] : [Workout]);

        public Task<IReadOnlyList<WorkoutSummary>> GetSummariesAsync() =>
            Task.FromResult<IReadOnlyList<WorkoutSummary>>([]);

        public Task<Workout?> GetByIdAsync(int id) =>
            Task.FromResult(Workout?.Id == id ? Workout : null);

        public Task<IReadOnlyList<WorkoutExercise>> GetExercisesAsync(int workoutId) =>
            Task.FromResult<IReadOnlyList<WorkoutExercise>>(
                Composition.Where(item => item.WorkoutId == workoutId).ToList());

        public Task<int> SaveAsync(Workout workout, IReadOnlyList<WorkoutExercise> exercises) =>
            throw new NotSupportedException();

        public Task<bool> HasSessionsAsync(int id) => Task.FromResult(false);

        public Task<int> DeleteAsync(int id) => throw new NotSupportedException();
    }

    private sealed class FakeExerciseDao : IExerciseDao
    {
        public List<Exercise> Items { get; } = [];

        public Task<IReadOnlyList<Exercise>> GetAllAsync(bool includeInactive = false) =>
            Task.FromResult<IReadOnlyList<Exercise>>(
                Items.Where(item => includeInactive || item.IsActive).ToList());

        public Task<Exercise?> GetByIdAsync(int id) =>
            Task.FromResult(Items.SingleOrDefault(item => item.Id == id));

        public Task<IReadOnlyList<Exercise>> SearchAsync(
            string? searchText,
            string? muscleGroup = null,
            bool includeInactive = false) =>
            Task.FromResult<IReadOnlyList<Exercise>>([]);

        public Task<bool> ExistsAsync(string name, string muscleGroup, int excludedId = 0) =>
            Task.FromResult(false);

        public Task<int> InsertAsync(Exercise exercise) => throw new NotSupportedException();

        public Task<int> UpdateAsync(Exercise exercise) => throw new NotSupportedException();

        public Task<int> ArchiveAsync(int id) => throw new NotSupportedException();

        public Task<int> DeleteAsync(int id) => throw new NotSupportedException();

        public Task<bool> HasHistoryAsync(int id) => Task.FromResult(false);

        public Task<bool> HasReferencesAsync(int id) => Task.FromResult(false);
    }

    private sealed class FakeSessionDao : ISessionDao
    {
        private int _nextSessionId = 50;
        private int _nextSetId = 100;

        public TaskCompletionSource<bool>? SaveSetGate { get; set; }

        public WorkoutSession? ActiveSession { get; set; }

        public List<SetRecord> Sets { get; } = [];

        public List<SetRecord> PreviousSets { get; } = [];

        public int InsertCalls { get; private set; }

        public int UpdateCalls { get; private set; }

        public Task<IReadOnlyList<WorkoutSession>> GetAllAsync() =>
            Task.FromResult<IReadOnlyList<WorkoutSession>>(
                ActiveSession is null ? [] : [ActiveSession]);

        public Task<WorkoutSession?> GetByIdAsync(int id) =>
            Task.FromResult(ActiveSession?.Id == id ? ActiveSession : null);

        public Task<WorkoutSession?> GetActiveAsync() =>
            Task.FromResult(ActiveSession?.FinishedAt is null ? ActiveSession : null);

        public Task<IReadOnlyList<SetRecord>> GetSetsAsync(int sessionId) =>
            Task.FromResult<IReadOnlyList<SetRecord>>(
                Sets.Where(record => record.SessionId == sessionId).ToList());

        public Task<IReadOnlyList<SetRecord>> GetPreviousSetsAsync(
            int workoutId,
            int exerciseId,
            int excludedSessionId) =>
            Task.FromResult<IReadOnlyList<SetRecord>>(
                PreviousSets.Where(record => record.ExerciseId == exerciseId).ToList());

        public Task<int> InsertAsync(WorkoutSession session)
        {
            InsertCalls++;
            session.Id = _nextSessionId++;
            ActiveSession = session;
            return Task.FromResult(1);
        }

        public Task<int> UpdateAsync(WorkoutSession session)
        {
            UpdateCalls++;
            ActiveSession = session;
            return Task.FromResult(1);
        }

        public async Task<int> SaveSetAsync(SetRecord setRecord)
        {
            if (SaveSetGate is not null)
            {
                await SaveSetGate.Task;
            }

            if (setRecord.Id == 0)
            {
                setRecord.Id = _nextSetId++;
                Sets.Add(setRecord);
            }
            else
            {
                var index = Sets.FindIndex(record => record.Id == setRecord.Id);
                Sets[index] = setRecord;
            }

            return 1;
        }

        public Task<int> DeleteSetAsync(int id) => throw new NotSupportedException();

        public Task<int> DeleteAsync(int id) => throw new NotSupportedException();
    }

    private sealed class FakeDialogService : IDialogService
    {
        public bool NextConfirmation { get; set; } = true;

        public List<AlertRequest> Alerts { get; } = [];

        public Task AlertAsync(string title, string message, string button)
        {
            Alerts.Add(new AlertRequest(title, message));
            return Task.CompletedTask;
        }

        public Task<string?> ChooseActionAsync(
            string title,
            string cancel,
            params string[] actions) => Task.FromResult<string?>(null);

        public Task<bool> ConfirmAsync(
            string title,
            string message,
            string accept,
            string cancel) => Task.FromResult(NextConfirmation);
    }

    private sealed record AlertRequest(string Title, string Message);

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
}
