using System.Text;
using System.Text.RegularExpressions;

namespace ParkingLotManager;

public class ParkingException : Exception
{
    public ParkingException(string message) : base(message) { }
}

/// <summary>
/// The business logic of the lot: which spots exist, who is parked where,
/// checking vehicles in and out, and the daily numbers.
/// </summary>
public class ParkingLot
{
    private static readonly Regex PlatePattern = new("^[A-Z0-9-]{3,10}$");
    private readonly List<ParkingSpot> spots = new();

    public ParkingLot(List<ParkingRecord> records)
    {
        Records = records;
        AddRow("A", 6, SpotSize.Small);
        AddRow("B", 8, SpotSize.Regular);
        AddRow("C", 8, SpotSize.Regular);
        AddRow("D", 4, SpotSize.Large);
    }

    public IReadOnlyList<ParkingSpot> Spots => spots;
    public List<ParkingRecord> Records { get; }
    public IEnumerable<ParkingRecord> ActiveRecords => Records.Where(r => r.IsActive);
    public int FreeSpotCount => spots.Count(s => IsFree(s.Id));
    public double OccupancyPercent => 100.0 * (spots.Count - FreeSpotCount) / spots.Count;

    private void AddRow(string row, int count, SpotSize size)
    {
        for (int number = 1; number <= count; number++)
            spots.Add(new ParkingSpot(row, number, size));
    }

    public ParkingSpot? GetSpot(string id) => spots.FirstOrDefault(s => s.Id == id);
    public ParkingRecord? ActiveRecordAt(string spotId) => ActiveRecords.FirstOrDefault(r => r.SpotId == spotId);
    public bool IsFree(string spotId) => ActiveRecordAt(spotId) is null;

    public static string NormalizePlate(string text) => text.Trim().ToUpperInvariant().Replace(' ', '-');
    public static bool IsValidPlate(string plate) => PlatePattern.IsMatch(plate);

    public ParkingRecord? FindActiveByPlate(string plate) =>
        ActiveRecords.FirstOrDefault(r => r.Plate == NormalizePlate(plate));

    public ParkingRecord? FindLastVisit(string plate) =>
        Records.Where(r => r.Plate == NormalizePlate(plate)).OrderByDescending(r => r.EntryTime).FirstOrDefault();

    public int FreeSpotsFor(VehicleType type)
    {
        var vehicle = Vehicle.Create(type, "TEMP");
        return spots.Count(s => vehicle.FitsIn(s.Size) && IsFree(s.Id));
    }

    /// <summary>
    /// Best-fit assignment: the smallest free spot the vehicle fits in, so
    /// bikes do not take up truck spots while smaller ones are free.
    /// </summary>
    public ParkingSpot? FindBestSpot(VehicleType type)
    {
        var vehicle = Vehicle.Create(type, "TEMP");
        return spots
            .Where(s => vehicle.FitsIn(s.Size) && IsFree(s.Id))
            .OrderBy(s => s.Size)
            .ThenBy(s => s.Row)
            .ThenBy(s => s.Number)
            .FirstOrDefault();
    }

    public ParkingRecord CheckIn(string plateText, VehicleType type, string? spotId, DateTime entryTime, DateTime now)
    {
        string plate = NormalizePlate(plateText);
        if (plate.Length == 0)
            throw new ParkingException("Please enter the vehicle's plate number.");
        if (!IsValidPlate(plate))
            throw new ParkingException("A plate must be 3 to 10 characters: letters, digits or dashes.");
        if (FindActiveByPlate(plate) is { } existing)
            throw new ParkingException($"{plate} is already parked in spot {existing.SpotId}.");
        if (entryTime > now.AddMinutes(1))
            throw new ParkingException("The arrival time cannot be in the future.");
        if (now - entryTime > TimeSpan.FromDays(7))
            throw new ParkingException("The arrival time cannot be more than 7 days ago.");

        var vehicle = Vehicle.Create(type, plate);
        ParkingSpot spot;
        if (spotId is null)
        {
            spot = FindBestSpot(type)
                ?? throw new ParkingException($"Sorry, there is no free spot big enough for a {type}.");
        }
        else
        {
            spot = GetSpot(spotId) ?? throw new ParkingException($"Spot {spotId} does not exist.");
            if (!IsFree(spot.Id))
                throw new ParkingException($"Spot {spot.Id} is already taken.");
            if (!vehicle.FitsIn(spot.Size))
                throw new ParkingException(
                    $"A {type} needs a {vehicle.MinimumSpotSize} spot or bigger, but {spot.Id} is {spot.Size}.\n\n" +
                    "Pick another spot, or click the selected spot again to let the system choose.");
        }

        var record = new ParkingRecord
        {
            Ticket = Records.Count == 0 ? 1001 : Records.Max(r => r.Ticket) + 1,
            Plate = plate,
            VehicleType = type,
            SpotId = spot.Id,
            EntryTime = entryTime > now ? now : entryTime
        };
        Records.Add(record);
        return record;
    }

