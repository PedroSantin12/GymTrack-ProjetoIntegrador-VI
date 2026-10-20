using System.Globalization;
using GymTrack.Data.Dao;
using GymTrack.Models;
using GymTrack.Services;
using GymTrack.Views.Components;

namespace GymTrack.Views.Consumer;

public sealed class ConsumerProgressPage : WorkspacePage
{
    private static readonly CultureInfo Portuguese = CultureInfo.GetCultureInfo("pt-BR");
    private readonly IAppNavigator _navigator;
    private readonly AppSession _session;
    private readonly ISessionDao _sessions;
    private readonly IWorkoutCatalogService _catalog;
    private readonly IManualWorkoutLogService _manualLogs;
    private readonly VerticalStackLayout _content = new() { Spacing = 20 };
    private readonly Label _status = ConsumerUi.Status();
    private Task? _loadTask;
    private bool _reloadRequested;
    private bool _busy;

    public ConsumerProgressPage(IAppNavigator navigator, AppSession session,
        ISessionDao sessions, IWorkoutCatalogService catalog, IManualWorkoutLogService manualLogs)
        : base("Sua evolução", "Acompanhe seu progresso.", AppSection.Progress, navigator, session)
    {
        _navigator = navigator;
        _session = session;
        _sessions = sessions;
        _catalog = catalog;
        _manualLogs = manualLogs;
        SetBody(new VerticalStackLayout { Spacing = 10, Children = { _status, _content } });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private Task LoadAsync()
    {
        if (_loadTask is { IsCompleted: false })
        {
            _reloadRequested = true;
            return _loadTask;
        }

        _loadTask = LoadUntilSettledAsync();
        return _loadTask;
    }

    private async Task LoadUntilSettledAsync()
    {
        do
        {
            _reloadRequested = false;
            await LoadContentAsync();
        } while (_reloadRequested);
    }

    private async Task LoadContentAsync()
    {
        try
        {
            if (_session.CurrentUser is not { } user) return;
            _status.Text = string.Empty;
            await _manualLogs.EnsureDemoExamplesAsync(user);
            var sessions = (await _sessions.GetSummariesAsync())
                .Where(item => item.FinishedAt is not null)
                .OrderByDescending(item => item.StartedAt).ToList();
            var manual = await _manualLogs.GetForUserAsync(user.Id);
            var recordedManual = manual.Where(item => !item.IsExample).ToList();
            var routines = await _catalog.GetPersonalAsync(user.Id);

            _content.Clear();
            AddManualForm(routines);
            if (manual.Any(item => item.IsExample))
                _content.Add(Ui.Text("Os registros marcados como Exemplo aparecem no histórico, mas não entram nas estatísticas.",
                    12, Ui.Muted));

            if (sessions.Count + recordedManual.Count == 0)
            {
                _content.Add(ConsumerUi.Empty("Ainda não há treinos realizados.",
                    "Registre um treino feito ou conclua uma sessão pelo aplicativo para acompanhar sua evolução."));
                _content.Add(Ui.Button("Escolher treino",
                    () => _navigator.NavigateAsync(AppSection.PersonalWorkouts)));
                if (manual.Count > 0) AddTimeline(sessions, manual);
                return;
            }

            await AddProgressAsync(sessions, recordedManual);
            AddTimeline(sessions, manual);
        }
        catch
        {
            _status.Text = "Não foi possível carregar sua evolução. Volte a esta página para tentar novamente.";
        }
    }

    private void AddManualForm(IReadOnlyList<WorkoutCatalogItem> routines)
    {
        var form = new VerticalStackLayout { Spacing = 10 };
        form.Add(Ui.Text("Registrar treino feito", 18, Ui.Foreground, FontAttributes.Bold));
        form.Add(Ui.Text("Anote aqui um treino realizado fora do aplicativo.", 13, Ui.Muted));

        var name = new Entry
        {
            Placeholder = "Nome do treino",
            MaxLength = 100,
            ReturnType = ReturnType.Done
        };

        var routineNames = routines.Select(item => item.Name)
            .Distinct(StringComparer.CurrentCultureIgnoreCase).ToList();
        if (routineNames.Count > 0)
        {
            var routine = new Picker
            {
                Title = "Usar uma rotina pessoal (opcional)",
                ItemsSource = routineNames
            };
            routine.SelectedIndexChanged += (_, _) =>
            {
                if (routine.SelectedItem is string selected) name.Text = selected;
            };
            form.Add(Ui.Text("Minha rotina", 12, Ui.Muted));
            form.Add(Ui.Card(routine, new Thickness(8, 0)));
        }

        form.Add(Ui.Text("Nome", 12, Ui.Muted));
        form.Add(Ui.Card(name, new Thickness(8, 0)));

        var date = new DatePicker { Date = DateTime.Today, MaximumDate = DateTime.Today };
        form.Add(Ui.Text("Dia do treino", 12, Ui.Muted));
        form.Add(Ui.Card(date, new Thickness(8, 0)));

        var notes = new Editor
        {
            Placeholder = "Observações (opcional)",
            AutoSize = EditorAutoSizeOption.TextChanges,
            MaxLength = 500,
            MaximumHeightRequest = 120
        };
        form.Add(Ui.Text("Observações", 12, Ui.Muted));
        form.Add(Ui.Card(notes, new Thickness(8)));

        form.Add(Ui.Button("Salvar treino feito", () => SaveManualAsync(name, date, notes)));
        _content.Add(Ui.Card(form));
    }

    private async Task SaveManualAsync(Entry name, DatePicker date, Editor notes)
    {
        if (_busy || _session.CurrentUser is not { } user) return;
        if (string.IsNullOrWhiteSpace(name.Text))
        {
            _status.Text = "Informe o nome do treino para salvar.";
            return;
        }
        if (date.Date is not { } performedOn)
        {
            _status.Text = "Selecione o dia do treino.";
            return;
        }

        _busy = true;
        try
        {
            await _manualLogs.AddAsync(user.Id, name.Text, performedOn, notes.Text);
            await LoadAsync();
            _status.Text = "Treino registrado na sua evolução.";
        }
        catch (ArgumentException exception)
        {
            _status.Text = exception.Message.Split('\n', StringSplitOptions.TrimEntries)[0];
        }
        catch
        {
            _status.Text = "Não foi possível registrar o treino. Tente novamente.";
        }
        finally { _busy = false; }
    }

    private async Task AddProgressAsync(IReadOnlyList<SessionSummary> sessions,
        IReadOnlyList<ManualWorkoutLog> manual)
    {
        var minutes = (int)Math.Round(sessions.Sum(item =>
            Math.Max(0, (item.FinishedAt!.Value - item.StartedAt).TotalMinutes)));
        var volume = sessions.Sum(item => item.TotalVolume);
        var monthStart = DateTime.Today.AddDays(-27);
        var frequency = (sessions.Count(item => item.StartedAt.ToLocalTime().Date >= monthStart) +
            manual.Count(item => item.PerformedOn.Date >= monthStart)) / 4d;

        _content.Add(Ui.Text("Resumo de desempenho", 18, Ui.Foreground, FontAttributes.Bold));
        var metrics = new Grid
        {
            RowSpacing = 9, ColumnSpacing = 9,
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }
        };
        metrics.Add(ConsumerUi.Metric((sessions.Count + manual.Count).ToString(CultureInfo.CurrentCulture),
            "Treinos realizados"), 0, 0);
        metrics.Add(ConsumerUi.Metric($"{volume:N0} kg", "Volume no app"), 1, 0);
        metrics.Add(ConsumerUi.Metric(minutes >= 60 ? $"{minutes / 60}h {minutes % 60:00}" : $"{minutes} min",
            "Tempo no app"), 0, 1);
        metrics.Add(ConsumerUi.Metric($"{frequency:0.#}", "Treinos por semana · 4 sem"), 1, 1);
        _content.Add(metrics);

        _content.Add(ConsumerUi.Section("Frequência de treinos", "Esta semana · segunda a domingo"));
        var today = DateTime.Today;
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        _content.Add(BarChart(Enumerable.Range(0, 7)
            .Select(index => monday.AddDays(index))
            .Select(day => (
                Portuguese.TextInfo.ToTitleCase(day.ToString("ddd", Portuguese).TrimEnd('.')),
                (double)(sessions.Count(item => item.StartedAt.ToLocalTime().Date == day) +
                    manual.Count(item => item.PerformedOn.Date == day))))
            .ToList(), "treinos"));

        if (sessions.Count == 0) return;
        _content.Add(ConsumerUi.Section("Volume de treino", "Últimas 6 semanas · apenas sessões pelo app"));
        var weeks = Enumerable.Range(0, 6).Select(index => monday.AddDays((index - 5) * 7)).ToList();
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
            _content.Add(ConsumerUi.Section("Evolução de carga", "Até 6 sessões por exercício · últimas 40 pelo app"));
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
    }

