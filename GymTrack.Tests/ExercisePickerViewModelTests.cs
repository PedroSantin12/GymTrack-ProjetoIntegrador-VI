using GymTrack.Data.Dao;
using GymTrack.Models;
using GymTrack.Services;
using GymTrack.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace GymTrack.Tests;

public sealed class ExercisePickerViewModelTests
{
    [Fact]
    public async Task InitializeAsync_ListsOnlyActiveExercisesNotAlreadySelected()
    {
        var dao = new FakeExerciseDao();
        dao.Stored.AddRange(
        [
            new Exercise { Id = 1, Name = "Supino", MuscleGroup = "Peito" },
            new Exercise { Id = 2, Name = "Remada", MuscleGroup = "Costas" },
            new Exercise
            {
                Id = 3,
                Name = "Exercício arquivado",
                MuscleGroup = "Outro",
                IsActive = false
            }
        ]);
        var viewModel = CreateViewModel(dao);

        await viewModel.InitializeAsync([1]);

        var item = Assert.Single(viewModel.Exercises);
        Assert.Equal(2, item.Id);
        Assert.Equal((string.Empty, null, false), dao.LastSearchRequest);
    }

    [Fact]
    public async Task SelectCommand_RaisesSelectedExercise()
    {
        var dao = new FakeExerciseDao();
        dao.Stored.Add(new Exercise
        {
            Id = 4,
            Name = "Agachamento",
            MuscleGroup = "Pernas"
        });
        var viewModel = CreateViewModel(dao);
        Exercise? selected = null;
        viewModel.ExerciseSelected += (_, eventArgs) => selected = eventArgs.Exercise;
        await viewModel.InitializeAsync([]);

        Assert.Single(viewModel.Exercises).SelectCommand.Execute(null);

        Assert.NotNull(selected);
        Assert.Equal(4, selected.Id);
        Assert.Equal("Agachamento", selected.Name);
    }

    [Fact]
    public async Task SearchTextChange_LoadsMatchingExercisesAfterDebounce()
    {
        var dao = new FakeExerciseDao();
        dao.Stored.AddRange(
        [
            new Exercise { Id = 1, Name = "Supino reto", MuscleGroup = "Peito" },
            new Exercise { Id = 2, Name = "Remada baixa", MuscleGroup = "Costas" }
        ]);
        var viewModel = CreateViewModel(dao);
        await viewModel.InitializeAsync([]);
        dao.SearchObserved = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        viewModel.SearchText = "sup";
        await dao.SearchObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(("sup", null, false), dao.LastSearchRequest);
        Assert.Equal("Supino reto", Assert.Single(viewModel.Exercises).Name);
    }

    private static ExercisePickerViewModel CreateViewModel(FakeExerciseDao dao)
    {
        return new ExercisePickerViewModel(
            dao,
            new FakeNotificationService(),
            NullLogger<ExercisePickerViewModel>.Instance);
    }

    private sealed class FakeExerciseDao : IExerciseDao
    {
        public List<Exercise> Stored { get; } = [];

        public (string? SearchText, string? MuscleGroup, bool IncludeInactive)? LastSearchRequest { get; private set; }

        public TaskCompletionSource SearchObserved { get; set; } = new(
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
            LastSearchRequest = (searchText, muscleGroup, includeInactive);
            var result = Stored
                .Where(item => includeInactive || item.IsActive)
                .Where(item => string.IsNullOrWhiteSpace(searchText) ||
                    item.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                .ToList();
            SearchObserved.TrySetResult();
            return Task.FromResult<IReadOnlyList<Exercise>>(result);
        }

        public Task<bool> ExistsAsync(
            string name,
            string muscleGroup,
            int excludedId = 0)
        {
            return Task.FromResult(false);
        }

        public Task<int> InsertAsync(Exercise exercise)
        {
            throw new NotSupportedException();
        }

        public Task<int> UpdateAsync(Exercise exercise)
        {
            throw new NotSupportedException();
        }

        public Task<int> ArchiveAsync(int id)
        {
            throw new NotSupportedException();
        }

        public Task<int> DeleteAsync(int id)
        {
            throw new NotSupportedException();
        }

        public Task<bool> HasHistoryAsync(int id)
        {
            return Task.FromResult(false);
        }

        public Task<bool> HasReferencesAsync(int id)
        {
            return Task.FromResult(false);
        }
    }

    private sealed class FakeNotificationService : INotificationService
    {
        public Task ShowAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
