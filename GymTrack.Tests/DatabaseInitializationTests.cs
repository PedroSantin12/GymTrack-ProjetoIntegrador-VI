using GymTrack.Data;
using GymTrack.Models;
using SQLite;

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

        Assert.Equal(2, version);
    }

    [Fact]
    public async Task InitializeAsync_CreatesEightTablesAndRequiredIndexes()
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
            ["BodyMeasurement", "Exercise", "ManualWorkoutDemoSeed", "ManualWorkoutLog",
                "SetRecord", "Workout", "WorkoutExercise", "WorkoutSession"],
            tables.Select(item => item.Name).ToArray());
        Assert.Contains(indexes, item => item.Name == "IX_Exercise_Name");
        Assert.Contains(indexes, item => item.Name == "IX_WorkoutExercise_WorkoutId");
        Assert.Contains(indexes, item => item.Name == "IX_WorkoutSession_WorkoutId");
        Assert.Contains(indexes, item => item.Name == "IX_SetRecord_SessionId");
        Assert.Contains(indexes, item => item.Name == "IX_ManualWorkoutLog_OwnerDate");
        Assert.Contains(indexes, item => item.Name == "UX_BodyMeasurement_OwnerDate");
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

    [Fact]
    public async Task InitializeAsync_UpgradesExistingWorkoutAndSessionWithoutLosingData()
    {
        var directory = Path.Combine(Path.GetTempPath(), "GymTrack.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, DatabaseConstants.Filename);

        try
        {
            var old = new SQLiteAsyncConnection(path, DatabaseConstants.OpenFlags,
                storeDateTimeAsTicks: false);
            await old.ExecuteAsync(
                "CREATE TABLE Workout (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, Description TEXT NULL, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL)");
            await old.ExecuteAsync(
                "CREATE TABLE WorkoutSession (Id INTEGER PRIMARY KEY AUTOINCREMENT, WorkoutId INTEGER NOT NULL, StartedAt TEXT NOT NULL, FinishedAt TEXT NULL, Notes TEXT NULL)");
            var date = DateTime.Today.AddDays(-3).ToString("O");
            await old.ExecuteAsync(
                "INSERT INTO Workout (Id, Name, CreatedAt, UpdatedAt) VALUES (1, ?, ?, ?)",
                "Treino legado", date, date);
            await old.ExecuteAsync(
                "INSERT INTO WorkoutSession (Id, WorkoutId, StartedAt) VALUES (1, 1, ?)", date);
            await old.ExecuteAsync("PRAGMA user_version = 1");
            await old.CloseAsync();

            await using var migrated = new GymTrackDatabase(path);
            var connection = await migrated.GetConnectionAsync();
            var workout = await connection.FindAsync<Workout>(1);
            Assert.NotNull(workout);
            Assert.Equal("Treino legado", workout.Name);
            Assert.True(workout.IsTemplate);
            Assert.Equal(1, await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM WorkoutSession WHERE Id = 1 AND WorkoutId = 1"));
            Assert.Equal(2, await connection.ExecuteScalarAsync<int>("PRAGMA user_version"));
            Assert.Equal(1, await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'ManualWorkoutLog'"));
            Assert.Equal(1, await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'BodyMeasurement'"));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
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
