using System.Globalization;
using ClosedXML.Excel;

namespace Rota.Core.Excel;

public sealed record WeekendHistoryRow(string Name, int? WeekendHolidayShifts);

/// <summary>
/// Reads the legacy "Oncaller Total Weekend And Public Shift" sheet once, to seed each person's
/// opening weekend/public holiday count. Layout: a header row with NAMES/NAMA in column A,
/// then name (A) + count (B). A blank count means unknown.
/// </summary>
public static class WeekendHistorySheet
{
    private static readonly HashSet<string> NameHeaders = new(StringComparer.OrdinalIgnoreCase) { "NAMA", "NAME", "NAMES" };

    public static List<WeekendHistoryRow> Read(Stream xlsx)
    {
        using var workbook = new XLWorkbook(xlsx);
        var ws = workbook.Worksheets.FirstOrDefault(w => w.LastRowUsed() != null)
                 ?? throw new InvalidDataException("The workbook is empty.");

        int lastRow = ws.LastRowUsed()!.RowNumber();
        int headerRow = Enumerable.Range(1, Math.Min(lastRow, 20))
            .FirstOrDefault(r => NameHeaders.Contains(ws.Cell(r, 1).GetFormattedString().Trim()));
        if (headerRow == 0)
            throw new InvalidDataException("Could not find a header row with NAMES/NAMA in column A.");

        var rows = new List<WeekendHistoryRow>();
        for (int r = headerRow + 1; r <= lastRow; r++)
        {
            string name = ws.Cell(r, 1).GetFormattedString().Trim();
            if (name.Length == 0) continue;
            rows.Add(new WeekendHistoryRow(name, ReadCount(ws.Cell(r, 2))));
        }
        return rows;
    }

    private static int? ReadCount(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;
        if (cell.Value.IsNumber) return (int)Math.Round(cell.Value.GetNumber());

        return double.TryParse(cell.GetFormattedString().Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double d)
            ? (int)Math.Round(d)
            : null;
    }
}
