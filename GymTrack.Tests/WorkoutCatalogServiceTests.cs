using GymTrack.Data;
using GymTrack.Data.Dao;
using GymTrack.Models;
using GymTrack.Services;
using SQLite;

namespace GymTrack.Tests;

public sealed class WorkoutCatalogServiceTests
{
    [Fact]
    public async Task GetPersonalAsync_SeparatesOwnersAndSharedTemplates()
    {
        await using var context = await TestDatabase.CreateAsync();
        var service = CreateService(context);
        var firstId = await service.CreatePersonalAsync(1);
        var secondId = await service.CreatePersonalAsync(2);
        var templateId = await service.CreateTemplateAsync();

        Assert.Equal(firstId, Assert.Single(await service.GetPersonalAsync(1)).Id);
        Assert.Equal(secondId, Assert.Single(await service.GetPersonalAsync(2)).Id);
        Assert.Equal(templateId, Assert.Single(await service.GetTemplatesAsync()).Id);
        Assert.Empty(await service.GetPersonalAsync(3));
        Assert.Equal(1, await service.CountTemplatesAsync());
    }

    [Fact]
    public async Task GetTemplatesAsync_SearchesNameAndDescriptionWithoutPersonalWorkouts()
    {
        await using var context = await TestDatabase.CreateAsync();
        var dao = new WorkoutDao(context.Database);
        var service = CreateService(context);
        var template = new Workout
        {
            Name = "Treino de pernas",
            Description = "Foco em mobilidade",
            IsTemplate = true
        };
        await dao.SaveAsync(template, []);
        await dao.SaveAsync(new Workout
        {
            Name = "Treino de pernas particular",
            OwnerUserId = 1
        }, []);

        Assert.Equal(template.Id, Assert.Single(await service.GetTemplatesAsync(" PERNAS ")).Id);
        Assert.Equal(template.Id, Assert.Single(await service.GetTemplatesAsync("MOBILIDADE")).Id);
        Assert.Empty(await service.GetTemplatesAsync("costas"));
    }

    [Fact]
    public async Task CopyTemplateAsync_CreatesIndependentPersonalComposition()
    {
        await using var context = await TestDatabase.CreateAsync();
        var dao = new WorkoutDao(context.Database);
        var exerciseDao = new ExerciseDao(context.Database);
        var service = CreateService(context);
        var first = new Exercise { Name = "Agachamento", MuscleGroup = "Pernas" };
        var second = new Exercise { Name = "Leg press", MuscleGroup = "Pernas" };
        await exerciseDao.InsertAsync(first);
        await exerciseDao.InsertAsync(second);
        var template = new Workout
        {
            Name = "Pernas A",
            Description = "Treino preparado pela equipe",
            IsTemplate = true
        };
        await dao.SaveAsync(template,
        [
            new WorkoutExercise
            {
                ExerciseId = first.Id,
                PlannedSets = 3,
                PlannedReps = 10,
                PlannedLoad = 20
            },
            new WorkoutExercise
            {
                ExerciseId = second.Id,
                PlannedSets = 4,
                PlannedReps = 12,
                PlannedLoad = 50
            }
        ]);

        var copiedId = await service.CopyTemplateAsync(template.Id, 7);
        var copied = await dao.GetByIdAsync(copiedId);
        var templatePlans = await dao.GetExercisesAsync(template.Id);
        var copiedPlans = await dao.GetExercisesAsync(copiedId);

        Assert.NotEqual(template.Id, copiedId);
        Assert.NotNull(copied);
        Assert.Equal(7, copied.OwnerUserId);
        Assert.False(copied.IsTemplate);
        Assert.Equal(template.Name, copied.Name);
        Assert.Equal(template.Description, copied.Description);
        Assert.Equal(2, Assert.Single(await service.GetPersonalAsync(7)).ExerciseCount);
        Assert.Equal([first.Id, second.Id], copiedPlans.Select(plan => plan.ExerciseId));
        Assert.Equal([0, 1], copiedPlans.Select(plan => plan.OrderIndex));
        Assert.Equal([3, 4], copiedPlans.Select(plan => plan.PlannedSets));
        Assert.Equal([10, 12], copiedPlans.Select(plan => plan.PlannedReps));
        Assert.Equal(new double?[] { 20, 50 }, copiedPlans.Select(plan => plan.PlannedLoad));
        Assert.All(copiedPlans, plan => Assert.DoesNotContain(templatePlans, original => original.Id == plan.Id));

        copiedPlans[0].PlannedSets = 5;
        await dao.SaveAsync(new Workout { Id = copiedId, Name = "Meu treino ajustado" }, copiedPlans);
        await service.DeleteTemplateAsync(template.Id);

        Assert.Equal(5, (await dao.GetExercisesAsync(copiedId))[0].PlannedSets);
        Assert.Equal(3, templatePlans[0].PlannedSets);
        Assert.Equal(7, (await dao.GetByIdAsync(copiedId))!.OwnerUserId);
        Assert.Empty(await service.GetTemplatesAsync());
        Assert.Single(await service.GetPersonalAsync(7));
    }

