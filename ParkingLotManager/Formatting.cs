using System.Globalization;

namespace ParkingLotManager;

public static class Formatting
{
    // Change this to your local currency symbol if needed (for example "Rs " or "AED ").
    public const string CurrencySymbol = "$";

    public static string Money(decimal amount) =>
        CurrencySymbol + amount.ToString("0.00", CultureInfo.InvariantCulture);

    public static string Duration(TimeSpan span)
    {
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        if (span.TotalDays >= 1) return $"{(int)span.TotalDays}d {span.Hours}h";
        if (span.TotalHours >= 1) return $"{(int)span.TotalHours}h {span.Minutes:00}m";
        return $"{span.Minutes}m";
    }
}
