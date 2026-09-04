namespace GymTrack.Models;

public sealed class SessionSummary
{
    public int Id { get; set; }

    public int WorkoutId { get; set; }

    public string WorkoutName { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; }

    public DateTime? FinishedAt { get; set; }

    public int SetCount { get; set; }

    public double TotalVolume { get; set; }
}
