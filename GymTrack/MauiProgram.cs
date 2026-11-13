using CommunityToolkit.Maui;
using GymTrack.Data;
using GymTrack.Data.Dao;
using GymTrack.Services;
using GymTrack.ViewModels;
using GymTrack.Views;
using GymTrack.Views.Admin;
using GymTrack.Views.Consumer;
using Microsoft.Extensions.Logging;

namespace GymTrack;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<AppSession>();
        builder.Services.AddSingleton<IAccountService>(_ =>
            new AccountService(Path.Combine(FileSystem.AppDataDirectory, "accounts.db3")));
        builder.Services.AddSingleton<IAppNavigator, AppNavigator>();
        builder.Services.AddSingleton<IWorkoutCatalogService, WorkoutCatalogService>();
        builder.Services.AddSingleton<StudentService>();
        builder.Services.AddSingleton<IStarterCatalogService, StarterCatalogService>();
        builder.Services.AddSingleton<IManualWorkoutLogService, ManualWorkoutLogService>();
        builder.Services.AddSingleton<IBodyMeasurementService, BodyMeasurementService>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterViewModel>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<AdminDashboardPage>();
        builder.Services.AddTransient<UsersPage>();
        builder.Services.AddTransient<StudentsPage>();
        builder.Services.AddTransient<TemplateManagementPage>();
        builder.Services.AddTransient<StatisticsPage>();
        builder.Services.AddTransient<TemplateLibraryPage>();
        builder.Services.AddTransient<TemplateDetailPage>();
        builder.Services.AddTransient<PersonalWorkoutsPage>();
        builder.Services.AddTransient<ConsumerProgressPage>();
        builder.Services.AddTransient<ProfilePage>();

        builder.Services.AddSingleton<HistoryPage>();
        builder.Services.AddTransient<ExercisesPage>();
        builder.Services.AddTransient<ExerciseEditPage>();
        builder.Services.AddTransient<WorkoutEditPage>();
        builder.Services.AddTransient<ExercisePickerPage>();
        builder.Services.AddTransient<ExerciseDemoPage>();
        builder.Services.AddTransient<WorkoutSessionPage>();
        builder.Services.AddTransient<HistoryDetailPage>();
        builder.Services.AddTransient<AboutPage>();

        builder.Services.AddTransient<HistoryViewModel>();
        builder.Services.AddTransient<ExercisesViewModel>();
        builder.Services.AddTransient<ExerciseEditViewModel>();
        builder.Services.AddTransient<WorkoutEditViewModel>();
        builder.Services.AddTransient<ExercisePickerViewModel>();
        builder.Services.AddTransient<WorkoutSessionViewModel>();
        builder.Services.AddTransient<HistoryDetailViewModel>();
        builder.Services.AddTransient<AboutViewModel>();

        builder.Services.AddSingleton<GymTrackDatabase>();
        builder.Services.AddSingleton<IExerciseDao, ExerciseDao>();
        builder.Services.AddSingleton<IWorkoutDao, WorkoutDao>();
        builder.Services.AddSingleton<ISessionDao>(provider =>
        {
            var session = provider.GetRequiredService<AppSession>();
            return new UserSessionDao(
                new SessionDao(provider.GetRequiredService<GymTrackDatabase>(), session),
                provider.GetRequiredService<IWorkoutDao>(), session);
        });
        builder.Services.AddSingleton<INotificationService, NotificationService>();
        builder.Services.AddSingleton<IDialogService, DialogService>();
        builder.Services.AddSingleton(TimeProvider.System);

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
