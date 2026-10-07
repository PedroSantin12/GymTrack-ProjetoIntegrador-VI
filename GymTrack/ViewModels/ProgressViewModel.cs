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
    private int _latestProgressRequest;
    private bool _isUpdatingExercises;

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
        if (!_isUpdatingExercises)
        {
            _ = LoadProgressAsync();
        }
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
        Interlocked.Increment(ref _latestProgressRequest);
        try
        {
            var exercises = await _exerciseDao.GetAllAsync(includeInactive: true);
            var selectedId = SelectedExercise?.Id;
            _isUpdatingExercises = true;
            try
            {
                Exercises.Clear();
                foreach (var exercise in exercises)
                {
                    Exercises.Add(exercise);
                }

                SelectedExercise = Exercises.FirstOrDefault(item => item.Id == selectedId)
                    ?? Exercises.FirstOrDefault();
            }
            finally
            {
                _isUpdatingExercises = false;
            }

            await LoadProgressAsync();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao carregar a lista de exercícios da evolução.");
            StatusMessage = "Não foi possível carregar os exercícios.";
            IsBusy = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    private async Task LoadProgressAsync()
    {
        var request = Interlocked.Increment(ref _latestProgressRequest);
        var exerciseId = SelectedExercise?.Id;
        if (exerciseId is null)
        {
            _points = [];
            IsBusy = false;
            UpdateChart();
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            var points = await _analyticsService.GetExerciseProgressAsync(exerciseId.Value);
            if (request != Volatile.Read(ref _latestProgressRequest))
            {
                return;
            }

            _points = points;
            UpdateChart();
        }
        catch (Exception exception)
        {
            if (request != Volatile.Read(ref _latestProgressRequest))
            {
                return;
            }

            _logger.LogError(exception, "Falha ao carregar evolução do exercício {ExerciseId}.", exerciseId.Value);
            StatusMessage = "Não foi possível carregar a evolução.";
            _points = [];
            UpdateChart();
        }
        finally
        {
            if (request == Volatile.Read(ref _latestProgressRequest))
            {
                IsBusy = false;
                OnPropertyChanged(nameof(IsEmpty));
            }
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