    public ParkingRecord CheckOut(string spotId, DateTime exitTime)
    {
        var record = ActiveRecordAt(spotId) ?? throw new ParkingException($"Spot {spotId} is already free.");
        if (exitTime < record.EntryTime) exitTime = record.EntryTime;
        record.ExitTime = exitTime;
        record.Fee = record.ToVehicle().CalculateFee(exitTime - record.EntryTime);
        return record;
    }

    public List<ParkingRecord> CompletedOn(DateTime day) =>
        Records.Where(r => r.ExitTime?.Date == day.Date).OrderBy(r => r.ExitTime).ToList();

    public decimal RevenueOn(DateTime day) => CompletedOn(day).Sum(r => r.Fee);

    public TimeSpan AverageStayOn(DateTime day)
    {
        var done = CompletedOn(day);
        return done.Count == 0 ? TimeSpan.Zero : TimeSpan.FromMinutes(done.Average(r => r.DurationAt(day).TotalMinutes));
    }

    public string BuildDailyReport(DateTime now)
    {
        var done = CompletedOn(now);
        var sb = new StringBuilder();
        sb.AppendLine("SMART PARKING LOT - DAILY REPORT");
        sb.AppendLine(new string('=', 64));
        sb.AppendLine($"Date: {now:dddd d MMMM yyyy}          Generated at {now:HH:mm}");
        sb.AppendLine();

        sb.AppendLine("SUMMARY");
        sb.AppendLine($"  Vehicles checked out today : {done.Count}");
        sb.AppendLine($"  Revenue today              : {Formatting.Money(done.Sum(r => r.Fee))}");
        sb.AppendLine($"  Currently parked           : {spots.Count - FreeSpotCount} of {spots.Count} spots ({OccupancyPercent:0}%)");
        sb.AppendLine($"  Average stay               : {Formatting.Duration(AverageStayOn(now))}");

        var busiest = Records
            .Where(r => r.EntryTime.Date == now.Date)
            .GroupBy(r => r.EntryTime.Hour)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();
        if (busiest is not null)
            sb.AppendLine($"  Busiest arrival hour       : {busiest.Key:00}:00-{busiest.Key + 1:00}:00 ({busiest.Count()} arrivals)");
        sb.AppendLine();

        sb.AppendLine("BY VEHICLE TYPE");
        sb.AppendLine($"  {"Type",-8}{"Visits",8}{"Revenue",12}{"Avg stay",12}");
        foreach (VehicleType type in Enum.GetValues<VehicleType>())
        {
            var ofType = done.Where(r => r.VehicleType == type).ToList();
            var avg = ofType.Count == 0 ? TimeSpan.Zero : TimeSpan.FromMinutes(ofType.Average(r => r.DurationAt(now).TotalMinutes));
            sb.AppendLine($"  {type,-8}{ofType.Count,8}{Formatting.Money(ofType.Sum(r => r.Fee)),12}{Formatting.Duration(avg),12}");
        }
        sb.AppendLine();

        sb.AppendLine("COMPLETED TICKETS");
        sb.AppendLine($"  {"Ticket",-8}{"Plate",-12}{"Type",-7}{"Spot",-6}{"In",-7}{"Out",-7}{"Stay",-9}{"Fee",8}");
        foreach (var r in done)
        {
            sb.AppendLine($"  {r.Ticket,-8}{r.Plate,-12}{r.VehicleType,-7}{r.SpotId,-6}{r.EntryTime,-7:HH:mm}{r.ExitTime,-7:HH:mm}" +
                          $"{Formatting.Duration(r.DurationAt(now)),-9}{Formatting.Money(r.Fee),8}");
        }
        if (done.Count == 0) sb.AppendLine("  (none yet)");
        return sb.ToString();
    }
}
