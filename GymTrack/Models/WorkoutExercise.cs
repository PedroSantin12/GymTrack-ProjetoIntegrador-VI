using SQLite;

namespace GymTrack.Models;

[Table("WorkoutExercise")]
public sealed class WorkoutExercise
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int WorkoutId { get; set; }

    public int ExerciseId { get; set; }

    public int OrderIndex { get; set; }

    public int PlannedSets { get; set; }

    public int PlannedReps { get; set; }

    public double? PlannedLoad { get; set; }
}
