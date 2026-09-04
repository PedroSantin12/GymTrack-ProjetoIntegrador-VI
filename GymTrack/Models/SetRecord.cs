using SQLite;

namespace GymTrack.Models;

[Table("SetRecord")]
public sealed class SetRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int SessionId { get; set; }

    public int ExerciseId { get; set; }

    public int SetNumber { get; set; }

    public int Reps { get; set; }

    public double LoadKg { get; set; }
}
