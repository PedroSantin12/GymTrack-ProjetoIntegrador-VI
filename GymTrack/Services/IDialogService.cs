namespace GymTrack.Services;

public interface IDialogService
{
    Task AlertAsync(
        string title,
        string message,
        string button);

    Task<string?> ChooseActionAsync(
        string title,
        string cancel,
        params string[] actions);

    Task<bool> ConfirmAsync(
        string title,
        string message,
        string accept,
        string cancel);
}
