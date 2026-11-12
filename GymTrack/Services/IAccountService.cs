using GymTrack.Models;

namespace GymTrack.Services;

public interface IAccountService
{
    Task InitializeAsync();

    Task<AppUser?> SignInAsync(string email, string password);

    Task<AppUser> RegisterConsumerAsync(string name, string email, string password);

    Task<IReadOnlyList<AppUser>> GetUsersAsync(string? search = null);

    Task UpdateUserAsync(AppUser user);

    Task<AppUser> UpdatePersonalDetailsAsync(int accountId, string name, string email, string? phone);

    Task<AppUser> UpdateProfessionalDetailsAsync(int accountId, string? jobTitle, string? gymName);

    Task<AppUser> SetNotificationsEnabledAsync(int accountId, bool enabled);

    Task ChangePasswordAsync(int accountId, string currentPassword, string newPassword);

    Task<int> CountUsersAsync();
}
