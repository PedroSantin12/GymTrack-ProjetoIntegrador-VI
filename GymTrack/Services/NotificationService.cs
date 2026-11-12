using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace GymTrack.Services;

public sealed class NotificationService(AppSession session) : INotificationService
{
    public Task ShowAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        if (session.CurrentUser is { NotificationsEnabled: false })
            return Task.CompletedTask;

        return Toast.Make(message, ToastDuration.Short, textSize: 14)
            .Show(cancellationToken);
    }
}
