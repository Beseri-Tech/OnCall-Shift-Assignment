using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Rota.Core.Leave;

namespace Rota.Core.Excel;

public sealed class LeaveSheetRow
{
    public required string RawName { get; init; }

    /// <summary>Leave dates with the note written in the same cell (e.g. "luar negara"), if any.</summary>
    public SortedDictionary<DateOnly, string?> Leave { get; } = [];

    public List<string> Warnings { get; } = [];
}

public sealed class LeaveSheet
{
    public List<LeaveSheetRow> Rows { get; } = [];

    /// <summary>First day of each month column found in the sheet.</summary>
    public List<DateOnly> Months { get; } = [];
}

/// <summary>
/// Reads the shared leave sheet: a NAMA/NAME column, then one column per month
/// (OKTOBER, NOVEMBER, DISEMBER, ...) holding free-typed days.
/// </summary>
public static class LeaveSheetReader
{
    private static readonly HashSet<string> NameHeaders = new(StringComparer.OrdinalIgnoreCase) { "NAMA", "NAME", "NAMES" };

    public static LeaveSheet Read(Stream xlsx, DateOnly rangeStart, DateOnly rangeEnd)
    {
        using var workbook = new XLWorkbook(xlsx);
        var result = new LeaveSheet();

        foreach (var ws in workbook.Worksheets)
        {
            if (ReadWorksheet(ws, rangeStart, rangeEnd, result)) return result;
        }

        throw new InvalidDataException("Could not find a header row with a NAMA/NAME column and month columns.");
    }

    private static bool ReadWorksheet(IXLWorksheet ws, DateOnly rangeStart, DateOnly rangeEnd, LeaveSheet result)
    {
        int lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
        int lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;

        for (int headerRow = 1; headerRow <= Math.Min(lastRow, 20); headerRow++)
        {
            int nameCol = -1;
            var monthCols = new Dictionary<int, DateOnly>();

            for (int c = 1; c <= lastCol; c++)
            {
                string header = ws.Cell(headerRow, c).GetFormattedString().Trim();
                if (nameCol < 0 && NameHeaders.Contains(header))
                {
                    nameCol = c;
                    continue;
                }

                var month = LeaveText.ParseMonthHeader(header, rangeStart, rangeEnd);
                if (month.HasValue) monthCols[c] = month.Value;
            }

            if (nameCol < 0 || monthCols.Count == 0) continue;

            result.Months.AddRange(monthCols.Values.Distinct().Order());

            for (int r = headerRow + 1; r <= lastRow; r++)
            {
                string name = ws.Cell(r, nameCol).GetFormattedString().Trim();
                if (name.Length == 0) continue;

                var row = new LeaveSheetRow { RawName = name };
                foreach (var (col, month) in monthCols)
                {
                    var cell = ReadCell(ws.Cell(r, col), month.Year, month.Month);
                    string label = month.ToString("MMM", CultureInfo.InvariantCulture);
                    string? note = cell.Notes.Count > 0 ? string.Join("; ", cell.Notes.Distinct()) : null;

                    foreach (var d in cell.Dates)
                        row.Leave[d] = note ?? row.Leave.GetValueOrDefault(d);
                    row.Warnings.AddRange(cell.Warnings.Select(w => $"{label}: {w}"));
                }

                result.Rows.Add(row);
            }

            return true;
        }

        return false;
    }

    private static CellParseResult ReadCell(IXLCell cell, int year, int month)
    {
        if (cell.IsEmpty()) return new CellParseResult();

        var value = cell.Value;
        string format = cell.Style.NumberFormat.Format ?? "";

        if (value.IsDateTime || (value.IsNumber && IsDateFormat(format, cell.Style.NumberFormat.NumberFormatId)))
        {
            var dt = value.IsDateTime ? value.GetDateTime() : DateTime.FromOADate(value.GetNumber());

            if (format.Contains('y', StringComparison.OrdinalIgnoreCase))
            {
                // A real full date.
                var real = new CellParseResult();
                var d = DateOnly.FromDateTime(dt);
                real.Dates.Add(d);
                if (d.Month != month || d.Year != year)
                    real.Warnings.Add($"date {LeaveText.Format(d)} is outside this month's column");
                return real;
            }

            // Excel turned something like "6-15" into 15 June. Read it back as typed.
            string typed = cell.GetFormattedString().Trim();
            if (typed.Length == 0 || !Regex.IsMatch(typed, @"^\d{1,2}\D\d{1,2}$"))
            {
                bool monthFirst = format.IndexOf('m', StringComparison.OrdinalIgnoreCase) <
                                  format.IndexOf('d', StringComparison.OrdinalIgnoreCase);
                typed = monthFirst ? $"{dt.Month}-{dt.Day}" : $"{dt.Day}-{dt.Month}";
            }

            var parsed = LeaveText.ParseCell(typed, year, month);
            parsed.Warnings.Insert(0, $"Excel stored '{typed}' as a date; read as '{typed}' in this month");
            return parsed;
        }

        if (value.IsNumber)
        {
            double d = value.GetNumber();
            if (d == Math.Floor(d))
                return LeaveText.ParseCell(((int)d).ToString(CultureInfo.InvariantCulture), year, month);

            var numeric = new CellParseResult();
            numeric.Warnings.Add($"could not read number '{d}'");
            return numeric;
        }

        return LeaveText.ParseCell(cell.GetFormattedString(), year, month);
    }

    private static bool IsDateFormat(string format, int numFmtId)
    {
        if (numFmtId is >= 14 and <= 22 or >= 45 and <= 47) return true;
        if (string.IsNullOrEmpty(format) || format.Equals("General", StringComparison.OrdinalIgnoreCase)) return false;

        string stripped = Regex.Replace(format, "\"[^\"]*\"|\\[[^\\]]*\\]", "");   // quoted text, [colors]
        return Regex.IsMatch(stripped, "[dDmMyY]");
    }
}
