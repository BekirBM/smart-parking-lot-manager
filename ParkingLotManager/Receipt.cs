using System.Text;

namespace ParkingLotManager;

public static class Receipt
{
    private const int Width = 38;

    public static string Build(ParkingRecord record, ParkingSpot? spot, DateTime exitTime)
    {
        var vehicle = record.ToVehicle();
        TimeSpan duration = exitTime - record.EntryTime;
        var lines = vehicle.GetFeeLines(duration);

        var sb = new StringBuilder();
        sb.AppendLine(Center("SMART PARKING LOT"));
        sb.AppendLine(Center("Parking receipt"));
        sb.AppendLine(new string('-', Width));
        sb.AppendLine(Row("Ticket", $"#{record.Ticket}"));
        sb.AppendLine(Row("Plate", record.Plate));
        sb.AppendLine(Row("Vehicle", record.VehicleType.ToString()));
        sb.AppendLine(Row("Spot", spot is null ? record.SpotId : $"{spot.Id} ({spot.Size})"));
        sb.AppendLine(Row("In", record.EntryTime.ToString("dd MMM yyyy HH:mm")));
        sb.AppendLine(Row("Out", exitTime.ToString("dd MMM yyyy HH:mm")));
        sb.AppendLine(Row("Duration", Formatting.Duration(duration)));
        sb.AppendLine(new string('-', Width));
        foreach (var line in lines)
            sb.AppendLine(Row(line.Description, Formatting.Money(line.Amount)));
        sb.AppendLine(new string('=', Width));
        sb.AppendLine(Row("TOTAL", Formatting.Money(lines.Sum(l => l.Amount))));
        sb.AppendLine(new string('=', Width));
        sb.AppendLine();
        sb.Append(Center("Thank you, drive safely!"));
        return sb.ToString();
    }

    private static string Row(string left, string right)
    {
        int space = Width - right.Length;
        if (left.Length > space - 1) left = left[..Math.Max(0, space - 2)] + ".";
        return left.PadRight(space) + right;
    }

    private static string Center(string text) => text.PadLeft((Width + text.Length) / 2);
}
