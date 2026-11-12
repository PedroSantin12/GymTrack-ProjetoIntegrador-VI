using GymTrack.Views.Components;

namespace GymTrack.Services;

public sealed class DialogService : IDialogService
{
    public async Task AlertAsync(
        string title,
        string message,
        string button)
    {
        var page = Ui.GetActivePage();
        if (page is not null)
        {
            await page.ShowAppMessageAsync(title, message, button);
        }
    }

    public async Task<string?> ChooseActionAsync(
        string title,
        string cancel,
        params string[] actions)
    {
        var page = Ui.GetActivePage();
        if (page is null)
        {
            return null;
        }

        return await page.ChooseAppActionAsync(title, cancel, actions);
    }

    public async Task<bool> ConfirmAsync(
        string title,
        string message,
        string accept,
        string cancel)
    {
        var page = Ui.GetActivePage();
        if (page is null)
        {
            return false;
        }

        return await page.ConfirmAppActionAsync(title, message, accept, cancel);
    }
}
