using System.Globalization;
using ClosedXML.Excel;

namespace Rota.Core.Excel;

public sealed record TimetableDay(DateOnly Date, string? PersonName, bool IsWeekendHoliday, bool IsPublicHoliday);

public sealed record PersonTotalsRow(string Code, string Name, int Total, int Weekday, int WeekendHoliday);

public sealed record LeaveExportRow(string Name, IReadOnlyCollection<DateOnly> Leave);

/// <summary>Excel downloads. Layouts follow the WinForms app so existing habits carry over.</summary>
public static class ExcelExports
{
    private static readonly XLColor HolidayFill = XLColor.PaleGreen;
    private static readonly XLColor WeekendFill = XLColor.FromHtml("#F1F5F9");
    private static readonly XLColor HeaderFill = XLColor.FromHtml("#E2E8F0");

    private static readonly string[] MalayMonths =
    [
        "JANUARI", "FEBRUARI", "MAC", "APRIL", "MEI", "JUN",
        "JULAI", "OGOS", "SEPTEMBER", "OKTOBER", "NOVEMBER", "DISEMBER"
    ];

    /// <summary>
    /// One sheet per month (Date, Day, WeekDayShift, WeekEndShift), public holidays shaded,
    /// plus an "Assignment Error" sheet when some dates have nobody.
    /// </summary>
    public static byte[] Timetable(IReadOnlyList<TimetableDay> days)
    {
        using var wb = new XLWorkbook();

        var unassigned = days.Where(d => d.PersonName is null).ToList();
        if (unassigned.Count > 0)
        {
            var err = wb.Worksheets.Add("Assignment Error");
            err.Cell(1, 1).Value = "Unassigned Dates";
            Header(err.Range(1, 1, 1, 1));
            int r = 2;
            foreach (var d in unassigned)
                err.Cell(r++, 1).Value = d.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            err.Columns().AdjustToContents();
        }

        foreach (var month in days.GroupBy(d => (d.Date.Year, d.Date.Month)).OrderBy(g => g.Key))
        {
            var first = new DateOnly(month.Key.Year, month.Key.Month, 1);
            var ws = wb.Worksheets.Add(first.ToString("MMMM yyyy", CultureInfo.InvariantCulture));

            ws.Cell(1, 1).Value = "Date";
            ws.Cell(1, 2).Value = "Day";
            ws.Cell(1, 3).Value = "WeekDayShift";
            ws.Cell(1, 4).Value = "WeekEndShift";
            Header(ws.Range(1, 1, 1, 4));

            int row = 2;
            foreach (var d in month.OrderBy(d => d.Date))
            {
                ws.Cell(row, 1).Value = d.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
                ws.Cell(row, 2).Value = d.Date.DayOfWeek + (d.IsPublicHoliday ? " (Public Holiday)" : "");
                ws.Cell(row, d.IsWeekendHoliday ? 4 : 3).Value = d.PersonName ?? "";

                if (d.IsPublicHoliday)
                    ws.Cell(row, 2).Style.Fill.BackgroundColor = HolidayFill;
                else if (d.IsWeekendHoliday)
                    ws.Range(row, 1, row, 4).Style.Fill.BackgroundColor = WeekendFill;
                row++;
            }

            ws.SheetView.FreezeRows(1);
            ws.Columns().AdjustToContents();
        }

        return Save(wb);
    }

    /// <summary>Everyone's shifts done so far (for reporting; the app remains the source of truth).</summary>
    public static byte[] Totals(IReadOnlyList<PersonTotalsRow> rows, DateOnly asOf)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Totals");

        ws.Cell(1, 1).Value = "as of";
        ws.Cell(1, 2).Value = asOf.ToString("d/M/yyyy", CultureInfo.InvariantCulture);

        string[] headers = ["CODE", "NAMES", "Total Shifts", "Weekday", "Weekend + Public Holidays"];
        for (int c = 0; c < headers.Length; c++) ws.Cell(3, c + 1).Value = headers[c];
        Header(ws.Range(3, 1, 3, headers.Length));

        int r = 4;
        foreach (var p in rows)
        {
            ws.Cell(r, 1).Value = p.Code;
            ws.Cell(r, 2).Value = p.Name;
            ws.Cell(r, 3).Value = p.Total;
            ws.Cell(r, 4).Value = p.Weekday;
            ws.Cell(r, 5).Value = p.WeekendHoliday;
            r++;
        }

        ws.SheetView.FreezeRows(3);
        ws.Columns().AdjustToContents();
        return Save(wb);
    }

    /// <summary>Leave in the shared-sheet layout: NAMA + one column per month of day lists ("3-5, 12").</summary>
    public static byte[] LeaveSheet(IReadOnlyList<LeaveExportRow> rows, DateOnly start, DateOnly end)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Leave");

        var months = new List<DateOnly>();
        for (var m = new DateOnly(start.Year, start.Month, 1); m <= end; m = m.AddMonths(1)) months.Add(m);

        ws.Cell(1, 1).Value = "NAMA";
        for (int c = 0; c < months.Count; c++) ws.Cell(1, c + 2).Value = MalayMonths[months[c].Month - 1];
        Header(ws.Range(1, 1, 1, months.Count + 1));

        int r = 2;
        foreach (var p in rows)
        {
            ws.Cell(r, 1).Value = p.Name;
            for (int c = 0; c < months.Count; c++)
            {
                var inMonth = p.Leave.Where(d => d.Year == months[c].Year && d.Month == months[c].Month);
                // Plain day numbers, typed as text so Excel doesn't turn "6-15" into a date.
                string days = DayList(inMonth);
                if (days.Length > 0) ws.Cell(r, c + 2).SetValue(days);
            }
            r++;
        }

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
        return Save(wb);
    }

    private static string DayList(IEnumerable<DateOnly> dates)
    {
        var days = dates.Select(d => d.Day).Distinct().Order().ToList();
        var parts = new List<string>();
        int i = 0;
        while (i < days.Count)
        {
            int j = i;
            while (j + 1 < days.Count && days[j + 1] == days[j] + 1) j++;
            parts.Add(i == j ? $"{days[i]}" : $"{days[i]}-{days[j]}");
            i = j + 1;
        }
        return string.Join(", ", parts);
    }

    private static void Header(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = HeaderFill;
    }

    private static byte[] Save(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
