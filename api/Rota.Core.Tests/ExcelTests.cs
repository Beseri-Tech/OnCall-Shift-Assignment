using ClosedXML.Excel;
using Rota.Core.Excel;
using Rota.Core.Names;

namespace Rota.Core.Tests;

/// <summary>Uses synthetic workbooks only - the real sheets contain staff names and stay out of git.</summary>
public class ExcelTests
{
    private static MemoryStream Save(XLWorkbook wb)
    {
        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void Leave_sheet_reads_text_cells_numbers_and_excel_auto_dates()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sheet1");
        ws.Cell(1, 1).Value = "NAMA";
        ws.Cell(1, 2).Value = "OKTOBER";
        ws.Cell(1, 3).Value = "NOVEMBER";
        ws.Cell(1, 4).Value = "DISEMBER";

        ws.Cell(2, 1).Value = "Dr Alpha";
        ws.Cell(2, 2).Value = "3,4,16-18";
        // What Excel does to "6-15" typed in the November column: a June 15 date formatted m-d.
        ws.Cell(2, 3).Value = new DateTime(2026, 6, 15);
        ws.Cell(2, 3).Style.NumberFormat.Format = "m-d";
        ws.Cell(2, 4).Value = "25-31 (luar negara)";

        ws.Cell(3, 1).Value = "Dr Beta";
        ws.Cell(3, 2).Value = 30;
        ws.Cell(3, 3).Value = "Cuti bersalin";

        ws.Cell(4, 1).Value = "";          // blank name row is skipped
        ws.Cell(4, 2).Value = "1-3";

        using var stream = Save(wb);
        var sheet = LeaveSheetReader.Read(stream, new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 31));

        Assert.Equal([new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 1)], sheet.Months);
        Assert.Equal(2, sheet.Rows.Count);

        var alpha = sheet.Rows[0];
        Assert.Equal(5 + 10 + 7, alpha.Leave.Count);
        Assert.Contains(new DateOnly(2026, 11, 6), alpha.Leave.Keys);
        Assert.Contains(new DateOnly(2026, 11, 15), alpha.Leave.Keys);
        Assert.Equal("luar negara", alpha.Leave[new DateOnly(2026, 12, 25)]);
        Assert.Contains(alpha.Warnings, w => w.StartsWith("Nov: Excel stored '6-15' as a date"));

        var beta = sheet.Rows[1];
        Assert.Contains(new DateOnly(2026, 10, 30), beta.Leave.Keys);
        Assert.Equal(1 + 30, beta.Leave.Count);
        Assert.Equal("Cuti bersalin", beta.Leave[new DateOnly(2026, 11, 1)]);
    }

    [Fact]
    public void Weekend_history_sheet_reads_counts_and_blanks()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sheet1");
        ws.Cell(1, 1).Value = "last updated";
        ws.Cell(3, 1).Value = "NAMES";
        ws.Cell(4, 1).Value = "DR ALPHA";
        ws.Cell(4, 2).Value = 3;
        ws.Cell(5, 1).Value = "DR BETA";

        using var stream = Save(wb);
        var rows = WeekendHistorySheet.Read(stream);

        Assert.Equal([new WeekendHistoryRow("DR ALPHA", 3), new WeekendHistoryRow("DR BETA", null)], rows);
    }

    [Fact]
    public void Timetable_export_has_a_sheet_per_month_and_an_error_sheet_for_gaps()
    {
        var days = new List<TimetableDay>
        {
            new(new DateOnly(2026, 10, 30), "Dr Alpha", false, false),
            new(new DateOnly(2026, 10, 31), "Dr Beta", true, false),
            new(new DateOnly(2026, 11, 1), null, true, false),
        };

        using var wb = new XLWorkbook(new MemoryStream(ExcelExports.Timetable(days)));

        Assert.Equal(["Assignment Error", "October 2026", "November 2026"], wb.Worksheets.Select(w => w.Name));
        var oct = wb.Worksheet("October 2026");
        Assert.Equal("Dr Alpha", oct.Cell(2, 3).GetString());
        Assert.Equal("Dr Beta", oct.Cell(3, 4).GetString());
    }

    [Fact]
    public void Leave_export_round_trips_through_the_leave_reader()
    {
        var leave = new List<LeaveExportRow>
        {
            new("Dr Alpha", [new(2026, 11, 6), new(2026, 11, 7), new(2026, 11, 8), new(2026, 12, 25)]),
        };

        using var stream = new MemoryStream(ExcelExports.LeaveSheet(leave, new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 31)));
        var sheet = LeaveSheetReader.Read(stream, new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 31));

        Assert.Equal(leave[0].Leave.Order(), sheet.Rows.Single().Leave.Keys);
        Assert.Empty(sheet.Rows.Single().Warnings);
    }

    [Fact]
    public void Name_matcher_handles_formatting_and_small_typos()
    {
        string[] people = ["DR. SAW JU WEN", "Dr Wong Yu Wan", "Dr Siti Aishah Binti Shahril", "Dr Lee Yik Yi"];
        string[] sheet = ["Dr Saw Ju Wen", "Dr Wong Yuyan", "Dr Siti Aisyah Bt Shahril", "Dr Evelyn Yeoh"];

        var m = NameMatcher.MatchAll(sheet, people, p => p);

        Assert.Equal(NameMatchType.Exact, m[0].Type);
        Assert.Equal("Dr Wong Yu Wan", m[1].Match);
        Assert.Equal(NameMatchType.Fuzzy, m[1].Type);
        Assert.Equal("Dr Siti Aishah Binti Shahril", m[2].Match);
        Assert.Equal(NameMatchType.None, m[3].Type);
    }
}
