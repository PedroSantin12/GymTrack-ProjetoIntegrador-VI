using SQLite;

namespace GymTrack.Data;

public static class DatabaseConstants
{
    public const string Filename = "gymtrack.db3";

    public const SQLiteOpenFlags OpenFlags =
        SQLiteOpenFlags.ReadWrite |
        SQLiteOpenFlags.Create |
        SQLiteOpenFlags.SharedCache |
        SQLiteOpenFlags.FullMutex;

    public static string Path => System.IO.Path.Combine(FileSystem.AppDataDirectory, Filename);
}
