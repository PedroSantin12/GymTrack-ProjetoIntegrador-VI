namespace GymTrack.Views;

public partial class MainTabbedPage : TabbedPage
{
    private readonly NavigationPage _dashboardNavigation;
    private readonly NavigationPage _workoutsNavigation;

    public MainTabbedPage(
        DashboardPage dashboardPage,
        WorkoutsPage workoutsPage,
        HistoryPage historyPage,
        ProgressPage progressPage)
    {
        InitializeComponent();

        _dashboardNavigation = CreateNavigationPage(dashboardPage, "Início");
        _workoutsNavigation = CreateNavigationPage(workoutsPage, "Treinos");

        Children.Add(_dashboardNavigation);
        Children.Add(_workoutsNavigation);
        Children.Add(CreateNavigationPage(historyPage, "Histórico"));
        Children.Add(CreateNavigationPage(progressPage, "Evolução"));
    }

    public NavigationPage CurrentNavigation =>
        CurrentPage as NavigationPage ?? _dashboardNavigation;

    public NavigationPage SelectDashboard()
    {
        CurrentPage = _dashboardNavigation;
        return _dashboardNavigation;
    }

    public NavigationPage SelectWorkouts()
    {
        CurrentPage = _workoutsNavigation;
        return _workoutsNavigation;
    }

    private static NavigationPage CreateNavigationPage(Page rootPage, string title)
    {
        rootPage.Title = title;
        return new NavigationPage(rootPage)
        {
            Title = title
        };
    }
}
