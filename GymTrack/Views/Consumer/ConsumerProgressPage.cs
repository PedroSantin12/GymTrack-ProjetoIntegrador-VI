using System.Globalization;
using GymTrack.Data.Dao;
using GymTrack.Models;
using GymTrack.Services;
using GymTrack.Views.Components;

namespace GymTrack.Views.Consumer;

public sealed class ConsumerProgressPage : WorkspacePage
{
    private readonly IAppNavigator _navigator;
    private readonly AppSession _session;
    private readonly ISessionDao _sessions;
    private readonly VerticalStackLayout _content = new() { Spacing = 20 };
    private readonly Label _status = ConsumerUi.Status();
    private bool _loading;

    public ConsumerProgressPage(IAppNavigator navigator, AppSession session,
        ISessionDao sessions)
        : base("Sua evolução", "Acompanhe seu progresso.", AppSection.Progress, navigator, session)
    {
        _navigator = navigator;
        _session = session;
        _sessions = sessions;
        SetBody(new VerticalStackLayout { Spacing = 10, Children = { _status, _content } });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loading) return;
        _loading = true;
        try
        {
            if (_session.CurrentUser is not { } user) return;
            _status.Text = string.Empty;
            var sessions = (await _sessions.GetSummariesAsync())
                .Where(item => item.FinishedAt is not null)
                .OrderByDescending(item => item.StartedAt).ToList();
            _content.Clear();
            if (sessions.Count == 0)
            {
                _content.Add(ConsumerUi.Empty("Seu progresso começa no primeiro treino.",
                    "Conclua uma sessão para começar a acompanhar sua evolução."));
                _content.Add(Ui.Button("Escolher treino",
                    () => _navigator.NavigateAsync(AppSection.PersonalWorkouts)));
            }
            else await AddProgressAsync(sessions);
        }
        catch { _status.Text = "Não foi possível carregar sua evolução. Volte a esta página para tentar novamente."; }
        finally { _loading = false; }
    }

    private async Task AddProgressAsync(IReadOnlyList<SessionSummary> sessions)
    {
        var minutes = (int)Math.Round(sessions.Sum(item =>
            Math.Max(0, (item.FinishedAt!.Value - item.StartedAt).TotalMinutes)));
        var volume = sessions.Sum(item => item.TotalVolume);
        var monthStart = DateTime.Today.AddDays(-27);
        var frequency = sessions.Count(item => item.StartedAt.ToLocalTime().Date >= monthStart) / 4d;
        _content.Add(Ui.Text("Resumo de desempenho", 18, Ui.Foreground, FontAttributes.Bold));
        var metrics = new Grid
        {
            RowSpacing = 9, ColumnSpacing = 9,
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }
        };
        metrics.Add(ConsumerUi.Metric(sessions.Count.ToString(CultureInfo.CurrentCulture), "Treinos concluídos"), 0, 0);
        metrics.Add(ConsumerUi.Metric($"{volume:N0} kg", "Volume levantado"), 1, 0);
        metrics.Add(ConsumerUi.Metric(minutes >= 60 ? $"{minutes / 60}h {minutes % 60:00}" : $"{minutes} min",
            "Tempo total"), 0, 1);
        metrics.Add(ConsumerUi.Metric($"{frequency:0.#}", "Treinos por semana · 4 sem"), 1, 1);
        _content.Add(metrics);

        _content.Add(ConsumerUi.Section("Frequência de treinos", "Últimos 7 dias"));
        var firstDay = DateTime.Today.AddDays(-6);
        _content.Add(BarChart(Enumerable.Range(0, 7)
            .Select(index => firstDay.AddDays(index))
            .Select(day => (day.ToString("ddd", CultureInfo.GetCultureInfo("pt-BR")).TrimEnd('.'),
                (double)sessions.Count(item => item.StartedAt.ToLocalTime().Date == day))).ToList(), "treinos"));

        _content.Add(ConsumerUi.Section("Volume de treino", "Últimas 6 semanas · kg"));
        var startOfWeek = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));
        var weeks = Enumerable.Range(0, 6).Select(index => startOfWeek.AddDays((index - 5) * 7)).ToList();
        _content.Add(BarChart(weeks.Select(day => (day.ToString("dd/MM"),
            sessions.Where(item => item.StartedAt.ToLocalTime().Date >= day &&
                item.StartedAt.ToLocalTime().Date < day.AddDays(7)).Sum(item => item.TotalVolume))).ToList(), "kg"));

        var series = new Dictionary<int, ExerciseSeries>();
        foreach (var session in sessions.Take(40))
        {
            var details = await _sessions.GetSetDetailsAsync(session.Id);
            foreach (var group in details.GroupBy(item => item.ExerciseId))
            {
                var heaviest = group.Max(item => item.LoadKg);
                if (!series.TryGetValue(group.Key, out var exercise))
                {
                    exercise = new ExerciseSeries(group.First().ExerciseName);
                    series.Add(group.Key, exercise);
                }
                exercise.Points.Add((session.StartedAt.ToLocalTime().ToString("dd/MM"), heaviest));
            }
        }
        if (series.Count > 0)
        {
            _content.Add(ConsumerUi.Section("Evolução de carga", "Maior carga por sessão · kg"));
            var picker = new Picker { Title = "Exercício", ItemsSource = series.Values.OrderBy(item => item.Name).ToList() };
            var host = new ContentView();
            picker.SelectedIndexChanged += (_, _) =>
            {
                if (picker.SelectedItem is ExerciseSeries selected)
                    host.Content = BarChart(selected.Points.AsEnumerable().Reverse().TakeLast(6).ToList(), "kg");
            };
            _content.Add(picker);
            _content.Add(host);
            picker.SelectedIndex = 0;
        }

        _content.Add(Ui.Button("Ver histórico completo", () => _navigator.OpenHistoryAsync(), true));
    }

    private static Border BarChart(IReadOnlyList<(string Label, double Value)> points, string unit)
    {
        var max = Math.Max(1, points.Max(item => item.Value));
        var grid = new Grid { ColumnSpacing = 7 };
        for (var index = 0; index < points.Count; index++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var point = points[index];
            var column = new VerticalStackLayout { Spacing = 5 };
            var value = Ui.Text(point.Value <= 0 ? "–" : point.Value.ToString("0.#", CultureInfo.CurrentCulture),
                10, point.Value > 0 ? Ui.Foreground : Ui.Muted);
            value.HorizontalTextAlignment = TextAlignment.Center;
            column.Add(value);
            var track = new Grid { HeightRequest = 72 };
            track.Add(new BoxView { BackgroundColor = Ui.Border, HeightRequest = 1,
                VerticalOptions = LayoutOptions.End });
            if (point.Value > 0)
                track.Add(new BoxView { BackgroundColor = Ui.Accent,
                    HeightRequest = Math.Max(5, 70d * point.Value / max),
                    VerticalOptions = LayoutOptions.End, WidthRequest = 16,
                    HorizontalOptions = LayoutOptions.Center });
            column.Add(track);
            var label = Ui.Text(point.Label, 10, Ui.Muted);
            label.HorizontalTextAlignment = TextAlignment.Center;
            column.Add(label);
            grid.Add(column, index);
        }
        var content = new VerticalStackLayout { Spacing = 8 };
        content.Add(grid);
        content.Add(Ui.Text(unit, 10, Ui.Muted));
        return Ui.Card(content, new Thickness(12));
    }

    private sealed class ExerciseSeries(string name)
    {
        public string Name { get; } = name;
        public List<(string Label, double Value)> Points { get; } = [];
        public override string ToString() => Name;
    }
}
