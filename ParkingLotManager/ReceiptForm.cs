namespace ParkingLotManager;

/// <summary>Check-out dialog: shows the receipt and asks the attendant to confirm payment.</summary>
public class ReceiptForm : Form
{
    private readonly string receiptText;

    public ReceiptForm(ParkingRecord record, ParkingSpot? spot, DateTime exitTime)
    {
        receiptText = Receipt.Build(record, spot, exitTime);

        Text = $"Check out {record.Plate}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(236, 239, 243);
        ClientSize = new Size(420, 520);

        var paper = new Label
        {
            Text = receiptText,
            Font = new Font("Consolas", 10.5f),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 30, 30),
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(14),
            Location = new Point(20, 20),
            Size = new Size(380, 420)
        };

        var payButton = new Button
        {
            Text = "Confirm payment",
            DialogResult = DialogResult.OK,
            BackColor = Color.FromArgb(39, 150, 86),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(20, 458),
            Size = new Size(160, 42)
        };
        payButton.FlatAppearance.BorderSize = 0;

        var saveButton = new Button { Text = "Save...", Location = new Point(190, 458), Size = new Size(100, 42) };
        saveButton.Click += (_, _) => SaveReceipt(record);

        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(300, 458), Size = new Size(100, 42) };

        Controls.AddRange(new Control[] { paper, payButton, saveButton, cancelButton });
        AcceptButton = payButton;
        CancelButton = cancelButton;
    }

    private void SaveReceipt(ParkingRecord record)
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "Text file (*.txt)|*.txt",
            FileName = $"receipt-{record.Ticket}-{record.Plate}.txt"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            File.WriteAllText(dialog.FileName, receiptText);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not save receipt", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
