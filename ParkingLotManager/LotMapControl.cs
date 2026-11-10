using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace ParkingLotManager;

public static class LotColors
{
    public static readonly Color Asphalt = Color.FromArgb(48, 54, 62);
    public static readonly Color Lane = Color.FromArgb(58, 65, 74);
    public static readonly Color Paint = Color.FromArgb(230, 234, 238);
    public static readonly Color LaneLine = Color.FromArgb(244, 196, 48);
    public static readonly Color Free = Color.FromArgb(39, 150, 86);
    public static readonly Color FreeHover = Color.FromArgb(52, 178, 106);
    public static readonly Color JustParked = Color.FromArgb(232, 112, 104);
    public static readonly Color LongStay = Color.FromArgb(150, 24, 24);
    public static readonly Color Selected = Color.FromArgb(79, 195, 247);
    public static readonly Color Found = Color.FromArgb(255, 213, 79);

    // Hours after which an occupied spot is drawn in the darkest red.
    public const double LongStayHours = 4;

    /// <summary>The longer a vehicle stays, the darker its spot ("stay heat").</summary>
    public static Color ForStay(TimeSpan stay)
    {
        double t = Math.Clamp(stay.TotalHours / LongStayHours, 0, 1);
        return Color.FromArgb(
            (int)(JustParked.R + (LongStay.R - JustParked.R) * t),
            (int)(JustParked.G + (LongStay.G - JustParked.G) * t),
            (int)(JustParked.B + (LongStay.B - JustParked.B) * t));
    }
}

/// <summary>
/// Custom control that draws the parking lot map with GDI+ and reports
/// which spot the user clicked.
/// </summary>
public class LotMapControl : Control
{
    private readonly Dictionary<string, RectangleF> spotBounds = new();
    private readonly Dictionary<string, PointF> rowLabels = new();
    private readonly ToolTip toolTip = new() { InitialDelay = 300, ReshowDelay = 100 };
    private RectangleF laneBounds;
    private string? hoverSpotId;
    private string? selectedSpotId;
    private string? foundSpotId;

