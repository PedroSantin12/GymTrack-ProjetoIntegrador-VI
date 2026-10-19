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
    private readonly IWorkoutCatalogService _catalog;
    private readonly ISessionDao _sessions;
    private readonly VerticalStackLayout _content = new() { Spacing = 22 };
    private readonly Label _status = ConsumerUi.Status();
    private bool _loading;
    private bool _busy;

    public ConsumerProgressPage(IAppNavigator navigator, AppSession session, IWorkoutCatalogService catalog, ISessionDao sessions)
        : base("Sua evolução", "Cada treino conta na sua trajetória.", AppSection.Progress, navigator, session)
    {
        _navigator = navigator;
        _session = session;
        _catalog = catalog;
        _sessions = sessions;
        var body = new VerticalStackLayout { Spacing = 16 };
        body.Add(_status);
        body.Add(_content);
        SetBody(body);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loading) return;
        _loading = true;
        try
        {
            if (_session.CurrentUser is not { } user) return;
            _status.Text = "Carregando sua evolução...";
            var ownIds = (await _catalog.GetPersonalAsync(user.Id)).Select(workout => workout.Id).ToHashSet();
            var sessions = (await _sessions.GetSummariesAsync())
                .Where(item => ownIds.Contains(item.WorkoutId) && item.FinishedAt is not null)
                .OrderByDescending(item => item.StartedAt)
                .ToList();
            _content.Clear();
            if (sessions.Count == 0)
            {
                _content.Add(Ui.Tag("UM PASSO DE CADA VEZ"));
                _content.Add(ConsumerUi.Empty("Seu acompanhamento começa no próximo treino", "Finalize uma sessão para acompanhar sua frequência, tempo de treino e histórico por aqui."));
                _content.Add(Ui.Button("Escolher meu treino", OpenWorkoutsAsync));
            }
            else
            {
                AddProgress(sessions);
            }
            _status.Text = string.Empty;
        }
        catch
        {
            _status.Text = "Não foi possível carregar sua evolução. Abra esta página novamente para tentar.";
        }
        finally
        {
            _loading = false;
        }
    }

    private void AddProgress(IReadOnlyList<SessionSummary> sessions)
    {
        var minutes = (int)Math.Round(sessions.Sum(item => Math.Max(0, (item.FinishedAt!.Value - item.StartedAt).TotalMinutes)));
        var metrics = new Grid
        {
            ColumnSpacing = 10,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }
        };
        metrics.Add(ConsumerUi.Metric(sessions.Count.ToString(CultureInfo.CurrentCulture), "Treinos concluídos"), 0);
        metrics.Add(ConsumerUi.Metric(minutes.ToString(CultureInfo.CurrentCulture), "Minutos de treino"), 1);
        _content.Add(metrics);
        _content.Add(ConsumerUi.Section("Sua frequência", "Sessões concluídas nos últimos 7 dias."));
        _content.Add(FrequencyChart(sessions));
        _content.Add(ConsumerUi.Section("Últimos treinos", "Seu histórico de sessões concluídas."));
        _content.Add(Ui.Button("Ver histórico completo", () => _navigator.OpenHistoryAsync(), true));
        foreach (var session in sessions.Take(10))
        {
            var card = new VerticalStackLayout { Spacing = 9 };
            card.Add(Ui.Text(session.WorkoutName, 18, null, FontAttributes.Bold));
            card.Add(Ui.Text(session.StartedAt.ToLocalTime().ToString("dd MMM yyyy • HH:mm", CultureInfo.GetCultureInfo("pt-BR")), 13, Ui.Muted));
            var duration = (int)Math.Round(Math.Max(0, (session.FinishedAt!.Value - session.StartedAt).TotalMinutes));
            card.Add(Ui.Text($"{duration} min  •  {session.SetCount} {(session.SetCount == 1 ? "série" : "séries")}", 13, Ui.Muted));
            _content.Add(Ui.Card(card));
        }
    }

    private static Border FrequencyChart(IReadOnlyList<SessionSummary> sessions)
    {
        var firstDay = DateTime.Today.AddDays(-6);
        var days = Enumerable.Range(0, 7).Select(index => firstDay.AddDays(index)).ToList();
        var counts = days.Select(day => sessions.Count(item => item.StartedAt.ToLocalTime().Date == day)).ToArray();
        var max = Math.Max(1, counts.Max());
        var grid = new Grid { ColumnSpacing = 10 };
        for (var index = 0; index < days.Count; index++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var column = new VerticalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.Fill };
            var count = Ui.Text(counts[index].ToString(CultureInfo.CurrentCulture), 12, counts[index] > 0 ? Ui.Accent : Ui.Muted);
            count.HorizontalTextAlignment = TextAlignment.Center;
            column.Add(count);
            var track = new Grid { HeightRequest = 80, BackgroundColor = Ui.Elevated };
            track.Add(new BoxView
            {
                BackgroundColor = Ui.Accent,
                HeightRequest = counts[index] == 0 ? 0 : Math.Max(8, 80d * counts[index] / max),
                VerticalOptions = LayoutOptions.End,
                IsVisible = counts[index] > 0
            });
            column.Add(track);
            var day = Ui.Text(days[index].ToString("ddd", CultureInfo.GetCultureInfo("pt-BR")).TrimEnd('.'), 10, Ui.Muted);
            day.HorizontalTextAlignment = TextAlignment.Center;
            column.Add(day);
            grid.Add(column, index);
        }
        return Ui.Card(grid);
    }

    private async Task OpenWorkoutsAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            await _navigator.NavigateAsync(AppSection.PersonalWorkouts);
        }
        catch
        {
            _status.Text = "Não foi possível abrir seus treinos. Tente novamente.";
        }
        finally
        {
            _busy = false;
        }
    }
}
