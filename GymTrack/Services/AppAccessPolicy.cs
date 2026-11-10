using GymTrack.Models;

namespace GymTrack.Services;

public static class AppAccessPolicy
{
    public static bool CanNavigate(AppUser? user, AppSection section, bool personalMode = false)
    {
        if (user is not { IsActive: true }) return false;
        if (section == AppSection.Profile) return Enum.IsDefined(user.Role);
        return user.Role switch
        {
            UserRole.Employee when !personalMode => section is AppSection.AdminDashboard or AppSection.Students or
                AppSection.Users or AppSection.Exercises or AppSection.AdminWorkouts or AppSection.Statistics,
            UserRole.Consumer or UserRole.Employee => section is AppSection.Templates or
                AppSection.PersonalWorkouts or AppSection.Progress,
            _ => false
        };
    }

    public static bool CanEditWorkout(AppUser? user, Workout? workout, bool personalMode = false) =>
        user is { IsActive: true } && workout is { IsArchived: false } &&
        (user.Role == UserRole.Employee && !personalMode && workout.IsTemplate ||
         UsesPersonalArea(user, personalMode) && !workout.IsTemplate && workout.OwnerUserId == user.Id);

    public static bool CanStartWorkout(AppUser? user, Workout? workout, bool personalMode = false) =>
        user is { IsActive: true } && UsesPersonalArea(user, personalMode) &&
        workout is { IsTemplate: false, IsArchived: false } &&
        workout.OwnerUserId == user.Id;

    private static bool UsesPersonalArea(AppUser user, bool personalMode) =>
        user.Role == UserRole.Consumer || user.Role == UserRole.Employee && personalMode;
}
