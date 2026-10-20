using GymTrack.Data;
using GymTrack.Models;

namespace GymTrack.Services;

public sealed class BodyMeasurementService(GymTrackDatabase database) : IBodyMeasurementService
{
    public async Task<IReadOnlyList<BodyMeasurement>> GetForUserAsync(int userId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        var connection = await database.GetConnectionAsync();
        return await connection.QueryAsync<BodyMeasurement>(
            "SELECT * FROM BodyMeasurement WHERE OwnerUserId = ? ORDER BY MeasuredOn DESC, Id DESC",
            userId);
    }

    public async Task<int> SaveAsync(int userId, DateTime measuredOn, double weightKg,
        double? heightCm, double? waistCm, double? hipCm, double? chestCm)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        if (measuredOn == default || measuredOn.Date > DateTime.Today)
            throw new ArgumentOutOfRangeException(nameof(measuredOn), "A data não pode estar no futuro.");
        ValidateMeasurement(weightKg, nameof(weightKg));
        ValidateOptionalMeasurement(heightCm, nameof(heightCm));
        ValidateOptionalMeasurement(waistCm, nameof(waistCm));
        ValidateOptionalMeasurement(hipCm, nameof(hipCm));
        ValidateOptionalMeasurement(chestCm, nameof(chestCm));

        var date = DateTime.SpecifyKind(measuredOn.Date, DateTimeKind.Unspecified);
        var connection = await database.GetConnectionAsync();
        var id = 0;
        await connection.RunInTransactionAsync(transaction =>
        {
            var entry = transaction.FindWithQuery<BodyMeasurement>(
                "SELECT * FROM BodyMeasurement WHERE OwnerUserId = ? AND MeasuredOn = ?", userId, date);
            if (entry is null)
            {
                entry = new BodyMeasurement { OwnerUserId = userId, MeasuredOn = date };
            }

            entry.WeightKg = weightKg;
            entry.HeightCm = heightCm;
            entry.WaistCm = waistCm;
            entry.HipCm = hipCm;
            entry.ChestCm = chestCm;

            if (entry.Id == 0)
                transaction.Insert(entry);
            else
                transaction.Update(entry);

            id = entry.Id;
        });
        return id;
    }

    public async Task DeleteAsync(int userId, int id)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        var connection = await database.GetConnectionAsync();
        if (await connection.ExecuteAsync(
                "DELETE FROM BodyMeasurement WHERE Id = ? AND OwnerUserId = ?", id, userId) == 0)
            throw new InvalidOperationException("Medida não encontrada.");
    }

    private static void ValidateMeasurement(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0)
            throw new ArgumentOutOfRangeException(parameterName, "Informe uma medida maior que zero.");
    }

    private static void ValidateOptionalMeasurement(double? value, string parameterName)
    {
        if (value.HasValue)
            ValidateMeasurement(value.Value, parameterName);
    }
}
