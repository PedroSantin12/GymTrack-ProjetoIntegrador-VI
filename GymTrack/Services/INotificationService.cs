namespace GymTrack.Services;

public interface INotificationService
{
    Task ShowAsync(string message, CancellationToken cancellationToken = default);
}
