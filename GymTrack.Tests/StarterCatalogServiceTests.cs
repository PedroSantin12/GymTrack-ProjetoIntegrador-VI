using GymTrack.Data.Dao;
using GymTrack.Models;
using GymTrack.Services;

namespace GymTrack.Tests;

public sealed class StarterCatalogServiceTests
{
    [Fact]
    public async Task EnsureSeededAsync_EveryStarterExerciseHasItsOwnGuide()
    {
        await using var context = await TestDatabase.CreateAsync();
        await new StarterCatalogService(context.Database).EnsureSeededAsync();

        foreach (var exercise in await new ExerciseDao(context.Database).GetAllAsync())
        {
            var guide = ExerciseGuideCatalog.ForExercise(exercise.Name);
            Assert.NotNull(guide);
            Assert.True(guide.Steps.Count >= 3, exercise.Name);
            Assert.False(string.IsNullOrWhiteSpace(guide.Muscles));
            Assert.False(string.IsNullOrWhiteSpace(guide.Caution));
        }
    }

    [Fact]
    public async Task EnsureSeededAsync_CreatesTwentyGroupsAndThreeUsableTemplates()
    {
        await using var context = await TestDatabase.CreateAsync();
        var starter = new StarterCatalogService(context.Database);
        var exerciseDao = new ExerciseDao(context.Database);
        var workoutDao = new WorkoutDao(context.Database);
        var catalog = new WorkoutCatalogService(context.Database, workoutDao);

        await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => starter.EnsureSeededAsync()));

        var exercises = await exerciseDao.GetAllAsync();
        var templates = await catalog.GetTemplatesAsync();
        Assert.Equal(33, exercises.Count);
        Assert.Equal(20, exercises.Select(exercise => exercise.MuscleGroup).Distinct().Count());
        Assert.Equal(3, templates.Count);
        Assert.Equal(4, Assert.Single(templates, template => template.Name == "Peito essencial").ExerciseCount);
        Assert.Equal(5, Assert.Single(templates, template => template.Name == "Pernas completas").ExerciseCount);
        Assert.Equal(4, Assert.Single(templates, template => template.Name == "Foco em glúteos").ExerciseCount);

        var copiedId = await catalog.CopyTemplateAsync(
            Assert.Single(templates, template => template.Name == "Peito essencial").Id, 42);
        var copied = await workoutDao.GetByIdAsync(copiedId);
        Assert.NotNull(copied);
        Assert.Equal(42, copied.OwnerUserId);
        Assert.False(copied.IsTemplate);
        Assert.Equal(4, (await workoutDao.GetExercisesAsync(copiedId)).Count);
    }

    [Fact]
    public async Task EnsureSeededAsync_ReusesExistingExerciseWithoutChangingIt()
    {
        await using var context = await TestDatabase.CreateAsync();
        var exerciseDao = new ExerciseDao(context.Database);
        var custom = new Exercise
        {
            Name = "Supino reto com halteres",
            MuscleGroup = "Peito",
            Notes = "Descrição criada pela equipe"
        };
        await exerciseDao.InsertAsync(custom);

        await new StarterCatalogService(context.Database).EnsureSeededAsync();

        var exercises = await exerciseDao.GetAllAsync();
        var saved = Assert.Single(exercises, exercise =>
            exercise.Name == custom.Name && exercise.MuscleGroup == custom.MuscleGroup);
        Assert.Equal(custom.Id, saved.Id);
        Assert.Equal(custom.Notes, saved.Notes);

        var catalog = new WorkoutCatalogService(context.Database, new WorkoutDao(context.Database));
        var chest = Assert.Single(await catalog.GetTemplatesAsync("Peito"));
        var plans = await new WorkoutDao(context.Database).GetExercisesAsync(chest.Id);
        Assert.Contains(plans, plan => plan.ExerciseId == custom.Id);
    }

    [Fact]
    public async Task EnsureSeededAsync_DoesNotRecreateDeletedTemplate()
    {
        await using var context = await TestDatabase.CreateAsync();
        var catalog = new WorkoutCatalogService(context.Database, new WorkoutDao(context.Database));
        await new StarterCatalogService(context.Database).EnsureSeededAsync();
        var glutes = Assert.Single(await catalog.GetTemplatesAsync("glúteos"));

        await catalog.DeleteTemplateAsync(glutes.Id);
        await new StarterCatalogService(context.Database).EnsureSeededAsync();

        Assert.Empty(await catalog.GetTemplatesAsync("glúteos"));
        Assert.Equal(2, await catalog.CountTemplatesAsync());
        Assert.Equal(33, (await new ExerciseDao(context.Database).GetAllAsync()).Count);
    }
}
