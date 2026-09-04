using CommunityToolkit.Maui;
using GymTrack.ViewModels;
using GymTrack.Views;
using LiveChartsCore.SkiaSharpView.Maui;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace GymTrack;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseSkiaSharp()
            .UseLiveCharts()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<MainFlyoutPage>();
        builder.Services.AddSingleton<FlyoutMenuPage>();
        builder.Services.AddSingleton<MainTabbedPage>();

        builder.Services.AddSingleton<DashboardPage>();
        builder.Services.AddSingleton<WorkoutsPage>();
        builder.Services.AddSingleton<HistoryPage>();
        builder.Services.AddSingleton<ProgressPage>();
        builder.Services.AddTransient<ExercisesPage>();
        builder.Services.AddTransient<ExerciseEditPage>();
        builder.Services.AddTransient<AboutPage>();

        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<WorkoutsViewModel>();
        builder.Services.AddTransient<HistoryViewModel>();
        builder.Services.AddTransient<ProgressViewModel>();
        builder.Services.AddTransient<ExercisesViewModel>();
        builder.Services.AddTransient<ExerciseEditViewModel>();
        builder.Services.AddTransient<AboutViewModel>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
