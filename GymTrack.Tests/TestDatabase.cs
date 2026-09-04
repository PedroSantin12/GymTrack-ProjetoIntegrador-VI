using GymTrack.Data;

namespace GymTrack.Tests;

internal sealed class TestDatabase : IAsyncDisposable
{
    private static readonly string TestRoot = Path.Combine(
        Path.GetTempPath(),
        "GymTrack.Tests");

    private TestDatabase(string directoryPath)
    {
        DirectoryPath = directoryPath;
        DatabasePath = Path.Combine(directoryPath, DatabaseConstants.Filename);
        Database = new GymTrackDatabase(DatabasePath);
    }

    public string DirectoryPath { get; }

    public string DatabasePath { get; }

    public GymTrackDatabase Database { get; }

    public static async Task<TestDatabase> CreateAsync()
    {
        var directory = Path.Combine(TestRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        var context = new TestDatabase(directory);
        await context.Database.InitializeAsync();
        return context;
    }

    public async ValueTask DisposeAsync()
    {
        await Database.DisposeAsync();

        var expectedPrefix = TestRoot + Path.DirectorySeparatorChar;
        if (DirectoryPath.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase) &&
            Directory.Exists(DirectoryPath))
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}
