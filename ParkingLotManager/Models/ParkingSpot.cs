namespace ParkingLotManager;

public class ParkingSpot
{
    public ParkingSpot(string row, int number, SpotSize size)
    {
        Row = row;
        Number = number;
        Size = size;
    }

    public string Row { get; }
    public int Number { get; }
    public SpotSize Size { get; }
    public string Id => $"{Row}{Number}";
}
