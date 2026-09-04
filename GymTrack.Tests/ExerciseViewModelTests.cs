using GymTrack.Data.Dao;
using GymTrack.Models;
using GymTrack.Services;
using GymTrack.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace GymTrack.Tests;

public sealed class ExerciseViewModelTests
{
    public static TheoryData<string, string?, string> InvalidExerciseData => new()
    {
        { string.Empty, "Peito", string.Empty },
        { "A", "Peito", string.Empty },
        { new string('A', 81), "Peito", string.Empty },
        { "Supino reto", null, string.Empty },
        { "Supino reto", "Peito", new string('N', 501) }
    };

    [Theory]
    [MemberData(nameof(InvalidExerciseData))]
    public async Task SaveAsync_InvalidData_DoesNotPersist(
        string name,
        string? muscleGroup,
        string notes)
    {
        var dao = new FakeExerciseDao();
        var viewModel = CreateEditor(dao);
        viewModel.Name = name;
        viewModel.SelectedMuscleGroup = muscleGroup;
        viewModel.Notes = notes;

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasValidationMessage);
        Assert.Equal(0, dao.InsertCalls);
        Assert.Equal(0, dao.UpdateCalls);
    }

    [Fact]
    public async Task SaveAsync_DuplicateExercise_IsRejected()
    {
        var dao = new FakeExerciseDao { DuplicateResult = true };
        var notifications = new FakeNotificationService();
        var viewModel = CreateEditor(dao, notifications: notifications);
        var closed = false;
        viewModel.CloseRequested += (_, _) => closed = true;
        viewModel.Name = "  Supino reto  ";
        viewModel.SelectedMuscleGroup = "Peito";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(("Supino reto", "Peito", 0), dao.LastExistsRequest);
        Assert.Contains("Já existe", viewModel.ValidationMessage);
        Assert.Equal(0, dao.InsertCalls);
        Assert.False(closed);
        Assert.Empty(notifications.Messages);
    }

    [Fact]
    public async Task SaveAsync_NewExercise_InsertsTrimmedValuesAndCloses()
    {
        var dao = new FakeExerciseDao();
        var notifications = new FakeNotificationService();
        var viewModel = CreateEditor(dao, notifications: notifications);
        var closed = false;
        viewModel.CloseRequested += (_, _) => closed = true;
        viewModel.Name = "  Supino reto  ";
        viewModel.SelectedMuscleGroup = "Peito";
        viewModel.Notes = "  Pegada média  ";
        viewModel.IsActive = true;

        await viewModel.SaveCommand.ExecuteAsync(null);

        var inserted = Assert.Single(dao.Stored);
        Assert.Equal("Supino reto", inserted.Name);
        Assert.Equal("Peito", inserted.MuscleGroup);
        Assert.Equal("Pegada média", inserted.Notes);
        Assert.True(inserted.IsActive);
        Assert.Equal(1, dao.InsertCalls);
        Assert.Equal(0, dao.UpdateCalls);
        Assert.True(closed);
        Assert.Contains("Exercício salvo.", notifications.Messages);
    }

    [Fact]
    public async Task SaveAsync_ExistingExercise_UpdatesAndExcludesOwnIdFromDuplicateCheck()
    {
        var dao = new FakeExerciseDao();
        dao.Stored.Add(new Exercise
        {
            Id = 7,
            Name = "Supino reto",
            MuscleGroup = "Peito",
            IsActive = true
        });
        var viewModel = CreateEditor(dao);
        await viewModel.InitializeAsync(7);
        viewModel.Name = "Supino inclinado";
        viewModel.Notes = "Halteres";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(("Supino inclinado", "Peito", 7), dao.LastExistsRequest);
        Assert.Equal(0, dao.InsertCalls);
        Assert.Equal(1, dao.UpdateCalls);
        Assert.Equal(7, Assert.Single(dao.Stored).Id);
        Assert.Equal("Supino inclinado", dao.Stored[0].Name);
    }

    [Fact]
    public async Task SaveAsync_ArchivingFromEditorRequiresConfirmation()
    {
        var dao = new FakeExerciseDao();
        dao.Stored.Add(new Exercise
        {
            Id = 4,
            Name = "Remada baixa",
            MuscleGroup = "Costas",
            IsActive = true
        });
        var dialogs = new FakeDialogService { NextResult = false };
        var viewModel = CreateEditor(dao, dialogs);
        await viewModel.InitializeAsync(4);
        viewModel.IsActive = false;

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Single(dialogs.Requests);
        Assert.Equal("Arquivar", dialogs.Requests[0].Accept);
        Assert.Equal(0, dao.UpdateCalls);
        Assert.True(dao.Stored[0].IsActive);
    }

    [Fact]
    public async Task LoadAsync_PopulatesActiveExercises()
    {
        var dao = new FakeExerciseDao();
        dao.Stored.AddRange(
        [
            new Exercise { Id = 1, Name = "Supino", MuscleGroup = "Peito" },
            new Exercise { Id = 2, Name = "Remada", MuscleGroup = "Costas", IsActive = false }
        ]);
        var viewModel = CreateList(dao);

        await viewModel.LoadCommand.ExecuteAsync(null);

        var item = Assert.Single(viewModel.Exercises);
        Assert.Equal("Supino", item.Name);
        Assert.Equal((string.Empty, null, false), dao.LastSearchRequest);
    }

    [Fact]
    public async Task SearchAndGroupChanges_AreCombinedAfterDebounce()
    {
        var dao = new FakeExerciseDao();
        var viewModel = CreateList(dao);

        viewModel.SearchText = "sup";
        viewModel.SelectedMuscleGroup = "Peito";

        await dao.SearchObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, dao.SearchCalls);
        Assert.Equal(("sup", "Peito", false), dao.LastSearchRequest);
    }

    [Fact]
    public async Task ActionsMenu_EditRequestsTheExistingExerciseEditor()
    {
        var dao = CreateDaoWithExercise();
        var dialogs = new FakeDialogService { NextAction = "Editar" };
        var viewModel = CreateList(dao, dialogs);
        int? requestedId = null;
        viewModel.EditorRequested += (_, eventArgs) => requestedId = eventArgs.ExerciseId;
        await viewModel.LoadCommand.ExecuteAsync(null);

        await Assert.Single(viewModel.Exercises).ActionsCommand.ExecuteAsync(null);

        var request = Assert.Single(dialogs.ActionRequests);
        Assert.Equal(["Editar", "Remover"], request.Actions);
        Assert.Equal(1, requestedId);
        Assert.Empty(dialogs.Requests);
    }

    [Fact]
    public async Task RemoveAsync_WhenConfirmationIsCancelled_DoesNothing()
    {
        var dao = CreateDaoWithExercise();
        var dialogs = new FakeDialogService { NextResult = false };
        var viewModel = CreateList(dao, dialogs);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await Assert.Single(viewModel.Exercises).ActionsCommand.ExecuteAsync(null);

        Assert.Single(dialogs.ActionRequests);
        Assert.Single(dialogs.Requests);
        Assert.Equal(0, dao.DeleteCalls);
        Assert.Equal(0, dao.ArchiveCalls);
        Assert.Single(dao.Stored);
    }

    [Fact]
    public async Task RemoveAsync_WithoutReferences_DeletesExercise()
    {
        var dao = CreateDaoWithExercise();
        var dialogs = new FakeDialogService { NextResult = true };
        var notifications = new FakeNotificationService();
        var viewModel = CreateList(dao, dialogs, notifications);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await Assert.Single(viewModel.Exercises).ActionsCommand.ExecuteAsync(null);

        Assert.Single(dialogs.ActionRequests);
        Assert.Equal("Excluir", Assert.Single(dialogs.Requests).Accept);
        Assert.Equal(1, dao.DeleteCalls);
        Assert.Equal(0, dao.ArchiveCalls);
        Assert.Empty(dao.Stored);
        Assert.Contains("Exercício excluído.", notifications.Messages);
    }

    [Fact]
    public async Task RemoveAsync_WithReferences_ArchivesExercise()
    {
        var dao = CreateDaoWithExercise();
        dao.HasReferencesResult = true;
        var dialogs = new FakeDialogService { NextResult = true };
        var notifications = new FakeNotificationService();
        var viewModel = CreateList(dao, dialogs, notifications);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await Assert.Single(viewModel.Exercises).ActionsCommand.ExecuteAsync(null);

        Assert.Single(dialogs.ActionRequests);
        Assert.Equal("Arquivar", Assert.Single(dialogs.Requests).Accept);
        Assert.Equal(0, dao.DeleteCalls);
        Assert.Equal(1, dao.ArchiveCalls);
        Assert.False(Assert.Single(dao.Stored).IsActive);
        Assert.Empty(viewModel.Exercises);
        Assert.Contains("Exercício arquivado.", notifications.Messages);
    }

    private static ExerciseEditViewModel CreateEditor(
        FakeExerciseDao dao,
        FakeDialogService? dialogs = null,
        FakeNotificationService? notifications = null)
    {
        return new ExerciseEditViewModel(
            dao,
            dialogs ?? new FakeDialogService(),
            notifications ?? new FakeNotificationService(),
            NullLogger<ExerciseEditViewModel>.Instance);
    }

    private static ExercisesViewModel CreateList(
        FakeExerciseDao dao,
        FakeDialogService? dialogs = null,
        FakeNotificationService? notifications = null)
    {
        return new ExercisesViewModel(
            dao,
            dialogs ?? new FakeDialogService(),
            notifications ?? new FakeNotificationService(),
            NullLogger<ExercisesViewModel>.Instance);
    }

    private static FakeExerciseDao CreateDaoWithExercise()
    {
        var dao = new FakeExerciseDao();
        dao.Stored.Add(new Exercise
        {
            Id = 1,
            Name = "Supino reto",
            MuscleGroup = "Peito",
            IsActive = true
        });
        return dao;
    }

    private sealed class FakeDialogService : IDialogService
    {
        public bool NextResult { get; set; } = true;

        public string? NextAction { get; set; } = "Remover";

        public List<DialogRequest> Requests { get; } = [];

        public List<ActionRequest> ActionRequests { get; } = [];

        public Task<string?> ChooseActionAsync(
            string title,
            string cancel,
            params string[] actions)
        {
            ActionRequests.Add(new ActionRequest(title, cancel, actions));
            return Task.FromResult(NextAction);
        }

        public Task<bool> ConfirmAsync(
            string title,
            string message,
            string accept,
            string cancel)
        {
            Requests.Add(new DialogRequest(title, message, accept, cancel));
            return Task.FromResult(NextResult);
        }
    }

    private sealed record DialogRequest(
        string Title,
        string Message,
        string Accept,
        string Cancel);

    private sealed record ActionRequest(
        string Title,
        string Cancel,
        IReadOnlyList<string> Actions);

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

    private sealed class FakeExerciseDao : IExerciseDao
    {
        private int _nextId = 100;

        public List<Exercise> Stored { get; } = [];

        public bool DuplicateResult { get; set; }

        public bool HasReferencesResult { get; set; }

        public int InsertCalls { get; private set; }

        public int UpdateCalls { get; private set; }

        public int DeleteCalls { get; private set; }

        public int ArchiveCalls { get; private set; }

        public int SearchCalls { get; private set; }

        public (string Name, string MuscleGroup, int ExcludedId)? LastExistsRequest { get; private set; }

        public (string? SearchText, string? MuscleGroup, bool IncludeInactive)? LastSearchRequest { get; private set; }

        public TaskCompletionSource SearchObserved { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<IReadOnlyList<Exercise>> GetAllAsync(bool includeInactive = false)
        {
            return Task.FromResult<IReadOnlyList<Exercise>>(
                Stored.Where(item => includeInactive || item.IsActive).ToList());
        }

        public Task<Exercise?> GetByIdAsync(int id)
        {
            return Task.FromResult(Stored.SingleOrDefault(item => item.Id == id));
        }

        public Task<IReadOnlyList<Exercise>> SearchAsync(
            string? searchText,
            string? muscleGroup = null,
            bool includeInactive = false)
        {
            SearchCalls++;
            LastSearchRequest = (searchText, muscleGroup, includeInactive);
            var result = Stored
                .Where(item => includeInactive || item.IsActive)
                .Where(item => string.IsNullOrWhiteSpace(searchText) ||
                    item.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                .Where(item => string.IsNullOrWhiteSpace(muscleGroup) ||
                    item.MuscleGroup.Equals(muscleGroup, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.Name)
                .ToList();
            SearchObserved.TrySetResult();
            return Task.FromResult<IReadOnlyList<Exercise>>(result);
        }

        public Task<bool> ExistsAsync(
            string name,
            string muscleGroup,
            int excludedId = 0)
        {
            LastExistsRequest = (name, muscleGroup, excludedId);
            return Task.FromResult(DuplicateResult);
        }

        public Task<int> InsertAsync(Exercise exercise)
        {
            InsertCalls++;
            exercise.Id = _nextId++;
            Stored.Add(exercise);
            return Task.FromResult(1);
        }

        public Task<int> UpdateAsync(Exercise exercise)
        {
            UpdateCalls++;
            var index = Stored.FindIndex(item => item.Id == exercise.Id);
            if (index >= 0)
            {
                Stored[index] = exercise;
                return Task.FromResult(1);
            }

            return Task.FromResult(0);
        }

        public Task<int> ArchiveAsync(int id)
        {
            ArchiveCalls++;
            var exercise = Stored.Single(item => item.Id == id);
            exercise.IsActive = false;
            return Task.FromResult(1);
        }

        public Task<int> DeleteAsync(int id)
        {
            DeleteCalls++;
            return Task.FromResult(Stored.RemoveAll(item => item.Id == id));
        }

        public Task<bool> HasHistoryAsync(int id)
        {
            return Task.FromResult(HasReferencesResult);
        }

        public Task<bool> HasReferencesAsync(int id)
        {
            return Task.FromResult(HasReferencesResult);
        }
    }
}
