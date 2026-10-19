using GymTrack.Models;
using GymTrack.Services;

namespace GymTrack.Tests;

public sealed class AppAccessPolicyTests
{
    [Fact]
    public void CanNavigate_AnonymousInactiveAndUnknownRoleCannotOpenAnySection()
    {
        foreach (var section in Enum.GetValues<AppSection>())
        {
            Assert.False(AppAccessPolicy.CanNavigate(null, section));
            Assert.False(AppAccessPolicy.CanNavigate(
                new AppUser { Id = 1, Role = UserRole.Consumer, IsActive = false }, section));
            Assert.False(AppAccessPolicy.CanNavigate(
                new AppUser { Id = 2, Role = UserRole.Employee, IsActive = false }, section));
            Assert.False(AppAccessPolicy.CanNavigate(new AppUser { Id = 3, Role = (UserRole)99 }, section));
        }
    }

    [Theory]
    [InlineData(AppSection.AdminDashboard)]
    [InlineData(AppSection.Users)]
    [InlineData(AppSection.Exercises)]
    [InlineData(AppSection.AdminWorkouts)]
    [InlineData(AppSection.Statistics)]
    public void CanNavigate_AdministrativeSectionsRequireEmployee(AppSection section)
    {
        Assert.False(AppAccessPolicy.CanNavigate(new AppUser { Role = UserRole.Consumer }, section));
        Assert.True(AppAccessPolicy.CanNavigate(new AppUser { Role = UserRole.Employee }, section));
    }

    [Theory]
    [InlineData(AppSection.Templates)]
    [InlineData(AppSection.PersonalWorkouts)]
    [InlineData(AppSection.Progress)]
    public void CanNavigate_ConsumerSectionsRequireConsumer(AppSection section)
    {
        Assert.True(AppAccessPolicy.CanNavigate(new AppUser { Role = UserRole.Consumer }, section));
        Assert.False(AppAccessPolicy.CanNavigate(new AppUser { Role = UserRole.Employee }, section));
    }

    [Theory]
    [InlineData(UserRole.Consumer)]
    [InlineData(UserRole.Employee)]
    public void CanNavigate_ProfileIsSharedByBothActiveRoles(UserRole role)
    {
        Assert.True(AppAccessPolicy.CanNavigate(new AppUser { Role = role }, AppSection.Profile));
    }

    [Theory]
    [InlineData(UserRole.Consumer, 7, false, true)]
    [InlineData(UserRole.Consumer, 8, false, false)]
    [InlineData(UserRole.Consumer, null, false, false)]
    [InlineData(UserRole.Consumer, 7, true, false)]
    [InlineData(UserRole.Employee, null, true, true)]
    [InlineData(UserRole.Employee, 7, true, true)]
    [InlineData(UserRole.Employee, 7, false, false)]
    [InlineData(UserRole.Employee, 8, false, false)]
    public void CanEditWorkout_ConsumersEditOwnWorkoutsAndEmployeesEditTemplates(
        UserRole role, int? ownerUserId, bool isTemplate, bool expected)
    {
        var user = new AppUser { Id = 7, Role = role };
        var workout = new Workout { Id = 1, OwnerUserId = ownerUserId, IsTemplate = isTemplate };

        Assert.Equal(expected, AppAccessPolicy.CanEditWorkout(user, workout));
    }

    [Theory]
    [InlineData(UserRole.Consumer, 7, false, true)]
    [InlineData(UserRole.Consumer, 8, false, false)]
    [InlineData(UserRole.Consumer, null, false, false)]
    [InlineData(UserRole.Consumer, 7, true, false)]
    [InlineData(UserRole.Employee, 7, false, false)]
    [InlineData(UserRole.Employee, null, true, false)]
    public void CanStartWorkout_OnlyConsumerCanStartOwnPersonalWorkout(
        UserRole role, int? ownerUserId, bool isTemplate, bool expected)
    {
        var user = new AppUser { Id = 7, Role = role };
        var workout = new Workout { Id = 1, OwnerUserId = ownerUserId, IsTemplate = isTemplate };

        Assert.Equal(expected, AppAccessPolicy.CanStartWorkout(user, workout));
    }

    [Fact]
    public void WorkoutActions_RejectAnonymousInactiveAndMissingWorkout()
    {
        var user = new AppUser { Id = 7, Role = UserRole.Consumer };
        var workout = new Workout { Id = 1, OwnerUserId = user.Id };

        Assert.False(AppAccessPolicy.CanEditWorkout(null, workout));
        Assert.False(AppAccessPolicy.CanStartWorkout(null, workout));
        Assert.False(AppAccessPolicy.CanEditWorkout(user, null));
        Assert.False(AppAccessPolicy.CanStartWorkout(user, null));

        user.IsActive = false;

        Assert.False(AppAccessPolicy.CanEditWorkout(user, workout));
        Assert.False(AppAccessPolicy.CanStartWorkout(user, workout));
        Assert.False(AppAccessPolicy.CanEditWorkout(
            new AppUser { Id = 1, Role = UserRole.Employee, IsActive = false },
            new Workout { IsTemplate = true }));
    }
}
