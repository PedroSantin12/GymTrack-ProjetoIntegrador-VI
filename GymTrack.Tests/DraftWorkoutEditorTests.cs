using GymTrack.Data.Dao;
using GymTrack.Models;
using GymTrack.Services;
using GymTrack.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace GymTrack.Tests;

public sealed class DraftWorkoutEditorTests
{
    [Theory]
    [InlineData(7, false)]
    [InlineData(null, true)]
    public async Task InitializeNewAsync_ClosingWithoutSaving_DoesNotPersistWorkout(int? ownerUserId, bool isTemplate)
    {
        await using var context = await TestDatabase.CreateAsync();
        var dao = new WorkoutDao(context.Database);
        var editor = CreateEditor(context);
        var closed = false;
        editor.CloseRequested += (_, _) => closed = true;

        await editor.InitializeNewAsync(ownerUserId, isTemplate);
        editor.Name = "Treino descartado";
        editor.CloseCommand.Execute(null);

        Assert.True(closed);
        Assert.Empty(await dao.GetAllAsync());
        Assert.Empty(await new WorkoutCatalogService(context.Database, dao).GetTemplatesAsync());
    }

    [Fact]
    public async Task SaveAsync_NewConsumerWorkout_PersistsOnlyOnSaveWithItsOwner()
    {
        await using var context = await TestDatabase.CreateAsync();
        var dao = new WorkoutDao(context.Database);
        var editor = CreateEditor(context);
        await editor.InitializeNewAsync(7, false);
        editor.Name = "Meu treino pessoal";
        Assert.Empty(await dao.GetAllAsync());

        await editor.SaveCommand.ExecuteAsync(null);

        var workout = Assert.Single(await dao.GetAllAsync());
        Assert.Equal("Meu treino pessoal", workout.Name);
        Assert.Equal(7, workout.OwnerUserId);
        Assert.False(workout.IsTemplate);
        Assert.False(editor.HasValidationMessage);
        Assert.Empty(await dao.GetExercisesAsync(workout.Id));
    }

    [Fact]
    public async Task SaveAsync_NewTemplateWithExercise_PersistsAndCanBeCopiedIndependently()
    {
        await using var context = await TestDatabase.CreateAsync();
        var dao = new WorkoutDao(context.Database);
        var catalog = new WorkoutCatalogService(context.Database, dao);
        var exercise = new Exercise { Name = "Agachamento", MuscleGroup = "Pernas" };
        await new ExerciseDao(context.Database).InsertAsync(exercise);
        var editor = CreateEditor(context);
        await editor.InitializeNewAsync(null, true);
        editor.Name = "Treino pronto A";
        Assert.True(editor.AddExercise(exercise));
        Assert.Empty(await catalog.GetTemplatesAsync());

        await editor.SaveCommand.ExecuteAsync(null);

        var template = Assert.Single(await catalog.GetTemplatesAsync());
        var stored = await dao.GetByIdAsync(template.Id);
        Assert.NotNull(stored);
        Assert.True(stored.IsTemplate);
        Assert.Null(stored.OwnerUserId);
        Assert.Equal(1, template.ExerciseCount);
        var copiedId = await catalog.CopyTemplateAsync(template.Id, 7);
        Assert.NotEqual(template.Id, copiedId);
        Assert.Equal(7, (await dao.GetByIdAsync(copiedId))!.OwnerUserId);
        Assert.False((await dao.GetByIdAsync(copiedId))!.IsTemplate);
        Assert.Equal(exercise.Id, Assert.Single(await dao.GetExercisesAsync(copiedId)).ExerciseId);
        Assert.Equal(template.Id, Assert.Single(await catalog.GetTemplatesAsync()).Id);
    }

    [Fact]
    public async Task SaveAsync_EmptyTemplate_DoesNotPersistOrCloseEditor()
    {
        await using var context = await TestDatabase.CreateAsync();
        var editor = CreateEditor(context);
        var closed = false;
        editor.CloseRequested += (_, _) => closed = true;
        await editor.InitializeNewAsync(null, true);
        editor.Name = "Treino incompleto";

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.False(closed);
        Assert.True(editor.CanSave);
        Assert.Contains("ao menos um exercício", editor.ValidationMessage);
        Assert.Empty(await new WorkoutDao(context.Database).GetAllAsync());
    }

    [Theory]
    [InlineData(7, false)]
    [InlineData(null, true)]
    public async Task InitializeAsync_NewLegacyDraft_ResetsPreviousOwnershipAndTemplateState(int? ownerUserId, bool isTemplate)
    {
        await using var context = await TestDatabase.CreateAsync();
        var editor = CreateEditor(context);
        await editor.InitializeNewAsync(ownerUserId, isTemplate);
        await editor.InitializeAsync(null);
        editor.Name = "Rascunho independente";

        await editor.SaveCommand.ExecuteAsync(null);

        var workout = Assert.Single(await new WorkoutDao(context.Database).GetAllAsync());
        Assert.Null(workout.OwnerUserId);
        Assert.False(workout.IsTemplate);
    }

    [Fact]
    public async Task InitializeAsync_ExistingTemplate_ReloadsTemplateValidation()
    {
        await using var context = await TestDatabase.CreateAsync();
        var dao = new WorkoutDao(context.Database);
        var template = new Workout { Name = "Treino a completar", IsTemplate = true };
        await dao.SaveAsync(template, []);
        var editor = CreateEditor(context);
        await editor.InitializeNewAsync(7, false);
        await editor.InitializeAsync(template.Id);
        editor.Name = "Não deve ser atualizado vazio";

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.Contains("ao menos um exercício", editor.ValidationMessage);
        Assert.Equal("Treino a completar", (await dao.GetByIdAsync(template.Id))!.Name);
        Assert.True((await dao.GetByIdAsync(template.Id))!.IsTemplate);
        Assert.Null((await dao.GetByIdAsync(template.Id))!.OwnerUserId);
    }

    private static WorkoutEditViewModel CreateEditor(TestDatabase context) => new(
        new WorkoutDao(context.Database),
        new ExerciseDao(context.Database),
        new SilentNotifications(),
        NullLogger<WorkoutEditViewModel>.Instance);

    private sealed class SilentNotifications : INotificationService
    {
        public Task ShowAsync(string message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
