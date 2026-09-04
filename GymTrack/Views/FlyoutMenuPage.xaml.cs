namespace GymTrack.Views;

public partial class FlyoutMenuPage : ContentPage
{
    public FlyoutMenuPage()
    {
        InitializeComponent();
    }

    public event EventHandler? HomeRequested;
    public event EventHandler? WorkoutsRequested;
    public event EventHandler? ExercisesRequested;
    public event EventHandler? AboutRequested;

    private void OnHomeClicked(object? sender, EventArgs e)
    {
        HomeRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnWorkoutsClicked(object? sender, EventArgs e)
    {
        WorkoutsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnExercisesClicked(object? sender, EventArgs e)
    {
        ExercisesRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnAboutClicked(object? sender, EventArgs e)
    {
        AboutRequested?.Invoke(this, EventArgs.Empty);
    }
}
