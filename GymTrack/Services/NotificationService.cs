using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace GymTrack.Services;

public sealed class NotificationService : INotificationService
{
    public Task ShowAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        return Toast.Make(message, ToastDuration.Short, textSize: 14)
            .Show(cancellationToken);
    }
}
