using GymTrack.Models;

namespace GymTrack.Tests;

public sealed class DatabaseInitializationTests
{
    [Fact]
    public async Task InitializeAsync_CalledConcurrently_CreatesSchemaOnce()
    {
        await using var context = await TestDatabase.CreateAsync();

        await Task.WhenAll(
            Enumerable.Range(0, 8)
                .Select(_ => context.Database.InitializeAsync()));

        var connection = await context.Database.GetConnectionAsync();
        var version = await connection.ExecuteScalarAsync<int>("PRAGMA user_version");

        Assert.Equal(1, version);
    }

    [Fact]
    public async Task InitializeAsync_CreatesFiveTablesAndRequiredIndexes()
    {
        await using var context = await TestDatabase.CreateAsync();
        var connection = await context.Database.GetConnectionAsync();

        var tables = await connection.QueryAsync<SqliteName>(
            """
            SELECT name
            FROM sqlite_master
            WHERE type = 'table' AND name NOT LIKE 'sqlite_%'
            ORDER BY name
            """);
        var indexes = await connection.QueryAsync<SqliteName>(
            "SELECT name FROM sqlite_master WHERE type = 'index'");

        Assert.Equal(
            ["Exercise", "SetRecord", "Workout", "WorkoutExercise", "WorkoutSession"],
            tables.Select(item => item.Name).ToArray());
        Assert.Contains(indexes, item => item.Name == "IX_Exercise_Name");
        Assert.Contains(indexes, item => item.Name == "IX_WorkoutExercise_WorkoutId");
        Assert.Contains(indexes, item => item.Name == "IX_WorkoutSession_WorkoutId");
        Assert.Contains(indexes, item => item.Name == "IX_SetRecord_SessionId");
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>("PRAGMA foreign_keys"));
    }

    [Fact]
    public async Task StoredValues_UseTextNumberAndBooleanRepresentations()
    {
        await using var context = await TestDatabase.CreateAsync();
        var connection = await context.Database.GetConnectionAsync();

        var exercise = new Exercise
        {
            Name = "Remada",
            MuscleGroup = "Costas",
            IsActive = true
        };
        var workout = new Workout
        {
            Name = "Treino A",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await connection.InsertAsync(exercise);
        await connection.InsertAsync(workout);

        var storage = await connection.FindWithQueryAsync<StorageTypes>(
            """
            SELECT
                typeof(e.IsActive) AS BooleanType,
                typeof(w.CreatedAt) AS DateType,
                typeof(e.Name) AS TextType
            FROM Exercise e
            CROSS JOIN Workout w
            LIMIT 1
            """);

        Assert.NotNull(storage);
        Assert.Equal("integer", storage.BooleanType);
        Assert.Equal("text", storage.DateType);
        Assert.Equal("text", storage.TextType);
    }

    private sealed class SqliteName
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class StorageTypes
    {
        public string BooleanType { get; set; } = string.Empty;

        public string DateType { get; set; } = string.Empty;

        public string TextType { get; set; } = string.Empty;
    }
}
