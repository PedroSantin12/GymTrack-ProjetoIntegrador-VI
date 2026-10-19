using GymTrack.Views;
using Microsoft.Extensions.DependencyInjection;

namespace GymTrack;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
        UserAppTheme = AppTheme.Dark;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new NavigationPage(_services.GetRequiredService<LoginPage>()))
        {
            Title = "GymTrack"
        };

#if WINDOWS
        window.Width = 420;
        window.Height = 820;
#endif

        return window;
    }
}
