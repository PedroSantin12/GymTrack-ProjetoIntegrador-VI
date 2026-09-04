using SQLite;

namespace GymTrack.Models;

[Table("Exercise")]
public sealed class Exercise
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull]
    public string Name { get; set; } = string.Empty;

    [NotNull]
    public string MuscleGroup { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;
}
