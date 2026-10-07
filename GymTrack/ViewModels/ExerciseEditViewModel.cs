using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GymTrack.Data.Dao;
using GymTrack.Models;
using GymTrack.Services;
using Microsoft.Extensions.Logging;

namespace GymTrack.ViewModels;

public partial class ExerciseEditViewModel : BaseViewModel
{
    private readonly IExerciseDao _exerciseDao;
    private readonly IDialogService _dialogService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ExerciseEditViewModel> _logger;
    private int _exerciseId;
    private bool _wasActive;
    private bool _canSave = true;

    public ExerciseEditViewModel(
        IExerciseDao exerciseDao,
        IDialogService dialogService,
        INotificationService notificationService,
        ILogger<ExerciseEditViewModel> logger)
    {
        _exerciseDao = exerciseDao;
        _dialogService = dialogService;
        _notificationService = notificationService;
        _logger = logger;
        Title = "Novo exercício";
    }

    public event EventHandler? CloseRequested;

    public IReadOnlyList<string> MuscleGroups { get; } =
    [
        "Peito",
        "Costas",
        "Pernas",
        "Ombros",
        "Braços",
        "Core"
    ];

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string? selectedMuscleGroup;

    [ObservableProperty]
    private string notes = string.Empty;

    [ObservableProperty]
    private bool isActive = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationMessage))]
    private string? validationMessage;

    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

    public async Task InitializeAsync(int? exerciseId)
    {
        _exerciseId = 0;
        _canSave = exerciseId is not > 0;
        Title = "Novo exercício";
        Name = string.Empty;
        SelectedMuscleGroup = null;
        Notes = string.Empty;
        IsActive = true;
        _wasActive = true;
        ValidationMessage = null;
        StatusMessage = null;

        if (exerciseId is not > 0)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var exercise = await _exerciseDao.GetByIdAsync(exerciseId.Value);
            if (exercise is null)
            {
                ValidationMessage = "Não foi possível localizar este exercício.";
                return;
            }

            _exerciseId = exercise.Id;
            _canSave = true;
            Title = "Editar exercício";
            Name = exercise.Name;
            SelectedMuscleGroup = exercise.MuscleGroup;
            Notes = exercise.Notes ?? string.Empty;
            IsActive = exercise.IsActive;
            _wasActive = exercise.IsActive;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao carregar exercício {ExerciseId}.", exerciseId);
            ValidationMessage = "Não foi possível carregar o exercício. Tente novamente.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy || !_canSave)
        {
            return;
        }

        var normalizedName = (Name ?? string.Empty).Trim();
        var normalizedGroup = SelectedMuscleGroup?.Trim();
        var normalizedNotes = (Notes ?? string.Empty).Trim();

        ValidationMessage = Validate(normalizedName, normalizedGroup, normalizedNotes);
        if (HasValidationMessage)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = null;

        try
        {
            if (await _exerciseDao.ExistsAsync(
                    normalizedName,
                    normalizedGroup!,
                    _exerciseId))
            {
                ValidationMessage = "Já existe um exercício com este nome e grupo muscular.";
                return;
            }

            if (_exerciseId > 0 && _wasActive && !IsActive)
            {
                var confirmed = await _dialogService.ConfirmAsync(
                    "Arquivar exercício?",
                    $"Arquivar {normalizedName}? Ele deixará de aparecer na lista padrão.",
                    "Arquivar",
                    "Cancelar");
                if (!confirmed)
                {
                    return;
                }
            }

            var exercise = new Exercise
            {
                Id = _exerciseId,
                Name = normalizedName,
                MuscleGroup = normalizedGroup!,
                Notes = string.IsNullOrEmpty(normalizedNotes) ? null : normalizedNotes,
                IsActive = IsActive
            };

            if (_exerciseId == 0)
            {
                var inserted = await _exerciseDao.InsertAsync(exercise);
                if (inserted != 1)
                {
                    throw new InvalidOperationException("O exercício não foi inserido.");
                }

                _exerciseId = exercise.Id;
            }
            else
            {
                var updated = await _exerciseDao.UpdateAsync(exercise);
                if (updated != 1)
                {
                    throw new InvalidOperationException("O exercício não foi atualizado.");
                }
            }

            _wasActive = IsActive;
            await TryNotifyAsync("Exercício salvo.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao salvar exercício {ExerciseId}.", _exerciseId);
            ValidationMessage = "Não foi possível salvar o exercício. Tente novamente.";
            await TryNotifyAsync(ValidationMessage);
        }
        finally
        {
            IsBusy = false;
        }

        if (!HasValidationMessage)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    private void Close()
    {
        if (!IsBusy)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private string? Validate(string normalizedName, string? normalizedGroup, string normalizedNotes)
    {
        if (normalizedName.Length is < 2 or > 80)
        {
            return "Informe um nome entre 2 e 80 caracteres.";
        }

        if (string.IsNullOrWhiteSpace(normalizedGroup) ||
            !MuscleGroups.Contains(normalizedGroup, StringComparer.Ordinal))
        {
            return "Selecione um grupo muscular.";
        }

        if (normalizedNotes.Length > 500)
        {
            return "As observações devem ter no máximo 500 caracteres.";
        }

        return null;
    }

    private async Task TryNotifyAsync(string message)
    {
        try
        {
            await _notificationService.ShowAsync(message);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "O aviso ao usuário não pôde ser exibido.");
        }
    }
}
