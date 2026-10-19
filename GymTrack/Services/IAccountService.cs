using GymTrack.Models;

namespace GymTrack.Services;

public interface IAccountService
{
    Task InitializeAsync();

    Task<AppUser?> SignInAsync(string email, string password);

    Task<AppUser> RegisterConsumerAsync(string name, string email, string password);

    Task<IReadOnlyList<AppUser>> GetUsersAsync(string? search = null);

    Task UpdateUserAsync(AppUser user);

    Task<int> CountUsersAsync();
}
