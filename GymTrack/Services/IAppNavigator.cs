namespace GymTrack.Services;

public enum AppSection
{
    Templates,
    PersonalWorkouts,
    Progress,
    Profile,
    AdminDashboard,
    Users,
    Exercises,
    AdminWorkouts,
    Statistics
}

public interface IAppNavigator
{
    Task ShowLoginAsync();
    Task OpenWorkspaceAsync();
    Task NavigateAsync(AppSection section);
    Task OpenWorkoutEditorAsync(int? workoutId);
    Task StartWorkoutAsync(int workoutId);
    Task OpenExerciseDemoAsync(int exerciseId);
    Task OpenHistoryAsync();
}
