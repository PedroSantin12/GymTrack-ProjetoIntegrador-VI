namespace GymTrack.Views;

public partial class MainTabbedPage : TabbedPage
{
    private readonly NavigationPage _dashboardNavigation;
    private readonly NavigationPage _workoutsNavigation;
    private readonly NavigationPage _historyNavigation;
    private readonly NavigationPage _progressNavigation;

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
        _historyNavigation = CreateNavigationPage(historyPage, "Histórico");
        Children.Add(_historyNavigation);
        _progressNavigation = CreateNavigationPage(progressPage, "Evolução");
        Children.Add(_progressNavigation);
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

    public NavigationPage SelectProgress()
    {
        CurrentPage = _progressNavigation;
        return _progressNavigation;
    }

    public NavigationPage SelectHistory()
    {
        CurrentPage = _historyNavigation;
        return _historyNavigation;
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
