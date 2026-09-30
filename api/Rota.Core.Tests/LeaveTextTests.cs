using Rota.Core.Leave;

namespace Rota.Core.Tests;

public class LeaveTextTests
{
    private static List<string> Days(CellParseResult r) => r.Dates.Select(d => $"{d.Day}/{d.Month}").ToList();

    [Theory]
    [InlineData("3,4,16,17", "3/10 4/10 16/10 17/10")]
    [InlineData("25-31", "25/10 26/10 27/10 28/10 29/10 30/10 31/10")]
    [InlineData("23 24", "23/10 24/10")]
    [InlineData("1,2,5-6,", "1/10 2/10 5/10 6/10")]
    [InlineData("1/10-4/10, 16/10-18/10", "1/10 2/10 3/10 4/10 16/10 17/10 18/10")]
    [InlineData("6/11- 8/11", "6/11 7/11 8/11")]
    [InlineData("1-4/10", "1/10 2/10 3/10 4/10")]
    [InlineData("30", "30/10")]
    public void Reads_lists_ranges_and_dates(string text, string expected)
    {
        Assert.Equal(expected.Split(' '), Days(LeaveText.ParseCell(text, 2026, 10)));
    }

    [Fact]
    public void Bracket_notes_are_kept_and_stripped()
    {
        var r = LeaveText.ParseCell("13-15 (luar negara)", 2026, 11);
        Assert.Equal(["13/11", "14/11", "15/11"], Days(r));
        Assert.Equal(["luar negara"], r.Notes);
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public void Missing_comma_between_ranges_is_paired_with_a_warning()
    {
        var r = LeaveText.ParseCell("11-13-18-20", 2026, 12);
        Assert.Equal(["11/12", "12/12", "13/12", "18/12", "19/12", "20/12"], Days(r));
        Assert.Contains(r.Warnings, w => w.Contains("read as 11-13, 18-20"));
    }

    [Fact]
    public void Slash_between_days_that_is_not_a_month_is_a_range()
    {
        var r = LeaveText.ParseCell("27/29", 2026, 11);
        Assert.Equal(["27/11", "28/11", "29/11"], Days(r));
        Assert.Contains(r.Warnings, w => w.Contains("read as 27-29"));
    }

    [Fact]
    public void Text_only_cell_marks_the_whole_month_and_keeps_the_text_as_note()
    {
        var r = LeaveText.ParseCell("Cuti bersalin", 2026, 11);
        Assert.Equal(30, r.Dates.Count);
        Assert.Contains("Cuti bersalin", r.Notes);
        Assert.Contains(r.Warnings, w => w.Contains("whole month"));
    }

    [Fact]
    public void Invalid_days_and_mismatched_months_are_warned()
    {
        var r = LeaveText.ParseCell("31, 1/2/2026", 2026, 11);
        Assert.Contains(r.Warnings, w => w.Contains("day 31 does not exist"));
        Assert.Contains(r.Warnings, w => w.Contains("outside this month's column"));
        Assert.Equal([new DateOnly(2026, 2, 1)], r.Dates);
    }

    [Theory]
    [InlineData("12")]
    [InlineData("3-5")]
    [InlineData("31/11")]
    [InlineData("cuti")]
    public void TryParseDateList_rejects_ambiguous_or_invalid_input(string text)
    {
        Assert.False(LeaveText.TryParseDateList(text, new DateOnly(2026, 10, 1), out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void TryParseDateList_infers_next_year_and_round_trips_with_FormatDateList()
    {
        var reference = new DateOnly(2026, 10, 1);
        Assert.True(LeaveText.TryParseDateList("27/12-2/1, 12/10", reference, out var dates, out _));

        Assert.Equal(8, dates.Count);
        Assert.Contains(new DateOnly(2027, 1, 2), dates);
        Assert.Equal("12/10, 27/12-2/1", LeaveText.FormatDateList(dates, reference));

        Assert.True(LeaveText.TryParseDateList(LeaveText.FormatDateList(dates, reference), reference, out var again, out _));
        Assert.Equal(dates, again);
    }
}