    [Fact]
    public async Task SaveAsync_EditingThroughExistingEditor_PreservesTemplateClassification()
    {
        await using var context = await TestDatabase.CreateAsync();
        var dao = new WorkoutDao(context.Database);
        var service = CreateService(context);
        var id = await service.CreateTemplateAsync();

        await dao.SaveAsync(new Workout { Id = id, Name = "Treino atualizado" }, []);

        var template = Assert.Single(await service.GetTemplatesAsync());
        Assert.Equal(id, template.Id);
        Assert.Equal("Treino atualizado", template.Name);
        Assert.Null((await dao.GetByIdAsync(id))!.OwnerUserId);
    }

    [Fact]
    public async Task CopyTemplateAsync_RejectsPersonalAndEmptyWorkoutsWithoutCreatingCopies()
    {
        await using var context = await TestDatabase.CreateAsync();
        var service = CreateService(context);
        var personalId = await service.CreatePersonalAsync(1);
        var templateId = await service.CreateTemplateAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CopyTemplateAsync(personalId, 2));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CopyTemplateAsync(templateId, 2));

        Assert.Empty(await service.GetPersonalAsync(2));
        Assert.Equal(2, (await new WorkoutDao(context.Database).GetAllAsync()).Count);
    }

    [Fact]
    public async Task DuplicatePersonalAsync_CopiesCompositionOnlyForOwner()
    {
        await using var context = await TestDatabase.CreateAsync();
        var dao = new WorkoutDao(context.Database);
        var exercises = new ExerciseDao(context.Database);
        var service = CreateService(context);
        var exercise = new Exercise { Name = "Supino reto", MuscleGroup = "Peito" };
        await exercises.InsertAsync(exercise);
        var source = new Workout { Name = "Peito A", OwnerUserId = 7 };
        await dao.SaveAsync(source,
        [
            new WorkoutExercise
            {
                ExerciseId = exercise.Id, PlannedSets = 4, PlannedReps = 8, PlannedLoad = 40
            }
        ]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DuplicatePersonalAsync(source.Id, 8));
        var copyId = await service.DuplicatePersonalAsync(source.Id, 7);
        var copy = await dao.GetByIdAsync(copyId);
        var plan = Assert.Single(await dao.GetExercisesAsync(copyId));

        Assert.NotEqual(source.Id, copyId);
        Assert.Equal("Peito A (cópia)", copy!.Name);
        Assert.Equal(7, copy.OwnerUserId);
        Assert.Equal(exercise.Id, plan.ExerciseId);
        Assert.Equal(4, plan.PlannedSets);
        Assert.Equal(8, plan.PlannedReps);
        Assert.Equal(40, plan.PlannedLoad);
        Assert.Equal(2, (await service.GetPersonalAsync(7)).Count);
        Assert.Empty(await service.GetPersonalAsync(8));
    }

    [Fact]
    public async Task CreateFromStarterAsync_CreatesUsablePersonalPlansWithoutRepeatingCatalogExercises()
    {
        await using var context = await TestDatabase.CreateAsync();
        await new StarterCatalogService(context.Database).EnsureSeededAsync();
        var service = CreateService(context);
        var dao = new WorkoutDao(context.Database);
        var exerciseDao = new ExerciseDao(context.Database);
        var plans = service.GetStarterPlans();
        var catalog = await exerciseDao.GetAllAsync();
        var catalogIds = catalog.Select(exercise => exercise.Id).ToHashSet();

        Assert.Equal(33, catalog.Count);
        Assert.Equal(6, plans.Count);
        Assert.All(plans, plan => Assert.NotEmpty(plan.Exercises));

        foreach (var plan in plans)
        {
            var copiedId = await service.CreateFromStarterAsync(plan.Key, 4);
            var copied = await dao.GetByIdAsync(copiedId);
            var exercises = await dao.GetExercisesAsync(copiedId);

            Assert.Equal(4, copied!.OwnerUserId);
            Assert.False(copied.IsTemplate);
            Assert.Equal(plan.Exercises.Count, exercises.Count);
            Assert.Equal(Enumerable.Range(0, exercises.Count), exercises.Select(item => item.OrderIndex));
            Assert.All(exercises, item =>
            {
                Assert.Contains(item.ExerciseId, catalogIds);
                Assert.True(item.PlannedSets > 0 && item.PlannedReps > 0);
            });
        }

        Assert.Equal(catalog.Count, (await exerciseDao.GetAllAsync()).Count);
        Assert.Equal(6, (await service.GetPersonalAsync(4)).Count);
        Assert.Empty(await service.GetPersonalAsync(5));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateFromStarterAsync("unknown", 4));
    }

    [Fact]
    public async Task DeletePersonalAsync_RemovesUnusedAndArchivesCompletedWithoutLosingHistory()
    {
        await using var context = await TestDatabase.CreateAsync();
        var service = CreateService(context);
        var workouts = new WorkoutDao(context.Database);
        var sessions = new SessionDao(context.Database);
        var unusedId = await service.CreatePersonalAsync(3);
        var usedId = await service.CreatePersonalAsync(3);
        var completed = new WorkoutSession { WorkoutId = usedId, StartedAt = DateTime.UtcNow.AddMinutes(-20) };
        await sessions.InsertAsync(completed);
        completed.FinishedAt = DateTime.UtcNow;
        await sessions.UpdateAsync(completed);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeletePersonalAsync(usedId, 4));
        await service.DeletePersonalAsync(unusedId, 3);
        await service.DeletePersonalAsync(usedId, 3);

        Assert.Null(await workouts.GetByIdAsync(unusedId));
        Assert.True((await workouts.GetByIdAsync(usedId))!.IsArchived);
        Assert.Equal(completed.Id, Assert.Single(await sessions.GetSummariesAsync(usedId)).Id);
        Assert.Empty(await service.GetPersonalAsync(3));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DuplicatePersonalAsync(usedId, 3));
    }

    [Fact]
    public async Task DeletePersonalAsync_RefusesActiveSession()
    {
        await using var context = await TestDatabase.CreateAsync();
        var service = CreateService(context);
        var sessions = new SessionDao(context.Database);
        var id = await service.CreatePersonalAsync(3);
        await sessions.InsertAsync(new WorkoutSession { WorkoutId = id });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeletePersonalAsync(id, 3));
        Assert.False((await new WorkoutDao(context.Database).GetByIdAsync(id))!.IsArchived);
    }

    [Fact]
    public async Task DeleteTemplateAsync_RejectsPersonalWorkout()
    {
        await using var context = await TestDatabase.CreateAsync();
        var service = CreateService(context);
        var id = await service.CreatePersonalAsync(1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteTemplateAsync(id));

        Assert.Equal(id, Assert.Single(await service.GetPersonalAsync(1)).Id);
    }

    [Fact]
    public async Task InitializeAsync_MigratesLegacyWorkoutsOnlyOnce()
    {
        var directory = Path.Combine(Path.GetTempPath(), "GymTrack.CatalogMigration", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "legacy.db3");

        try
        {
            var legacy = new SQLiteAsyncConnection(path, DatabaseConstants.OpenFlags, storeDateTimeAsTicks: false);
            await legacy.ExecuteAsync(
                """
                CREATE TABLE Workout (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Description TEXT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL
                )
                """);
            await legacy.ExecuteAsync(
                "INSERT INTO Workout (Name, CreatedAt, UpdatedAt) VALUES (?, ?, ?)",
                "Treino existente", DateTime.UtcNow, DateTime.UtcNow);
            await legacy.CloseAsync();

            await using (var database = new GymTrackDatabase(path))
            {
                var dao = new WorkoutDao(database);
                var service = new WorkoutCatalogService(database, dao);
                Assert.Equal("Treino existente", Assert.Single(await service.GetTemplatesAsync()).Name);
                await dao.SaveAsync(new Workout { Name = "Rascunho sem proprietário" }, []);
            }

            await using (var database = new GymTrackDatabase(path))
            {
                var service = new WorkoutCatalogService(database, new WorkoutDao(database));
                Assert.Equal("Treino existente", Assert.Single(await service.GetTemplatesAsync()).Name);
                Assert.Equal(1, await service.CountTemplatesAsync());
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static WorkoutCatalogService CreateService(TestDatabase context) =>
        new(context.Database, new WorkoutDao(context.Database));
}
