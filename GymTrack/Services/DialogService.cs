namespace GymTrack.Services;

public sealed class DialogService : IDialogService
{
    public async Task AlertAsync(
        string title,
        string message,
        string button)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is not null)
        {
            await page.DisplayAlertAsync(title, message, button);
        }
    }

    public async Task<string?> ChooseActionAsync(
        string title,
        string cancel,
        params string[] actions)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is null)
        {
            return null;
        }

        return await page.DisplayActionSheetAsync(title, cancel, null, actions);
    }

    public async Task<bool> ConfirmAsync(
        string title,
        string message,
        string accept,
        string cancel)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is null)
        {
            return false;
        }

        return await page.DisplayAlertAsync(title, message, accept, cancel);
    }
}
