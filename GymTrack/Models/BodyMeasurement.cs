using SQLite;

namespace GymTrack.Models;

[Table("BodyMeasurement")]
public sealed class BodyMeasurement
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int OwnerUserId { get; set; }

    public DateTime MeasuredOn { get; set; }

    public double WeightKg { get; set; }

    public double? HeightCm { get; set; }

    public double? WaistCm { get; set; }

    public double? HipCm { get; set; }

    public double? ChestCm { get; set; }
}
