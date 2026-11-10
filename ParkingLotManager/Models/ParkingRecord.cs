using System.Text.Json.Serialization;

namespace ParkingLotManager;

/// <summary>One visit to the lot: created at check-in, completed at check-out.</summary>
public class ParkingRecord
{
    public int Ticket { get; set; }
    public string Plate { get; set; } = "";
    public VehicleType VehicleType { get; set; }
    public string SpotId { get; set; } = "";
    public DateTime EntryTime { get; set; }
    public DateTime? ExitTime { get; set; }
    public decimal Fee { get; set; }

    [JsonIgnore]
    public bool IsActive => ExitTime is null;

    public TimeSpan DurationAt(DateTime now) => (ExitTime ?? now) - EntryTime;

    public Vehicle ToVehicle() => Vehicle.Create(VehicleType, Plate);
}
