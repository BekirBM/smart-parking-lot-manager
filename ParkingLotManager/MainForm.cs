namespace ParkingLotManager;

public class MainForm : Form
{
    private readonly ParkingLot lot = new(LotStorage.Load());
    private readonly LotMapControl map = new();
    private readonly TextBox plateBox = new();
    private readonly ComboBox typeBox = new();
    private readonly CheckBox arrivedNowCheck = new();
    private readonly DateTimePicker arrivalPicker = new();
    private readonly Label targetSpotLabel = new();
    private readonly Button checkInButton = new();
    private readonly Label spotInfoLabel = new();
    private readonly Button checkOutButton = new();
    private readonly TextBox searchBox = new();
    private readonly Label occupancyLabel = new();
    private readonly ProgressBar occupancyBar = new();
    private readonly Label statsLabel = new();
    private readonly Label statsValuesLabel = new();
    private readonly DataGridView parkedGrid = new();
    private readonly DataGridView historyGrid = new();
    private readonly TabPage parkedPage = new();
    private readonly TabPage historyPage = new();
    private readonly Label clockLabel = new();
    private readonly ToolStripStatusLabel statusLabel = new();
    private readonly System.Windows.Forms.Timer clockTimer = new() { Interval = 1000 };
    private string? selectedSpotId;
    private int lastMinute = -1;

    private static readonly Color PageBack = Color.FromArgb(242, 244, 247);
    private static readonly Color Ink = Color.FromArgb(33, 37, 41);

    public MainForm()
    {
        Text = "Smart Parking Lot Manager";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1100, 760);
        Size = new Size(1320, 860);
        Font = new Font("Segoe UI", 10);
        BackColor = PageBack;
        KeyPreview = true;
        DoubleBuffered = true;

        map.Lot = lot;
        BuildLayout();
        RefreshAll();