    public LotMapControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = LotColors.Asphalt;
    }

    public event EventHandler<string>? SpotClicked;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ParkingLot? Lot { get; set; }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? SelectedSpotId
    {
        get => selectedSpotId;
        set { selectedSpotId = value; Invalidate(); }
    }

    /// <summary>Spot found by the plate search; drawn with a gold outline.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? FoundSpotId
    {
        get => foundSpotId;
        set { foundSpotId = value; Invalidate(); }
    }

    private static float Units(SpotSize size) => size switch
    {
        SpotSize.Small => 0.8f,
        SpotSize.Regular => 1f,
        _ => 1.2f
    };

    private const float LaneUnits = 0.55f;

    private void ComputeLayout()
    {
        spotBounds.Clear();
        rowLabels.Clear();
        laneBounds = RectangleF.Empty;
        if (Lot is null) return;

        const float pad = 14, labelWidth = 30, gap = 8;
        var rows = Lot.Spots.GroupBy(s => s.Row).ToList();
        float totalUnits = rows.Sum(r => Units(r.First().Size)) + LaneUnits;
        float unit = (ClientSize.Height - pad * 2 - gap * rows.Count) / totalUnits;
        float left = pad + labelWidth;
        float width = ClientSize.Width - left - pad;
        if (unit <= 10 || width <= 50) return;

        float y = pad;
        foreach (var row in rows)
        {
            var rowSpots = row.OrderBy(s => s.Number).ToList();
            float h = unit * Units(rowSpots[0].Size);
            float w = (width - gap * (rowSpots.Count - 1)) / rowSpots.Count;
            for (int i = 0; i < rowSpots.Count; i++)
                spotBounds[rowSpots[i].Id] = new RectangleF(left + i * (w + gap), y, w, h);
            rowLabels[row.Key] = new PointF(pad + labelWidth / 2, y + h / 2);
            y += h + gap;

            // The driving lane runs between rows B and C.
            if (row.Key == "B")
            {
                laneBounds = new RectangleF(pad, y, ClientSize.Width - pad * 2, unit * LaneUnits);
                y += laneBounds.Height + gap;
            }
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ComputeLayout();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (spotBounds.Count == 0) ComputeLayout();
        if (Lot is null) return;

        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        DateTime now = DateTime.Now;

        DrawLane(g);

        using var rowFont = new Font("Segoe UI", 15, FontStyle.Bold, GraphicsUnit.Pixel);
        using var paintBrush = new SolidBrush(LotColors.Paint);
        using var centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        foreach (var (row, point) in rowLabels)
            g.DrawString(row, rowFont, paintBrush, point, centered);

        foreach (var spot in Lot.Spots)
        {
            if (spotBounds.TryGetValue(spot.Id, out var bounds))
                DrawSpot(g, spot, bounds, Lot.ActiveRecordAt(spot.Id), now);
        }
    }

    private void DrawLane(Graphics g)
    {
        if (laneBounds.IsEmpty) return;
        using (var laneBrush = new SolidBrush(LotColors.Lane))
        using (var lanePath = RoundedRect(laneBounds, 10))
            g.FillPath(laneBrush, lanePath);

        float midY = laneBounds.Y + laneBounds.Height / 2;
        using (var dashed = new Pen(LotColors.LaneLine, 3) { DashPattern = new[] { 4f, 3f } })
            g.DrawLine(dashed, laneBounds.X + 110, midY, laneBounds.Right - 110, midY);

        float fontPx = Math.Clamp(laneBounds.Height * 0.32f, 10, 16);
        using var font = new Font("Segoe UI", fontPx, FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(LotColors.Paint);
        using var leftFormat = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
        using var rightFormat = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
        var inner = RectangleF.Inflate(laneBounds, -14, 0);
        g.DrawString("ENTRY ▶", font, brush, inner, leftFormat);
        g.DrawString("▶ EXIT", font, brush, inner, rightFormat);
    }

    private void DrawSpot(Graphics g, ParkingSpot spot, RectangleF r, ParkingRecord? record, DateTime now)
    {
        bool hover = spot.Id == hoverSpotId;
        Color fill = record is null
            ? (hover ? LotColors.FreeHover : LotColors.Free)
            : LotColors.ForStay(record.DurationAt(now));
        if (record is not null && hover) fill = ControlPaint.Light(fill, 0.25f);

        float radius = Math.Min(8, Math.Min(r.Width, r.Height) / 4);
        using (var path = RoundedRect(r, radius))
        {
            using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);

            Color border = LotColors.Paint;
            float borderWidth = 1.5f;
            if (spot.Id == foundSpotId) { border = LotColors.Found; borderWidth = 4; }
            if (spot.Id == selectedSpotId) { border = LotColors.Selected; borderWidth = 4; }
            using var pen = new Pen(border, borderWidth);
            g.DrawPath(pen, path);
        }

        float idPx = Math.Clamp(r.Height * 0.16f, 10, 15);
        float mainPx = Math.Clamp(r.Height * 0.21f, 11, 19);
        float smallPx = Math.Clamp(r.Height * 0.14f, 9, 13);
        using var idFont = new Font("Segoe UI", idPx, FontStyle.Bold, GraphicsUnit.Pixel);
        using var mainFont = new Font("Segoe UI", mainPx, FontStyle.Bold, GraphicsUnit.Pixel);
        using var smallFont = new Font("Segoe UI", smallPx, FontStyle.Regular, GraphicsUnit.Pixel);
        using var white = new SolidBrush(Color.White);
        using var faded = new SolidBrush(Color.FromArgb(215, Color.White));
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };

        var idRect = new RectangleF(r.X + 6, r.Y + 3, r.Width - 12, idFont.Height + 2);
        using (var nearFormat = new StringFormat(format) { Alignment = StringAlignment.Near })
            g.DrawString(spot.Id, idFont, faded, idRect, nearFormat);

        var mainRect = new RectangleF(r.X + 3, r.Y + r.Height * 0.30f, r.Width - 6, r.Height * 0.36f);
        var bottomRect = new RectangleF(r.X + 3, r.Y + r.Height * 0.66f, r.Width - 6, r.Height * 0.30f);
        if (record is null)
        {
            g.DrawString("FREE", mainFont, white, mainRect, format);
            g.DrawString(spot.Size.ToString(), smallFont, faded, bottomRect, format);
        }
        else
        {
            g.DrawString(record.Plate, mainFont, white, mainRect, format);
            g.DrawString($"{record.VehicleType} · {Formatting.Duration(record.DurationAt(now))}", smallFont, faded, bottomRect, format);
        }
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        float d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private string? HitTest(Point point)
    {
        foreach (var (id, bounds) in spotBounds)
            if (bounds.Contains(point)) return id;
        return null;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        string? id = HitTest(e.Location);
        Cursor = id is null ? Cursors.Default : Cursors.Hand;
        if (id == hoverSpotId) return;
        hoverSpotId = id;
        toolTip.SetToolTip(this, id is null ? "" : TooltipFor(id));
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hoverSpotId = null;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left && HitTest(e.Location) is { } id)
            SpotClicked?.Invoke(this, id);
    }

    private string TooltipFor(string spotId)
    {
        if (Lot?.GetSpot(spotId) is not { } spot) return "";
        var record = Lot.ActiveRecordAt(spotId);
        if (record is null) return $"{spot.Id} · {spot.Size} spot · free";
        TimeSpan stay = record.DurationAt(DateTime.Now);
        return $"{spot.Id} · {record.Plate} ({record.VehicleType})\n" +
               $"Parked since {record.EntryTime:HH:mm} ({Formatting.Duration(stay)})\n" +
               $"Fee so far: {Formatting.Money(record.ToVehicle().CalculateFee(stay))}";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) toolTip.Dispose();
        base.Dispose(disposing);
    }
}
