using System.Globalization;
using GymTrack.Models;
using GymTrack.Views.Components;
using Microsoft.Maui.Controls.Shapes;

namespace GymTrack.Views.Consumer;

internal sealed class WorkoutCalendar : ContentView
{
    private static readonly CultureInfo Portuguese = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly string[] Weekdays = ["Seg", "Ter", "Qua", "Qui", "Sex", "Sáb", "Dom"];
    private static readonly DateOnly LastMonth = StartOfMonth(DateOnly.MaxValue);
    private readonly Label _monthTitle = Ui.Text(string.Empty, 16, Ui.Foreground, FontAttributes.Bold);
    private readonly Label _empty = Ui.Text("Seus treinos registrados aparecerão aqui.", 12, Ui.Muted);
    private readonly Label _dayDetails = Ui.Text(string.Empty, 12);
    private readonly ContentView _dayGrid = new();
    private readonly Button _close;
    private readonly Button _previous;
    private readonly Button _next;
    private Dictionary<DateOnly, string[]> _workoutsByDate = [];
    private DateOnly _month = StartOfMonth(DateOnly.FromDateTime(DateTime.Today));

    public WorkoutCalendar(Action dismiss)
    {
        AutomationId = "WorkoutCalendar";
        _close = IconButton("×", "Fechar calendário", dismiss);
        _previous = IconButton("‹", "Mês anterior", () => ChangeMonth(-1));
        _next = IconButton("›", "Próximo mês", () => ChangeMonth(1));

        var heading = new Grid
        {
            ColumnSpacing = 6,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
        };
        var title = Ui.Text("Calendário de treinos", 16, Ui.Foreground, FontAttributes.Bold);
        title.VerticalTextAlignment = TextAlignment.Center;
        SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level2);
        heading.Add(title);
        heading.Add(_close, 1);

        var monthNavigation = new Grid
        {
            ColumnSpacing = 6,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        _monthTitle.HorizontalTextAlignment = TextAlignment.Center;
        _monthTitle.VerticalTextAlignment = TextAlignment.Center;
        monthNavigation.Add(_previous);
        monthNavigation.Add(_monthTitle, 1);
        monthNavigation.Add(_next, 2);

        var legend = new HorizontalStackLayout
        {
            Spacing = 7,
            Children =
            {
                new Ellipse
                {
                    Fill = new SolidColorBrush(Ui.Accent),
                    WidthRequest = 8, HeightRequest = 8,
                    VerticalOptions = LayoutOptions.Center
                },
                Ui.Text("Dia com treino registrado", 11, Ui.Muted)
            }
        };
        var card = Ui.Card(new VerticalStackLayout
        {
            Spacing = 6,
            Children = { heading, monthNavigation, _dayGrid, legend, _empty, _dayDetails }
        }, new Thickness(12, 8, 12, 12));
        card.StrokeThickness = 0;
        Content = card;
        RenderMonth();
    }

    public void FocusCloseButton() => _close.Focus();

    public void SetWorkouts(IEnumerable<SessionSummary> sessions, IEnumerable<ManualWorkoutLog> manualWorkouts)
    {
        _workoutsByDate = sessions
            .Where(session => session.FinishedAt is not null)
            .Select(session => (Date: DateOnly.FromDateTime(session.StartedAt.ToLocalTime()), session.WorkoutName))
            .Concat(manualWorkouts
                .Where(workout => !workout.IsExample)
                .Select(workout => (Date: DateOnly.FromDateTime(workout.PerformedOn), workout.WorkoutName)))
            .GroupBy(workout => workout.Date)
            .ToDictionary(day => day.Key, day => day.Select(workout => workout.WorkoutName).ToArray());
        RenderMonth();
    }

    private void ChangeMonth(int direction)
    {
        if (direction < 0 ? _month == DateOnly.MinValue : _month == LastMonth) return;
        _month = _month.AddMonths(direction);
        RenderMonth();
    }

    private void RenderMonth()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        _monthTitle.Text = Portuguese.TextInfo.ToTitleCase(_month.ToString("MMMM yyyy", Portuguese));
        _previous.IsEnabled = _month > DateOnly.MinValue;
        _next.IsEnabled = _month < LastMonth;
        _empty.IsVisible = _workoutsByDate.Count == 0;
        _dayDetails.IsVisible = false;

        var days = new Grid { ColumnSpacing = 3, RowSpacing = 3 };
        days.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (var column = 0; column < Weekdays.Length; column++)
        {
            days.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            days.Add(Centered(Weekdays[column], 10, Ui.Muted), column);
        }

        var start = ((int)_month.DayOfWeek + 6) % 7;
        var dayCount = DateTime.DaysInMonth(_month.Year, _month.Month);
        var weeks = (start + dayCount + 6) / 7;
        for (var week = 0; week < weeks; week++)
            days.RowDefinitions.Add(new RowDefinition(new GridLength(29)));

        for (var day = 1; day <= dayCount; day++)
        {
            var date = _month.AddDays(day - 1);
            var offset = start + day - 1;
            days.Add(CreateDay(date, today), offset % 7, offset / 7 + 1);
        }
        _dayGrid.Content = days;
    }

    private View CreateDay(DateOnly date, DateOnly today)
    {
        var description = date.ToString("dd 'de' MMMM", Portuguese) +
            (date == today ? ", hoje" : string.Empty);
        if (_workoutsByDate.TryGetValue(date, out var workouts))
            return CreateWorkoutDay(date, description, workouts);

        var color = date == today ? Ui.Accent : date > today ? Ui.Muted : Ui.Foreground;
        var label = Centered(date.Day.ToString(Portuguese), 12, color);
        if (date == today) label.FontAttributes = FontAttributes.Bold;
        SemanticProperties.SetDescription(label, description);
        return label;
    }

    private Button CreateWorkoutDay(DateOnly date, string description, string[] workouts)
    {
        var button = new Button
        {
            Text = date.Day.ToString(Portuguese),
            FontSize = 12,
            WidthRequest = 28,
            HeightRequest = 28,
            MinimumWidthRequest = 28,
            MinimumHeightRequest = 28,
            Padding = 0,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            CornerRadius = 14,
            BackgroundColor = Ui.Accent,
            TextColor = Ui.Background
        };
        var workoutNames = string.Join(Environment.NewLine, workouts);
        ToolTipProperties.SetText(button, workoutNames);
        SemanticProperties.SetDescription(button, $"{description}. {string.Join(", ", workouts)}");
        button.Clicked += (_, _) =>
        {
            _dayDetails.Text = $"{date.ToString("dd/MM", Portuguese)}\n{workoutNames}";
            _dayDetails.IsVisible = true;
        };
        return button;
    }

    private static Button IconButton(string glyph, string description, Action action)
    {
        var button = new Button { Text = glyph, WidthRequest = 44, FontSize = 22 };
        button.SetDynamicResource(VisualElement.StyleProperty, "QuietButton");
        SemanticProperties.SetDescription(button, description);
        button.Clicked += (_, _) => action();
        return button;
    }

    private static DateOnly StartOfMonth(DateOnly date) => new(date.Year, date.Month, 1);

    private static Label Centered(string text, int size, Color color)
    {
        var label = Ui.Text(text, size, color);
        label.HorizontalTextAlignment = TextAlignment.Center;
        label.VerticalTextAlignment = TextAlignment.Center;
        return label;
    }
}