    private void AddTimeline(IReadOnlyList<SessionSummary> sessions,
        IReadOnlyList<ManualWorkoutLog> manual)
    {
        _content.Add(ConsumerUi.Section("Histórico de treinos", "Registros manuais e sessões pelo aplicativo"));
        var timeline = sessions
            .Select(item => new TimelineEntry(item.StartedAt.ToLocalTime(), item.WorkoutName, item, null))
            .Concat(manual.Select(item =>
                new TimelineEntry(item.PerformedOn.Date, item.WorkoutName, null, item)))
            .OrderByDescending(item => item.Date)
            .ToList();

        foreach (var item in timeline)
        {
            var details = new VerticalStackLayout { Spacing = 7 };
            var heading = new Grid
            {
                ColumnSpacing = 8,
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
            };
            heading.Add(Ui.Text(item.Name, 17, Ui.Foreground, FontAttributes.Bold));
            heading.Add(Ui.Tag(item.Manual is { IsExample: true } ? "Exemplo" :
                item.Manual is not null ? "Manual" : "Pelo app"), 1);
            details.Add(heading);
            var dateText = item.Date.ToString("dddd, dd/MM/yyyy", Portuguese);
            details.Add(Ui.Text(Portuguese.TextInfo.ToTitleCase(dateText), 12, Ui.Muted));

            if (item.Session is { } session)
            {
                details.Add(Ui.Text($"{session.SetCount} séries · {session.TotalVolume:N0} kg de volume", 12, Ui.Muted));
            }
            else if (item.Manual is { } log)
            {
                if (!string.IsNullOrWhiteSpace(log.Notes))
                    details.Add(Ui.Text(log.Notes, 13, Ui.Muted));
                details.Add(Ui.Button("Excluir registro", () => DeleteManualAsync(log), true));
            }
            _content.Add(Ui.Card(details));
        }

        if (sessions.Count > 0)
            _content.Add(Ui.Button("Detalhes das sessões pelo app", () => _navigator.OpenHistoryAsync(), true));
    }

    private async Task DeleteManualAsync(ManualWorkoutLog log)
    {
        if (_busy || _session.CurrentUser is not { } user) return;
        var dateText = log.PerformedOn.Date.ToString("dd/MM/yyyy", Portuguese);
        if (!await DisplayAlertAsync("Excluir registro?",
                $"Remover “{log.WorkoutName}” de {dateText} da sua evolução?",
                "Excluir", "Cancelar")) return;

        _busy = true;
        try
        {
            await _manualLogs.DeleteAsync(user.Id, log.Id);
            await LoadAsync();
            _status.Text = "Registro excluído.";
        }
        catch
        {
            _status.Text = "Não foi possível excluir o registro. Tente novamente.";
        }
        finally { _busy = false; }
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

    private sealed record TimelineEntry(DateTime Date, string Name,
        SessionSummary? Session, ManualWorkoutLog? Manual);

    private sealed class ExerciseSeries(string name)
    {
        public string Name { get; } = name;
        public List<(string Label, double Value)> Points { get; } = [];
        public override string ToString() => Name;
    }
}
