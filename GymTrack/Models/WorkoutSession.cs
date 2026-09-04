using SQLite;

namespace GymTrack.Models;

[Table("WorkoutSession")]
public sealed class WorkoutSession
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int WorkoutId { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? FinishedAt { get; set; }

    public string? Notes { get; set; }
}