        clockTimer.Tick += (_, _) => OnClockTick();
        clockTimer.Start();
        OnClockTick();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ClearGridSelection();
        plateBox.Focus();
        SetStatus(lot.Records.Count == 0
            ? "Welcome! Type a plate and press Enter to park a vehicle, or use Tools > Load demo data."
            : $"Loaded {lot.Records.Count} records. {lot.FreeSpotCount} spots are free.");
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape)
        {
            SelectSpot(null);
            e.Handled = true;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        clockTimer.Stop();
        base.OnFormClosing(e);
    }

    // ------------------------------------------------------------------ layout

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14, 8, 14, 8), ColumnCount = 2, RowCount = 2 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 350));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.SetColumnSpan(root.GetControlFromPosition(0, 0)!, 2);
        root.Controls.Add(BuildLeftSide(), 0, 1);
        root.Controls.Add(BuildSidePanel(), 1, 1);

        // Order matters: the Fill control must be added first so the menu and status bar dock around it.
        Controls.Add(root);
        Controls.Add(BuildStatusBar());
        Controls.Add(BuildMenu());
    }

    private Control BuildHeader()
    {
        var header = new Panel { Dock = DockStyle.Fill };
        var title = new Label
        {
            Text = "Smart Parking Lot",
            Font = new Font("Segoe UI", 20, FontStyle.Bold),
            ForeColor = Ink,
            AutoSize = true,
            Location = new Point(0, 4)
        };
        var subtitle = new Label
        {
            Text = "Click a spot to select it  ·  Enter to check in  ·  Esc to clear",
            ForeColor = Color.DimGray,
            AutoSize = true,
            Location = new Point(270, 18)
        };
        clockLabel.AutoSize = true;
        clockLabel.Font = new Font("Segoe UI", 13, FontStyle.Bold);
        clockLabel.ForeColor = Ink;
        clockLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        header.Controls.AddRange(new Control[] { title, subtitle, clockLabel });
        header.Resize += (_, _) => clockLabel.Location = new Point(header.ClientSize.Width - clockLabel.Width, 14);
        clockLabel.SizeChanged += (_, _) => clockLabel.Location = new Point(header.ClientSize.Width - clockLabel.Width, 14);
        return header;
    }

    private Control BuildLeftSide()
    {
        var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0, 0, 10, 0) };
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 38));

        map.Dock = DockStyle.Fill;
        map.AccessibleName = "Parking map";
        map.Margin = new Padding(0, 0, 0, 8);
        map.SpotClicked += (_, id) => SelectSpot(id == selectedSpotId ? null : id);
        left.Controls.Add(map, 0, 0);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        parkedPage.Text = "Currently parked";
        historyPage.Text = "Today's check-outs";
        SetUpGrid(parkedGrid, "Ticket", "Plate", "Type", "Spot", "Arrived", "Stay", "Fee so far");
        SetUpGrid(historyGrid, "Ticket", "Plate", "Type", "Spot", "In", "Out", "Stay", "Paid");
        parkedPage.Controls.Add(parkedGrid);
        historyPage.Controls.Add(historyGrid);
        tabs.TabPages.Add(parkedPage);
        tabs.TabPages.Add(historyPage);
        parkedGrid.CellClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && parkedGrid.Rows[e.RowIndex].Tag is ParkingRecord record)
                SelectSpot(record.SpotId, fromGrid: true);
        };
        left.Controls.Add(tabs, 0, 1);
        return left;
    }

    private static void SetUpGrid(DataGridView grid, params string[] columns)
    {
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.None;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(230, 234, 239);
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersHeight = 32;
        grid.RowTemplate.Height = 28;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 251);
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(205, 232, 250);
        grid.DefaultCellStyle.SelectionForeColor = Ink;
        foreach (string column in columns)
            grid.Columns.Add(column.Replace(" ", ""), column);
    }

    private Control BuildSidePanel()
    {
        var side = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = new Padding(0) };
        side.RowStyles.Add(new RowStyle(SizeType.Absolute, 232));
        side.RowStyles.Add(new RowStyle(SizeType.Absolute, 172));
        side.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        side.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        side.Controls.Add(BuildCheckInBox(), 0, 0);
        side.Controls.Add(BuildSelectedSpotBox(), 0, 1);
        side.Controls.Add(BuildSearchBox(), 0, 2);
        side.Controls.Add(BuildStatsBox(), 0, 3);
        return side;
    }

    private Control BuildCheckInBox()
    {
        var box = new GroupBox { Text = "Check in a vehicle", Dock = DockStyle.Fill, Padding = new Padding(10, 6, 10, 8) };
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6 };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 5; i++) grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        plateBox.Dock = DockStyle.Fill;
        plateBox.AccessibleName = "Plate number";
        plateBox.CharacterCasing = CharacterCasing.Upper;
        plateBox.MaxLength = 10;
        plateBox.PlaceholderText = "e.g. ABC-1234";
        plateBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            CheckIn();
        };

        typeBox.Dock = DockStyle.Fill;
        typeBox.DropDownStyle = ComboBoxStyle.DropDownList;
        typeBox.FormattingEnabled = true;
        foreach (VehicleType type in Enum.GetValues<VehicleType>()) typeBox.Items.Add(type);
        typeBox.Format += (_, e) =>
        {
            if (e.ListItem is VehicleType type)
                e.Value = $"{type}  ({Formatting.Money(Vehicle.Create(type, "X").HourlyRate)}/h)";
        };
        typeBox.SelectedItem = VehicleType.Car;
        typeBox.SelectedIndexChanged += (_, _) => RefreshSelection();

        arrivedNowCheck.Text = "Just now";
        arrivedNowCheck.Checked = true;
        arrivedNowCheck.AutoSize = true;
        arrivedNowCheck.CheckedChanged += (_, _) =>
        {
            arrivalPicker.Enabled = !arrivedNowCheck.Checked;
            if (!arrivedNowCheck.Checked) arrivalPicker.Value = DateTime.Now.AddMinutes(-30);
        };

        arrivalPicker.Dock = DockStyle.Fill;
        arrivalPicker.Format = DateTimePickerFormat.Custom;
        arrivalPicker.CustomFormat = "dd MMM  HH:mm";
        arrivalPicker.Enabled = false;

        targetSpotLabel.Dock = DockStyle.Fill;
        targetSpotLabel.TextAlign = ContentAlignment.MiddleLeft;
        targetSpotLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);

        StyleButton(checkInButton, "Check in", LotColors.Free);
        checkInButton.Dock = DockStyle.Top;
        checkInButton.Height = 38;
        checkInButton.Margin = new Padding(3, 6, 3, 0);
        checkInButton.Click += (_, _) => CheckIn();

        grid.Controls.Add(MakeFieldLabel("Plate"), 0, 0);
        grid.Controls.Add(plateBox, 1, 0);
        grid.Controls.Add(MakeFieldLabel("Vehicle"), 0, 1);
        grid.Controls.Add(typeBox, 1, 1);
        grid.Controls.Add(MakeFieldLabel("Arrived"), 0, 2);
        grid.Controls.Add(arrivedNowCheck, 1, 2);
        grid.Controls.Add(arrivalPicker, 1, 3);
        grid.Controls.Add(MakeFieldLabel("Spot"), 0, 4);
        grid.Controls.Add(targetSpotLabel, 1, 4);
        grid.Controls.Add(checkInButton, 0, 5);
        grid.SetColumnSpan(checkInButton, 2);
        box.Controls.Add(grid);
        return box;
    }

    private Control BuildSelectedSpotBox()
    {
        var box = new GroupBox { Text = "Selected spot", Dock = DockStyle.Fill, Padding = new Padding(10, 6, 10, 8) };
        spotInfoLabel.Dock = DockStyle.Fill;
        spotInfoLabel.ForeColor = Ink;
        StyleButton(checkOutButton, "Check out && take payment", Color.FromArgb(200, 55, 50));
        checkOutButton.Dock = DockStyle.Bottom;
        checkOutButton.Height = 38;
        checkOutButton.Click += (_, _) => CheckOutSelected();
        box.Controls.Add(spotInfoLabel);
        box.Controls.Add(checkOutButton);
        return box;
    }

    private Control BuildSearchBox()
    {
        var box = new GroupBox { Text = "Find a vehicle", Dock = DockStyle.Fill, Padding = new Padding(10, 4, 10, 6) };
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        searchBox.Dock = DockStyle.Fill;
        searchBox.AccessibleName = "Search plate";
        searchBox.CharacterCasing = CharacterCasing.Upper;
        searchBox.PlaceholderText = "Plate number";
        searchBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            FindVehicle();
        };
        var findButton = new Button { Text = "Find", Dock = DockStyle.Fill, Margin = new Padding(6, 0, 0, 0) };
        findButton.Click += (_, _) => FindVehicle();
        row.Controls.Add(searchBox, 0, 0);
        row.Controls.Add(findButton, 1, 0);
        box.Controls.Add(row);
        return box;
    }

    private Control BuildStatsBox()
    {
        var box = new GroupBox { Text = "Today at a glance", Dock = DockStyle.Fill, Padding = new Padding(10, 6, 10, 8) };
        var stack = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        stack.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        occupancyLabel.Dock = DockStyle.Fill;
        occupancyLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        occupancyBar.Dock = DockStyle.Fill;
        occupancyBar.Maximum = 100;
        statsLabel.Dock = DockStyle.Fill;
        statsLabel.ForeColor = Color.DimGray;
        statsLabel.Text = "Revenue collected\nStill to collect\nCheck-outs today\nAverage stay\nFree for bike / car / truck";
        statsValuesLabel.Dock = DockStyle.Fill;
        statsValuesLabel.ForeColor = Ink;
        statsValuesLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        var numbers = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(0, 4, 0, 0) };
        numbers.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        numbers.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        numbers.Controls.Add(statsLabel, 0, 0);
        numbers.Controls.Add(statsValuesLabel, 1, 0);

        var legend = new Panel { Dock = DockStyle.Fill };
        legend.Paint += (_, e) => DrawLegend(e.Graphics);

        stack.Controls.Add(occupancyLabel, 0, 0);
        stack.Controls.Add(occupancyBar, 0, 1);
        stack.Controls.Add(numbers, 0, 2);
        stack.Controls.Add(legend, 0, 3);
        box.Controls.Add(stack);
        return box;
    }

    private void DrawLegend(Graphics g)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var items = new (string Text, Color Color, bool Outline)[]
        {
            ("Free", LotColors.Free, false),
            ("Just parked", LotColors.JustParked, false),
            ($"{LotColors.LongStayHours:0}h+ stay", LotColors.LongStay, false),
            ("Selected", LotColors.Selected, true),
            ("Search result", LotColors.Found, true)
        };
        using var font = new Font("Segoe UI", 8.5f);
        using var textBrush = new SolidBrush(Color.DimGray);
        float x = 0, y = 2;
        foreach (var (text, color, outline) in items)
        {
            float width = 18 + g.MeasureString(text, font).Width + 10;
            if (x + width > g.VisibleClipBounds.Width) { x = 0; y += 22; }
            var swatch = new RectangleF(x, y + 2, 14, 14);
            if (outline)
            {
                using var pen = new Pen(color, 3);
                g.DrawRectangle(pen, swatch.X + 1.5f, swatch.Y + 1.5f, swatch.Width - 3, swatch.Height - 3);
            }
            else
            {
                using var brush = new SolidBrush(color);
                g.FillRectangle(brush, swatch);
            }
            g.DrawString(text, font, textBrush, x + 18, y);
            x += width;
        }
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip { BackColor = Color.White };

        var file = new ToolStripMenuItem("&File");
        file.DropDownItems.Add(new ToolStripMenuItem("&Export daily report...", null, (_, _) => ExportReport()) { ShortcutKeys = Keys.Control | Keys.E });
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(new ToolStripMenuItem("E&xit", null, (_, _) => Close()));

        var tools = new ToolStripMenuItem("&Tools");
        tools.DropDownItems.Add(new ToolStripMenuItem("Load &demo data", null, (_, _) => LoadDemoData()));
        tools.DropDownItems.Add(new ToolStripMenuItem("&Clear all data...", null, (_, _) => ClearAllData()));

        var help = new ToolStripMenuItem("&Help");
        help.DropDownItems.Add(new ToolStripMenuItem("&Rates and rules", null, (_, _) => ShowRates()));
        help.DropDownItems.Add(new ToolStripMenuItem("&About", null, (_, _) =>
            MessageBox.Show(this, "Smart Parking Lot Manager\nC# Windows Forms project (.NET 8)\n\n" +
                                  $"Data file:\n{LotStorage.FilePath}", "About", MessageBoxButtons.OK, MessageBoxIcon.Information)));

        menu.Items.AddRange(new ToolStripItem[] { file, tools, help });
        MainMenuStrip = menu;
        return menu;
    }

    private StatusStrip BuildStatusBar()
    {
        var strip = new StatusStrip { BackColor = Color.White, SizingGrip = false };
        statusLabel.Spring = true;
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        strip.Items.Add(statusLabel);
        return strip;
    }

    private static Label MakeFieldLabel(string text) =>
        new() { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.DimGray };

    private static void StyleButton(Button button, string text, Color color)
    {
        button.Text = text;
        button.BackColor = color;
        button.ForeColor = Color.White;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
    }

    // ------------------------------------------------------------------ actions

    private void CheckIn()
    {
        if (typeBox.SelectedItem is not VehicleType type) return;
        DateTime now = DateTime.Now;
        DateTime arrival = arrivedNowCheck.Checked ? now : arrivalPicker.Value;
        // Use the selected spot only if it is free; otherwise let the lot choose the best fit.
        string? target = selectedSpotId is not null && lot.IsFree(selectedSpotId) ? selectedSpotId : null;

        try
        {
            var record = lot.CheckIn(plateBox.Text, type, target, arrival, now);
            Save();
            plateBox.Clear();
            arrivedNowCheck.Checked = true;
            map.FoundSpotId = null;
            SelectSpot(record.SpotId);
            SetStatus($"Ticket #{record.Ticket}: {record.Plate} ({record.VehicleType}) parked in spot {record.SpotId}.");
            plateBox.Focus();
        }
        catch (ParkingException ex)
        {
            MessageBox.Show(this, ex.Message, "Cannot check in", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            plateBox.Focus();
            plateBox.SelectAll();
        }
    }

    private void CheckOutSelected()
    {
        if (selectedSpotId is null || lot.ActiveRecordAt(selectedSpotId) is not { } record)
        {
            MessageBox.Show(this, "Select an occupied (red) spot first.", "Check out", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        DateTime exitTime = DateTime.Now;
        using var dialog = new ReceiptForm(record, lot.GetSpot(record.SpotId), exitTime);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        lot.CheckOut(record.SpotId, exitTime);
        Save();
        RefreshAll();
        SetStatus($"{record.Plate} left spot {record.SpotId}. Collected {Formatting.Money(record.Fee)}.");
    }

    private void FindVehicle()
    {
        string plate = ParkingLot.NormalizePlate(searchBox.Text);
        if (plate.Length == 0) return;

        if (lot.FindActiveByPlate(plate) is { } record)
        {
            SelectSpot(record.SpotId);
            map.FoundSpotId = record.SpotId;
            SetStatus($"Found {plate} in spot {record.SpotId} (parked {Formatting.Duration(record.DurationAt(DateTime.Now))}).");
            return;
        }

        map.FoundSpotId = null;
        var last = lot.FindLastVisit(plate);
        string message = last?.ExitTime is { } exit
            ? $"{plate} is not in the lot. It last left spot {last.SpotId} on {exit:dd MMM} at {exit:HH:mm} and paid {Formatting.Money(last.Fee)}."
            : $"No vehicle with plate {plate} has ever parked here.";
        SetStatus(message);
        MessageBox.Show(this, message, "Find a vehicle", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ExportReport()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "Text file (*.txt)|*.txt",
            FileName = $"parking-report-{DateTime.Now:yyyy-MM-dd}.txt"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            File.WriteAllText(dialog.FileName, lot.BuildDailyReport(DateTime.Now));
            SetStatus($"Report saved to {dialog.FileName}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not save report", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LoadDemoData()
    {
        if (lot.Records.Count > 0 &&
            MessageBox.Show(this, "Replace all current records with demo data?", "Load demo data",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        DemoData.Fill(lot, DateTime.Now);
        Save();
        SelectSpot(null);
        map.FoundSpotId = null;
        SetStatus($"Demo data loaded: {lot.ActiveRecords.Count()} vehicles parked, {lot.CompletedOn(DateTime.Now).Count} check-outs today.");
    }

    private void ClearAllData()
    {
        if (MessageBox.Show(this, "Delete every parking record? This cannot be undone.", "Clear all data",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        lot.Records.Clear();
        Save();
        SelectSpot(null);
        map.FoundSpotId = null;
        SetStatus("All records deleted. The lot is empty.");
    }

    private void ShowRates()
    {
        var lines = Enum.GetValues<VehicleType>().Select(type =>
        {
            var v = Vehicle.Create(type, "X");
            string extra = v is Truck ? $" + {Formatting.Money(Truck.HeavySurcharge)} surcharge" : "";
            return $"{type,-6}  {Formatting.Money(v.HourlyRate)}/h, max {Formatting.Money(v.DailyMaximum)}/day{extra}  → needs a {v.MinimumSpotSize} spot or bigger";
        });
        MessageBox.Show(this,
            string.Join("\n", lines) +
            $"\n\n• The first {Vehicle.GracePeriod.TotalMinutes:0} minutes are free." +
            "\n• After that, every started hour is charged." +
            "\n• Auto-assign picks the smallest free spot that fits (best fit).",
            "Rates and rules", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void Save()
    {
        try
        {
            LotStorage.Save(lot.Records);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not save data:\n" + ex.Message, "Save error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ------------------------------------------------------------------ refresh

    private void SelectSpot(string? spotId, bool fromGrid = false)
    {
        selectedSpotId = spotId;
        map.SelectedSpotId = spotId;
        if (spotId != map.FoundSpotId) map.FoundSpotId = null;
        RefreshAll(keepGridSelection: fromGrid);
    }

    private void OnClockTick()
    {
        DateTime now = DateTime.Now;
        clockLabel.Text = now.ToString("ddd d MMM  HH:mm:ss");
        if (now.Minute == lastMinute) return;
        lastMinute = now.Minute;
        RefreshAll(keepGridSelection: true);
    }

    private void RefreshAll(bool keepGridSelection = false)
    {
        map.Invalidate();
        RefreshGrids(keepGridSelection);
        RefreshStats();
        RefreshSelection();
    }

    private void RefreshGrids(bool keepGridSelection)
    {
        DateTime now = DateTime.Now;
        int scroll = parkedGrid.FirstDisplayedScrollingRowIndex;

        parkedGrid.Rows.Clear();
        foreach (var r in lot.ActiveRecords.OrderBy(r => r.SpotId[0]).ThenBy(r => int.Parse(r.SpotId[1..])))
        {
            TimeSpan stay = r.DurationAt(now);
            int index = parkedGrid.Rows.Add(r.Ticket, r.Plate, r.VehicleType, r.SpotId, r.EntryTime.ToString("HH:mm"),
                Formatting.Duration(stay), Formatting.Money(r.ToVehicle().CalculateFee(stay)));
            parkedGrid.Rows[index].Tag = r;
        }

        historyGrid.Rows.Clear();
        foreach (var r in lot.CompletedOn(now).OrderByDescending(r => r.ExitTime))
        {
            historyGrid.Rows.Add(r.Ticket, r.Plate, r.VehicleType, r.SpotId, r.EntryTime.ToString("HH:mm"),
                r.ExitTime!.Value.ToString("HH:mm"), Formatting.Duration(r.DurationAt(now)), Formatting.Money(r.Fee));
        }

        parkedPage.Text = $"Currently parked ({parkedGrid.Rows.Count})";
        historyPage.Text = $"Today's check-outs ({historyGrid.Rows.Count})";

        if (scroll >= 0 && scroll < parkedGrid.Rows.Count) parkedGrid.FirstDisplayedScrollingRowIndex = scroll;
        ClearGridSelection();
        if (selectedSpotId is null) return;
        foreach (DataGridViewRow row in parkedGrid.Rows)
        {
            if (row.Tag is ParkingRecord record && record.SpotId == selectedSpotId)
            {
                row.Selected = true;
                if (!keepGridSelection && !row.Displayed) parkedGrid.FirstDisplayedScrollingRowIndex = row.Index;
            }
        }
    }

    private void ClearGridSelection()
    {
        parkedGrid.ClearSelection();
        historyGrid.ClearSelection();
    }

    private void RefreshStats()
    {
        DateTime now = DateTime.Now;
        int taken = lot.Spots.Count - lot.FreeSpotCount;
        occupancyLabel.Text = $"Occupancy {lot.OccupancyPercent:0}%  ·  {lot.FreeSpotCount} of {lot.Spots.Count} free";
        occupancyBar.Value = (int)Math.Round(lot.OccupancyPercent);

        var done = lot.CompletedOn(now);
        decimal pending = lot.ActiveRecords.Sum(r => r.ToVehicle().CalculateFee(r.DurationAt(now)));
        statsValuesLabel.Text =
            $"{Formatting.Money(done.Sum(r => r.Fee))}\n" +
            $"{Formatting.Money(pending)}\n" +
            $"{done.Count}\n" +
            $"{Formatting.Duration(lot.AverageStayOn(now))}\n" +
            $"{lot.FreeSpotsFor(VehicleType.Bike)} / {lot.FreeSpotsFor(VehicleType.Car)} / {lot.FreeSpotsFor(VehicleType.Truck)}";

        if (taken == lot.Spots.Count) occupancyLabel.ForeColor = Color.Firebrick;
        else occupancyLabel.ForeColor = Ink;
    }

    private void RefreshSelection()
    {
        var type = typeBox.SelectedItem as VehicleType? ?? VehicleType.Car;
        var spot = selectedSpotId is null ? null : lot.GetSpot(selectedSpotId);
        var record = spot is null ? null : lot.ActiveRecordAt(spot.Id);

        // Where will the next check-in go?
        if (spot is not null && record is null)
        {
            bool fits = Vehicle.Create(type, "X").FitsIn(spot.Size);
            targetSpotLabel.Text = fits ? $"{spot.Id} ({spot.Size})" : $"{spot.Id} is too small for a {type}";
            targetSpotLabel.ForeColor = fits ? LotColors.Free : Color.Firebrick;
        }
        else
        {
            var best = lot.FindBestSpot(type);
            targetSpotLabel.Text = best is null ? "No free spot for this type" : $"Auto → {best.Id} (best fit)";
            targetSpotLabel.ForeColor = best is null ? Color.Firebrick : Ink;
        }

        // Selected spot details.
        checkOutButton.Enabled = record is not null;
        checkOutButton.BackColor = record is not null ? Color.FromArgb(200, 55, 50) : Color.FromArgb(190, 190, 190);
        if (spot is null)
        {
            spotInfoLabel.Text = "Click any spot on the map to see its details.\nFree spot: the next check-in goes there.\nRed spot: check the vehicle out.";
            return;
        }

        if (record is null)
        {
            var fitsList = Enum.GetValues<VehicleType>().Where(t => Vehicle.Create(t, "X").FitsIn(spot.Size));
            spotInfoLabel.Text = $"Spot {spot.Id}  ·  {spot.Size}\nStatus:  FREE\nFits:  {string.Join(", ", fitsList)}";
            return;
        }

        TimeSpan stay = record.DurationAt(DateTime.Now);
        spotInfoLabel.Text =
            $"Spot {spot.Id}  ·  {spot.Size}\n" +
            $"Plate:  {record.Plate}  ({record.VehicleType})   Ticket #{record.Ticket}\n" +
            $"Arrived:  {record.EntryTime:HH:mm}  ({Formatting.Duration(stay)} ago)\n" +
            $"Fee so far:  {Formatting.Money(record.ToVehicle().CalculateFee(stay))}";
    }

    private void SetStatus(string message) => statusLabel.Text = message;
}
