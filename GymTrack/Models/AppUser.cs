namespace GymTrack.Models;

public sealed class AppUser
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string JobTitle { get; set; } = string.Empty;

    public string GymName { get; set; } = string.Empty;

    public bool NotificationsEnabled { get; set; } = true;

    public UserRole Role { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
}
