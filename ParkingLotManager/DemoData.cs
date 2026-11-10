namespace ParkingLotManager;

/// <summary>Fills the lot with a realistic day so the app can be presented quickly.</summary>
public static class DemoData
{
    private record Visit(string Plate, VehicleType Type, DateTime Entry, DateTime? Exit);

    public static void Fill(ParkingLot lot, DateTime now)
    {
        lot.Records.Clear();
        var random = new Random(42);
        var visits = new List<Visit>();

        // Vehicles that already came and left today.
        for (int i = 0; i < 16; i++)
        {
            DateTime entry = now.AddMinutes(-random.Next(90, 540));
            DateTime exit = entry.AddMinutes(random.Next(10, 200));
            if (exit > now.AddMinutes(-5)) exit = now.AddMinutes(-5);
            visits.Add(new Visit(RandomPlate(random), RandomType(random), entry, exit));
        }

        // Vehicles still parked right now.
        for (int i = 0; i < 15; i++)
            visits.Add(new Visit(RandomPlate(random), RandomType(random), now.AddMinutes(-random.Next(3, 330)), null));

        // Replay every arrival and departure in time order, so spots are reused realistically.
        var events = visits
            .SelectMany(v => v.Exit is { } exit
                ? new[] { (Time: v.Entry, Arrive: true, Visit: v), (Time: exit, Arrive: false, Visit: v) }
                : new[] { (Time: v.Entry, Arrive: true, Visit: v) })
            .OrderBy(e => e.Time)
            .ToList();

        var spotOf = new Dictionary<Visit, string>();
        foreach (var (time, arrive, visit) in events)
        {
            try
            {
                if (arrive)
                    spotOf[visit] = lot.CheckIn(visit.Plate, visit.Type, null, time, now).SpotId;
                else if (spotOf.TryGetValue(visit, out var spotId))
                    lot.CheckOut(spotId, time);
            }
            catch (ParkingException)
            {
                // Lot full for that type, or a duplicate random plate: skip this visit.
            }
        }
    }

    private static VehicleType RandomType(Random random)
    {
        int roll = random.Next(100);
        return roll < 22 ? VehicleType.Bike : roll < 85 ? VehicleType.Car : VehicleType.Truck;
    }

    private static string RandomPlate(Random random)
    {
        const string letters = "ABCDEFGHJKLMNPRSTUVWXYZ";
        var prefix = new string(Enumerable.Range(0, 3).Select(_ => letters[random.Next(letters.Length)]).ToArray());
        return $"{prefix}-{random.Next(100, 9999)}";
    }
}
