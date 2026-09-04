namespace GymTrack.Models;

public sealed class SessionSetDetail
{
    public int ExerciseId { get; set; }

    public string ExerciseName { get; set; } = string.Empty;

    public string MuscleGroup { get; set; } = string.Empty;

    public int ExerciseOrder { get; set; }

    public int SetNumber { get; set; }

    public int Reps { get; set; }

    public double LoadKg { get; set; }
}
