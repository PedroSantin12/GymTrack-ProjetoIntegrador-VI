namespace GymTrack.Models;

public sealed record StudentConnection(AppUser Employee, bool SharesHistory);

public sealed record StudentDetails(AppUser Student, bool SharesHistory,
    IReadOnlyList<WorkoutCatalogItem> Workouts, IReadOnlyList<StudentActivity> History);

public sealed class StudentActivity
{
    public string WorkoutName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public bool IsManual { get; set; }

    public DateTime LocalDate => IsManual ? Date.Date : Date.ToLocalTime().Date;
}
