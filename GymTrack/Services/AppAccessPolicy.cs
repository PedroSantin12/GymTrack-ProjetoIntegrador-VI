using GymTrack.Models;

namespace GymTrack.Services;

public static class AppAccessPolicy
{
    public static bool CanNavigate(AppUser? user, AppSection section)
    {
        if (user is not { IsActive: true }) return false;
        if (section == AppSection.Profile) return Enum.IsDefined(user.Role);
        return user.Role switch
        {
            UserRole.Employee => section is AppSection.AdminDashboard or AppSection.Users or
                AppSection.Exercises or AppSection.AdminWorkouts or AppSection.Statistics,
            UserRole.Consumer => section is AppSection.Templates or
                AppSection.PersonalWorkouts or AppSection.Progress,
            _ => false
        };
    }

    public static bool CanEditWorkout(AppUser? user, Workout? workout) =>
        user is { IsActive: true } && workout is { IsArchived: false } &&
        (user.Role == UserRole.Employee && workout.IsTemplate ||
         user.Role == UserRole.Consumer && !workout.IsTemplate && workout.OwnerUserId == user.Id);

    public static bool CanStartWorkout(AppUser? user, Workout? workout) =>
        user is { IsActive: true, Role: UserRole.Consumer } &&
        workout is { IsTemplate: false, IsArchived: false } &&
        workout.OwnerUserId == user.Id;
}
