namespace ParkingLotManager;

public enum VehicleType { Bike, Car, Truck }

// Order matters: a bigger spot can always hold a smaller vehicle.
public enum SpotSize { Small, Regular, Large }

public record FeeLine(string Description, decimal Amount);

/// <summary>
/// Base class for every vehicle. Each subclass sets its own rates and the
/// smallest spot it fits in. Fees are worked out here so all vehicles share
/// the same rules (grace period, per started hour, daily maximum).
/// </summary>
public abstract class Vehicle
{
    public static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(15);

    protected Vehicle(string plate) => Plate = plate;

    public string Plate { get; }
    public abstract VehicleType Type { get; }
    public abstract SpotSize MinimumSpotSize { get; }
    public abstract decimal HourlyRate { get; }
    public abstract decimal DailyMaximum { get; }

    public bool FitsIn(SpotSize spotSize) => spotSize >= MinimumSpotSize;

    public virtual List<FeeLine> GetFeeLines(TimeSpan duration)
    {
        var lines = new List<FeeLine>();
        if (duration <= GracePeriod)
        {
            lines.Add(new FeeLine($"Grace period (under {GracePeriod.TotalMinutes:0} min)", 0m));
            return lines;
        }

        int fullDays = (int)duration.TotalDays;
        if (fullDays > 0)
            lines.Add(new FeeLine($"{fullDays} full day(s) x {Formatting.Money(DailyMaximum)}", fullDays * DailyMaximum));

        TimeSpan rest = duration - TimeSpan.FromDays(fullDays);
        int startedHours = (int)Math.Ceiling(rest.TotalHours);
        if (startedHours > 0)
        {
            decimal hourly = startedHours * HourlyRate;
            if (hourly > DailyMaximum)
                lines.Add(new FeeLine($"{startedHours} h (daily max)", DailyMaximum));
            else
                lines.Add(new FeeLine($"{startedHours} h x {Formatting.Money(HourlyRate)}", hourly));
        }
        return lines;
    }

    public decimal CalculateFee(TimeSpan duration) => GetFeeLines(duration).Sum(line => line.Amount);

    public static Vehicle Create(VehicleType type, string plate) => type switch
    {
        VehicleType.Bike => new Bike(plate),
        VehicleType.Car => new Car(plate),
        VehicleType.Truck => new Truck(plate),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}

public class Bike : Vehicle
{
    public Bike(string plate) : base(plate) { }
    public override VehicleType Type => VehicleType.Bike;
    public override SpotSize MinimumSpotSize => SpotSize.Small;
    public override decimal HourlyRate => 1.00m;
    public override decimal DailyMaximum => 6.00m;
}

public class Car : Vehicle
{
    public Car(string plate) : base(plate) { }
    public override VehicleType Type => VehicleType.Car;
    public override SpotSize MinimumSpotSize => SpotSize.Regular;
    public override decimal HourlyRate => 2.50m;
    public override decimal DailyMaximum => 20.00m;
}

public class Truck : Vehicle
{
    public const decimal HeavySurcharge = 3.00m;

    public Truck(string plate) : base(plate) { }
    public override VehicleType Type => VehicleType.Truck;
    public override SpotSize MinimumSpotSize => SpotSize.Large;
    public override decimal HourlyRate => 5.00m;
    public override decimal DailyMaximum => 40.00m;

    // Trucks pay a one-time surcharge on top of the normal fee (polymorphism).
    public override List<FeeLine> GetFeeLines(TimeSpan duration)
    {
        var lines = base.GetFeeLines(duration);
        if (duration > GracePeriod)
            lines.Add(new FeeLine("Heavy vehicle surcharge", HeavySurcharge));
        return lines;
    }
}
