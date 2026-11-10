using System.Text.Json;
using System.Text.Json.Serialization;

namespace ParkingLotManager;

public static class LotStorage
{
    private static readonly string Folder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ParkingLotManager");
    public static readonly string FilePath = Path.Combine(Folder, "records.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static List<ParkingRecord> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new List<ParkingRecord>();
            string json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<ParkingRecord>>(json, Options) ?? new List<ParkingRecord>();
        }
        catch
        {
            return new List<ParkingRecord>();
        }
    }

    public static void Save(List<ParkingRecord> records)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(records, Options));
    }
}
