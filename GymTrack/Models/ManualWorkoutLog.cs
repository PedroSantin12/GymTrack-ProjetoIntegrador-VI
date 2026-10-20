using SQLite;

namespace GymTrack.Models;

[Table("ManualWorkoutLog")]
public sealed class ManualWorkoutLog
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int OwnerUserId { get; set; }

    public string WorkoutName { get; set; } = string.Empty;

    public DateTime PerformedOn { get; set; }

    public string? Notes { get; set; }

    public bool IsExample { get; set; }
}
