using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GymTrack.Data.Dao;
using GymTrack.Models;
using GymTrack.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Microsoft.Extensions.Logging;

namespace GymTrack.ViewModels;

public partial class ProgressViewModel : BaseViewModel
{
    private readonly IExerciseDao _exerciseDao;
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger<ProgressViewModel> _logger;
    private IReadOnlyList<ExerciseProgressPoint> _points = [];

    public ProgressViewModel(
        IExerciseDao exerciseDao,
        IAnalyticsService analyticsService,
        ILogger<ProgressViewModel> logger)
    {
        _exerciseDao = exerciseDao;
        _analyticsService = analyticsService;
        _logger = logger;
        Title = "Evolução";
    }

    public ObservableCollection<Exercise> Exercises { get; } = [];

    public ISeries[] Series { get; private set; } = [];

    public Axis[] XAxes { get; private set; } = [new Axis()];

    public Axis[] YAxes { get; private set; } = [new Axis { MinLimit = 0 }];

    [ObservableProperty]
    private Exercise? selectedExercise;

    [ObservableProperty]
    private bool isMaxLoadSelected = true;

    [ObservableProperty]
    private bool isVolumeSelected;

    public bool HasChart => _points.Count >= 2;

    public bool IsEmpty => !IsBusy && !HasChart;

    public string BestLoadText { get; private set; } = "—";

    public string LastLoadText { get; private set; } = "—";

    public string SessionCountText { get; private set; } = "0";

    public string VariationText { get; private set; } = "—";

    partial void OnSelectedExerciseChanged(Exercise? value)
    {
        _ = LoadProgressAsync();
    }

    partial void OnIsMaxLoadSelectedChanged(bool value)
    {
        if (value)
        {
            IsVolumeSelected = false;
            UpdateChart();
        }
    }

    partial void OnIsVolumeSelectedChanged(bool value)
    {
        if (value)
        {
            IsMaxLoadSelected = false;
            UpdateChart();
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (Exercises.Count == 0)
        {
            var exercises = await _exerciseDao.GetAllAsync(includeInactive: true);
            foreach (var exercise in exercises)
            {
                Exercises.Add(exercise);
            }

            SelectedExercise ??= Exercises.FirstOrDefault();
        }

        await LoadProgressAsync();
    }

    private async Task LoadProgressAsync()
    {
        if (SelectedExercise is null)
        {
            _points = [];
            UpdateChart();
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            _points = await _analyticsService.GetExerciseProgressAsync(SelectedExercise.Id);
            UpdateChart();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao carregar evolução do exercício {ExerciseId}.", SelectedExercise.Id);
            StatusMessage = "Não foi possível carregar a evolução.";
            _points = [];
            UpdateChart();
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    private void UpdateChart()
    {
        var values = _points
            .Select(point => IsVolumeSelected ? point.Volume : point.MaxLoad)
            .ToArray();
        Series = values.Length == 0
            ? []
            :
            [
                new LineSeries<double>
            {
                Name = IsVolumeSelected ? "Volume" : "Maior carga",
                Values = values,
                GeometrySize = 10,
                LineSmoothness = 0.25
            }
            ];
        XAxes =
        [
            new Axis
            {
                Labels = _points
                    .Select(point => point.SessionDate.ToLocalTime().ToString("dd/MM"))
                    .ToArray(),
                LabelsRotation = -25
            }
        ];
        YAxes = [new Axis { MinLimit = 0, Name = IsVolumeSelected ? "Volume (kg)" : "Carga (kg)" }];

        BestLoadText = _points.Count == 0 ? "—" : $"{_points.Max(point => point.MaxLoad):N1} kg";
        LastLoadText = _points.Count == 0 ? "—" : $"{_points[^1].MaxLoad:N1} kg";
        SessionCountText = _points.Count.ToString();
        VariationText = CalculateVariation(values);
        OnPropertyChanged(nameof(BestLoadText));
        OnPropertyChanged(nameof(LastLoadText));
        OnPropertyChanged(nameof(SessionCountText));
        OnPropertyChanged(nameof(VariationText));
        OnPropertyChanged(nameof(Series));
        OnPropertyChanged(nameof(XAxes));
        OnPropertyChanged(nameof(YAxes));
        OnPropertyChanged(nameof(HasChart));
        OnPropertyChanged(nameof(IsEmpty));
    }

    private static string CalculateVariation(IReadOnlyList<double> values)
    {
        if (values.Count < 2 || values[0] == 0)
        {
            return "—";
        }

        var percentage = (values[^1] - values[0]) / values[0] * 100;
        return $"{percentage:+0.0;-0.0;0}%";
    }
}
