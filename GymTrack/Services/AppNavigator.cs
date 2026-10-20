using GymTrack.Data.Dao;
using GymTrack.Models;
using GymTrack.Views;
using GymTrack.Views.Admin;
using GymTrack.Views.Consumer;
using Microsoft.Extensions.DependencyInjection;

namespace GymTrack.Services;

public sealed class AppNavigator(IServiceProvider services, AppSession session,
    IAccountService accounts, IWorkoutDao workouts, IStarterCatalogService starterCatalog) : IAppNavigator
{
    private bool _isNavigating;
    private static Window CurrentWindow => Application.Current?.Windows.FirstOrDefault()
        ?? throw new InvalidOperationException("O aplicativo ainda não possui uma janela.");

    public Task ShowLoginAsync()
    {
        session.SignOut();
        CurrentWindow.Page = new NavigationPage(services.GetRequiredService<LoginPage>());
        return Task.CompletedTask;
    }

    public async Task OpenWorkspaceAsync()
    {
        await starterCatalog.EnsureSeededAsync();
        await NavigateAsync(session.IsEmployee
            ? AppSection.AdminDashboard : AppSection.PersonalWorkouts);
    }

    public async Task NavigateAsync(AppSection section)
    {
        if (_isNavigating) return;
        _isNavigating = true;
        try
        {
            var user = await RequireUserAsync();
            if (!AppAccessPolicy.CanNavigate(user, section))
                throw new UnauthorizedAccessException("Seu perfil não possui acesso a esta área.");
            if (section == AppSection.Exercises)
            {
                await CurrentNavigation.PushAsync(services.GetRequiredService<ExercisesPage>());
                return;
            }
            Page page = section switch
            {
                AppSection.AdminDashboard => services.GetRequiredService<AdminDashboardPage>(),
                AppSection.Users => services.GetRequiredService<UsersPage>(),
                AppSection.AdminWorkouts => services.GetRequiredService<TemplateManagementPage>(),
                AppSection.Statistics => services.GetRequiredService<StatisticsPage>(),
                AppSection.Templates => services.GetRequiredService<TemplateLibraryPage>(),
                AppSection.PersonalWorkouts => services.GetRequiredService<PersonalWorkoutsPage>(),
                AppSection.Progress => services.GetRequiredService<ConsumerProgressPage>(),
                AppSection.Profile => services.GetRequiredService<ProfilePage>(),
                _ => throw new ArgumentOutOfRangeException(nameof(section))
            };
            CurrentWindow.Page = new NavigationPage(page);
        }
        finally { _isNavigating = false; }
    }

    public async Task OpenWorkoutEditorAsync(int? workoutId)
    {
        if (_isNavigating) return;
        _isNavigating = true;
        try
        {
            var user = await RequireUserAsync();
            var workout = workoutId.HasValue ? await workouts.GetByIdAsync(workoutId.Value) : null;
            if (workoutId.HasValue && !AppAccessPolicy.CanEditWorkout(user, workout))
                throw new UnauthorizedAccessException("Este treino não está disponível para edição.");
            var page = services.GetRequiredService<WorkoutEditPage>();
            if (workoutId.HasValue)
                await page.InitializeAsync(workoutId);
            else
                await page.InitializeNewAsync(user.Role == UserRole.Consumer ? user.Id : null,
                    user.Role == UserRole.Employee);
            await CurrentNavigation.PushAsync(page);
        }
        finally { _isNavigating = false; }
    }

    public async Task StartWorkoutAsync(int workoutId)
    {
        if (_isNavigating) return;
        _isNavigating = true;
        try
        {
            var user = await RequireUserAsync();
            if (!AppAccessPolicy.CanStartWorkout(user, await workouts.GetByIdAsync(workoutId)))
                throw new UnauthorizedAccessException("Este treino não pertence à sua conta.");
            var page = services.GetRequiredService<WorkoutSessionPage>();
            if (await page.InitializeAsync(workoutId)) await CurrentNavigation.Navigation.PushModalAsync(page);
        }
        finally { _isNavigating = false; }
    }

    public async Task OpenExerciseDemoAsync(int exerciseId)
    {
        if (_isNavigating) return;
        _isNavigating = true;
        try
        {
            await RequireUserAsync();
            var page = services.GetRequiredService<ExerciseDemoPage>();
            await page.InitializeAsync(exerciseId);
            var modal = CurrentNavigation.Navigation.ModalStack.LastOrDefault();
            if (modal is NavigationPage modalNavigation)
            {
                await modalNavigation.PushAsync(page);
            }
            else if (modal is not null)
            {
                var close = new ToolbarItem { Text = "Fechar" };
                close.Clicked += async (_, _) => await page.Navigation.PopModalAsync();
                page.ToolbarItems.Add(close);
                await CurrentNavigation.Navigation.PushModalAsync(new NavigationPage(page));
            }
            else
            {
                await CurrentNavigation.PushAsync(page);
            }
        }
        finally { _isNavigating = false; }
    }

    public async Task OpenHistoryAsync()
    {
        var user = await RequireUserAsync();
        if (user.Role != UserRole.Consumer) throw new UnauthorizedAccessException();
        await CurrentNavigation.PushAsync(services.GetRequiredService<HistoryPage>());
    }

    private NavigationPage CurrentNavigation => CurrentWindow.Page as NavigationPage
        ?? throw new InvalidOperationException("A navegação ainda não foi iniciada.");

    private async Task<AppUser> RequireUserAsync()
    {
        var current = session.CurrentUser;
        var account = current is null ? null : (await accounts.GetUsersAsync())
            .FirstOrDefault(user => user.Id == current.Id && user.IsActive);
        if (account is null)
        {
            await ShowLoginAsync();
            throw new UnauthorizedAccessException("Entre novamente para continuar.");
        }
        session.SignIn(account);
        return account;
    }
}
