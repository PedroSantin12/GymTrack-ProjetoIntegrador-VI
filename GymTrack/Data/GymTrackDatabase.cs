using SQLite;

namespace GymTrack.Data;

public sealed class GymTrackDatabase : IAsyncDisposable
{
    private readonly SQLiteAsyncConnection _connection;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private volatile bool _isInitialized;

    public GymTrackDatabase()
        : this(DatabaseConstants.Path)
    {
    }

    public GymTrackDatabase(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var directory = System.IO.Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connection = new SQLiteAsyncConnection(
            databasePath,
            DatabaseConstants.OpenFlags,
            storeDateTimeAsTicks: false);
    }

    public string DatabasePath => _connection.DatabasePath;

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        await _initializationLock.WaitAsync();
        try
        {
            if (_isInitialized)
            {
                return;
            }

            await _connection.ExecuteAsync("PRAGMA foreign_keys = ON");
            await _connection.ExecuteAsync(CreateExerciseTableSql);
            await _connection.ExecuteAsync(CreateWorkoutTableSql);
            await MigrateWorkoutOwnershipAsync();
            await _connection.ExecuteAsync(CreateWorkoutExerciseTableSql);
            await _connection.ExecuteAsync(CreateWorkoutSessionTableSql);
            await _connection.ExecuteAsync(CreateSetRecordTableSql);
            await _connection.ExecuteAsync(CreateManualWorkoutLogTableSql);
            await _connection.ExecuteAsync(CreateManualWorkoutDemoSeedTableSql);
            await _connection.ExecuteAsync(CreateBodyMeasurementTableSql);

            await CreateIndexesAsync();
            await _connection.ExecuteAsync("PRAGMA user_version = 2");

            _isInitialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        await InitializeAsync();
        return _connection;
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.CloseAsync();
        _initializationLock.Dispose();
    }

    private async Task CreateIndexesAsync()
    {
        await _connection.ExecuteAsync(
            "CREATE INDEX IF NOT EXISTS IX_Workout_OwnerUserId ON Workout (OwnerUserId, IsTemplate)");
        await _connection.ExecuteAsync(
            "CREATE INDEX IF NOT EXISTS IX_Exercise_Name ON Exercise (Name COLLATE NOCASE)");
        await _connection.ExecuteAsync(
            "CREATE INDEX IF NOT EXISTS IX_WorkoutExercise_WorkoutId ON WorkoutExercise (WorkoutId)");
        await _connection.ExecuteAsync(
            "CREATE INDEX IF NOT EXISTS IX_WorkoutExercise_ExerciseId ON WorkoutExercise (ExerciseId)");
        await _connection.ExecuteAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS UX_WorkoutExercise_Order ON WorkoutExercise (WorkoutId, OrderIndex)");
        await _connection.ExecuteAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS UX_WorkoutExercise_Exercise ON WorkoutExercise (WorkoutId, ExerciseId)");
        await _connection.ExecuteAsync(
            "CREATE INDEX IF NOT EXISTS IX_WorkoutSession_WorkoutId ON WorkoutSession (WorkoutId)");
        await _connection.ExecuteAsync(
            "CREATE INDEX IF NOT EXISTS IX_SetRecord_SessionId ON SetRecord (SessionId)");
        await _connection.ExecuteAsync(
            "CREATE INDEX IF NOT EXISTS IX_SetRecord_ExerciseId ON SetRecord (ExerciseId)");
        await _connection.ExecuteAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS UX_SetRecord_SetNumber ON SetRecord (SessionId, ExerciseId, SetNumber)");
        await _connection.ExecuteAsync(
            "CREATE INDEX IF NOT EXISTS IX_ManualWorkoutLog_OwnerDate ON ManualWorkoutLog (OwnerUserId, PerformedOn DESC)");
        await _connection.ExecuteAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS UX_BodyMeasurement_OwnerDate ON BodyMeasurement (OwnerUserId, MeasuredOn)");
    }

    private Task MigrateWorkoutOwnershipAsync()
    {
        return _connection.RunInTransactionAsync(transaction =>
        {
            var columns = transaction.GetTableInfo("Workout");
            if (!columns.Any(column => column.Name == nameof(Models.Workout.OwnerUserId)))
            {
                transaction.Execute("ALTER TABLE Workout ADD COLUMN OwnerUserId INTEGER NULL");
            }

            if (!columns.Any(column => column.Name == nameof(Models.Workout.IsTemplate)))
            {
                transaction.Execute("ALTER TABLE Workout ADD COLUMN IsTemplate INTEGER NOT NULL DEFAULT 0");
                transaction.Execute("UPDATE Workout SET IsTemplate = 1 WHERE OwnerUserId IS NULL");
            }

            if (!columns.Any(column => column.Name == nameof(Models.Workout.IsArchived)))
            {
                transaction.Execute("ALTER TABLE Workout ADD COLUMN IsArchived INTEGER NOT NULL DEFAULT 0");
            }
        });
    }

