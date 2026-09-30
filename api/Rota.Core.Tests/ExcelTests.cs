using ClosedXML.Excel;
using Rota.Core.Excel;

namespace Rota.Core.Tests;

/// <summary>Uses synthetic data only - real rotas contain staff names and stay out of git.</summary>
public class ExcelTests
{
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
}
