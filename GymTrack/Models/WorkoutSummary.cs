namespace GymTrack.Models;

public sealed class WorkoutSummary
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int ExerciseCount { get; set; }

    public DateTime? LastSessionAt { get; set; }
}
