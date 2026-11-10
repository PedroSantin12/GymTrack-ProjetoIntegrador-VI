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
    Statistics,
    Students
}

public interface IAppNavigator
{
    Task ShowLoginAsync();
    Task OpenWorkspaceAsync();
    Task SwitchWorkspaceAsync();
    Task NavigateAsync(AppSection section);
    Task OpenWorkoutEditorAsync(int? workoutId);
    Task StartWorkoutAsync(int workoutId);
    Task OpenExerciseDemoAsync(int exerciseId);
    Task OpenHistoryAsync();
}
