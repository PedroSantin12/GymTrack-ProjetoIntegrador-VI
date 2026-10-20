using GymTrack.Models;

namespace GymTrack.Services;

public interface IBodyMeasurementService
{
    Task<IReadOnlyList<BodyMeasurement>> GetForUserAsync(int userId);

    Task<int> SaveAsync(int userId, DateTime measuredOn, double weightKg,
        double? heightCm, double? waistCm, double? hipCm, double? chestCm);

    Task DeleteAsync(int userId, int id);
}
