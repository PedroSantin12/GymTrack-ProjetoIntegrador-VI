using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GymTrack.Models;

namespace GymTrack.ViewModels;

public partial class SessionSetItemViewModel : ObservableObject
{
    public SessionSetItemViewModel(
        int setNumber,
        string previousText,
        string loadText,
        string repsText,
        Func<SessionSetItemViewModel, Task> complete,
        Action<SessionSetItemViewModel> remove)
    {
        SetNumber = setNumber;
        PreviousText = previousText;
        LoadText = loadText;
        RepsText = repsText;
        CompleteCommand = new AsyncRelayCommand(() => complete(this));
        RemoveCommand = new RelayCommand(() => remove(this));
    }

    public int Id { get; private set; }

    public int SetNumber { get; }

    public string SetNumberText => SetNumber.ToString();

    public string PreviousText { get; }

    public double? CompletedLoadKg { get; private set; }

    public int? CompletedReps { get; private set; }

    [ObservableProperty]
    private string loadText;

    [ObservableProperty]
    private string repsText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotCompleted))]
    [NotifyPropertyChangedFor(nameof(CanComplete))]
    [NotifyPropertyChangedFor(nameof(CompletionText))]
    private bool isCompleted;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanComplete))]
    private bool isBusy;

    [ObservableProperty]
    private bool canRemove;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationMessage))]
    private string? validationMessage;

    public bool IsNotCompleted => !IsCompleted;

    public bool CanComplete => !IsCompleted && !IsBusy;

    public string CompletionText => IsCompleted ? "Concluída" : "Concluir";

    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

    public IAsyncRelayCommand CompleteCommand { get; }

    public IRelayCommand RemoveCommand { get; }

    public void MarkCompleted(SetRecord record)
    {
        Id = record.Id;
        CompletedLoadKg = record.LoadKg;
        CompletedReps = record.Reps;
        LoadText = record.LoadKg.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture);
        RepsText = record.Reps.ToString(System.Globalization.CultureInfo.InvariantCulture);
        ValidationMessage = null;
        CanRemove = false;
        IsCompleted = true;
    }
}
