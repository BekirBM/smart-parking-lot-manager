# Smart Parking Lot Manager

## Overview
Smart Parking Lot Manager is a Windows desktop application built with C# and .NET Windows Forms. A parking attendant sees a live, color-coded map of the lot, checks vehicles in and out with one click, and gets a printed-style receipt with an automatic fee. At the end of the day the app exports a revenue report.

## Features
- **Live parking map** drawn with GDI+: 26 spots in 4 rows (Small, Regular and Large) with an entry/exit driving lane.
  - Green = free, red = occupied.
  - **Stay heat:** a spot gets darker red the longer the vehicle stays (4 hours or more is darkest).
  - Hover a spot to see a tooltip with the plate, arrival time and fee so far.
- **Three vehicle types (inheritance):** `Bike`, `Car` and `Truck` all extend the abstract `Vehicle` class. Each has its own hourly rate, daily maximum and minimum spot size. `Truck` overrides the fee calculation to add a surcharge (polymorphism).
- **Smart check-in:**
  - Select a free spot yourself, or let the app choose the **best fit** (the smallest free spot the vehicle fits in).
  - Validates the plate format, duplicate plates, spot size ("A6 is too small for a Truck") and arrival time.
- **Check-out with receipt:** the fee is calculated automatically (15-minute grace period, every started hour after that, capped at a daily maximum). The receipt can be saved as a `.txt` file.
- **Find a vehicle** by plate: its spot is highlighted in gold. If the vehicle already left, the app shows when it left and what it paid.
- **Today at a glance:** occupancy bar, revenue collected, money still to collect, number of check-outs, average stay, and free spots per vehicle type.
- **Two tables:** currently parked vehicles (click a row to find it on the map) and today's check-outs.
- **Daily report export** (File > Export daily report, or Ctrl+E): summary, busiest arrival hour, breakdown by vehicle type, and all completed tickets.
- **Demo data** (Tools > Load demo data) fills the lot with a realistic day for presentations.
- **Automatic saving** to a JSON file.

## Fee rules
| Vehicle | Rate | Daily maximum | Needs spot |
|---|---|---|---|
| Bike | $1.00 / hour | $6.00 | Small or bigger |
| Car | $2.50 / hour | $20.00 | Regular or bigger |
| Truck | $5.00 / hour + $3.00 surcharge | $40.00 | Large |

The first 15 minutes are free. After that, every started hour is charged. To use a different currency symbol, change `CurrencySymbol` in `Formatting.cs`.

## Technologies
- C#
- .NET 8
- Windows Forms (custom control, `MenuStrip`, `StatusStrip`, `TabControl`, `DataGridView`, `Timer`, `ToolTip`, dialogs)
- GDI+ drawing (`Graphics`, `GraphicsPath`, anti-aliasing)
- Object-oriented programming (abstract class, inheritance, polymorphism, encapsulation)
- LINQ
- JSON file storage (`System.Text.Json`)
- Custom exceptions for validation (`ParkingException`)

## Project structure
| File | Purpose |
|---|---|
| `Models/Vehicle.cs` | Abstract `Vehicle` plus `Bike`, `Car`, `Truck`; fee calculation |
| `Models/ParkingSpot.cs` | One spot (row, number, size) |
| `Models/ParkingRecord.cs` | One visit / ticket (entry, exit, fee) |
| `ParkingLot.cs` | Business logic: check in/out, best-fit search, statistics, daily report |
| `LotMapControl.cs` | Custom control that draws the map and handles clicks and hover |
| `MainForm.cs` | Main window: layout, events and refreshing |
| `ReceiptForm.cs` / `Receipt.cs` | Check-out dialog and receipt text |
| `LotStorage.cs` | Load/save records as JSON |
| `DemoData.cs` | Generates a realistic demo day |
| `Formatting.cs` | Money and duration formatting |

## How to run
1. Install Visual Studio 2022 with the **.NET desktop development** workload.
2. Open `ParkingLotManager.csproj`.
3. If prompted, install/target the .NET 8 SDK.
4. Press **F5** to run the program.

Alternatively, from a terminal in this folder:
```bash
dotnet run
```
This project targets Windows because it uses Windows Forms.

## How to use
1. **Check in:** type a plate (for example `ABC-1234`), choose the vehicle type and press **Enter**. The spot is picked automatically, or you can click a free spot first to choose it yourself.
2. **Check out:** click a red spot, press **Check out & take payment**, then confirm on the receipt.
3. **Find:** type a plate in *Find a vehicle* and press Enter.
4. Press **Esc** to clear the selection.

## Data storage
Records are saved automatically at:
`%LOCALAPPDATA%\ParkingLotManager\records.json`

## Suggested presentation
1. Start with Tools > Load demo data and explain the map colors and the "stay heat".
2. Check in a car and show that best fit chooses the smallest spot that fits.
3. Select a small spot, choose Truck and try to check in to show the validation.
4. Check out a vehicle and walk through the receipt and fee rules.
5. Search for a plate, then export the daily report.

Smart Parking Lot Manager replaces paper tickets and manual fee calculation. The program demonstrates classes and inheritance, polymorphism, collections and LINQ, event-driven programming, custom drawing with GDI+, validation with exceptions, and file handling.
