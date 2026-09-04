using Microsoft.Extensions.DependencyInjection;

namespace GymTrack.Views;

public partial class MainFlyoutPage : FlyoutPage
{
    private readonly MainTabbedPage _tabs;
    private readonly IServiceProvider _services;

    public MainFlyoutPage(
        FlyoutMenuPage menu,
        MainTabbedPage tabs,
        IServiceProvider services)
    {
        InitializeComponent();

        _tabs = tabs;
        _services = services;
        Flyout = menu;
        Detail = tabs;

        menu.HomeRequested += OnHomeRequested;
        menu.WorkoutsRequested += OnWorkoutsRequested;
        menu.ExercisesRequested += OnExercisesRequested;
        menu.AboutRequested += OnAboutRequested;
    }

    private void OnHomeRequested(object? sender, EventArgs e)
    {
        _tabs.SelectDashboard();
        IsPresented = false;
    }

    private void OnWorkoutsRequested(object? sender, EventArgs e)
    {
        _tabs.SelectWorkouts();
        IsPresented = false;
    }

    private async void OnExercisesRequested(object? sender, EventArgs e)
    {
        IsPresented = false;
        var navigation = _tabs.SelectWorkouts();

        if (navigation.CurrentPage is not ExercisesPage)
        {
            await navigation.PushAsync(_services.GetRequiredService<ExercisesPage>());
        }
    }

    private async void OnAboutRequested(object? sender, EventArgs e)
    {
        IsPresented = false;
        var navigation = _tabs.CurrentNavigation;

        if (navigation.CurrentPage is not AboutPage)
        {
            await navigation.PushAsync(_services.GetRequiredService<AboutPage>());
        }
    }
}