    private const string CreateExerciseTableSql =
        """
        CREATE TABLE IF NOT EXISTS Exercise (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            MuscleGroup TEXT NOT NULL,
            Notes TEXT NULL,
            IsActive INTEGER NOT NULL DEFAULT 1 CHECK (IsActive IN (0, 1))
        )
        """;

    private const string CreateWorkoutTableSql =
        """
        CREATE TABLE IF NOT EXISTS Workout (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            Description TEXT NULL,
            OwnerUserId INTEGER NULL,
            IsTemplate INTEGER NOT NULL DEFAULT 0 CHECK (IsTemplate IN (0, 1)),
            IsArchived INTEGER NOT NULL DEFAULT 0 CHECK (IsArchived IN (0, 1)),
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL
        )
        """;

    private const string CreateWorkoutExerciseTableSql =
        """
        CREATE TABLE IF NOT EXISTS WorkoutExercise (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            WorkoutId INTEGER NOT NULL,
            ExerciseId INTEGER NOT NULL,
            OrderIndex INTEGER NOT NULL,
            PlannedSets INTEGER NOT NULL CHECK (PlannedSets > 0),
            PlannedReps INTEGER NOT NULL CHECK (PlannedReps > 0),
            PlannedLoad REAL NULL CHECK (PlannedLoad IS NULL OR PlannedLoad >= 0),
            FOREIGN KEY (WorkoutId) REFERENCES Workout (Id) ON DELETE CASCADE,
            FOREIGN KEY (ExerciseId) REFERENCES Exercise (Id) ON DELETE RESTRICT
        )
        """;

    private const string CreateWorkoutSessionTableSql =
        """
        CREATE TABLE IF NOT EXISTS WorkoutSession (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            WorkoutId INTEGER NOT NULL,
            StartedAt TEXT NOT NULL,
            FinishedAt TEXT NULL,
            Notes TEXT NULL,
            FOREIGN KEY (WorkoutId) REFERENCES Workout (Id) ON DELETE RESTRICT
        )
        """;

    private const string CreateSetRecordTableSql =
        """
        CREATE TABLE IF NOT EXISTS SetRecord (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            SessionId INTEGER NOT NULL,
            ExerciseId INTEGER NOT NULL,
            SetNumber INTEGER NOT NULL CHECK (SetNumber > 0),
            Reps INTEGER NOT NULL CHECK (Reps > 0),
            LoadKg REAL NOT NULL CHECK (LoadKg >= 0),
            FOREIGN KEY (SessionId) REFERENCES WorkoutSession (Id) ON DELETE CASCADE,
            FOREIGN KEY (ExerciseId) REFERENCES Exercise (Id) ON DELETE RESTRICT
        )
        """;

    private const string CreateManualWorkoutLogTableSql =
        """
        CREATE TABLE IF NOT EXISTS ManualWorkoutLog (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            OwnerUserId INTEGER NOT NULL CHECK (OwnerUserId > 0),
            WorkoutName TEXT NOT NULL,
            PerformedOn TEXT NOT NULL,
            Notes TEXT NULL,
            IsExample INTEGER NOT NULL DEFAULT 0 CHECK (IsExample IN (0, 1))
        )
        """;

    private const string CreateManualWorkoutDemoSeedTableSql =
        """
        CREATE TABLE IF NOT EXISTS ManualWorkoutDemoSeed (
            OwnerUserId INTEGER PRIMARY KEY CHECK (OwnerUserId > 0)
        )
        """;

    private const string CreateBodyMeasurementTableSql =
        """
        CREATE TABLE IF NOT EXISTS BodyMeasurement (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            OwnerUserId INTEGER NOT NULL CHECK (OwnerUserId > 0),
            MeasuredOn TEXT NOT NULL,
            WeightKg REAL NOT NULL CHECK (WeightKg > 0),
            HeightCm REAL NULL CHECK (HeightCm IS NULL OR HeightCm > 0),
            WaistCm REAL NULL CHECK (WaistCm IS NULL OR WaistCm > 0),
            HipCm REAL NULL CHECK (HipCm IS NULL OR HipCm > 0),
            ChestCm REAL NULL CHECK (ChestCm IS NULL OR ChestCm > 0)
        )
        """;
}
