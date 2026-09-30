using System.Globalization;
using System.Text.RegularExpressions;

namespace Rota.Core.Leave;

public sealed class CellParseResult
{
    public List<DateOnly> Dates { get; } = [];
    public List<string> Warnings { get; } = [];
    public List<string> Notes { get; } = [];
}

/// <summary>
/// Parses free-typed leave text as people write it in the leave sheet:
/// "3,4,16", "25-31", "1/10-4/10", "13-26 (luar negara)", "11-13-18-20", "27/29", "Cuti bersalin".
/// Dates are day-first (Malaysian order).
/// </summary>
public static class LeaveText
{
    private const string DatePart = @"\d{1,2}[/.]\d{1,2}(?:[/.]\d{2,4})?";

    /// <summary>Parses one free-typed leave cell for the given month.</summary>
    public static CellParseResult ParseCell(string? text, int year, int month)
    {
        var result = new CellParseResult();
        if (string.IsNullOrWhiteSpace(text)) return result;

        string t = text;

        // Notes in brackets: "13-26 (luar negara)". An unclosed "(" runs to the end.
        foreach (Match m in Regex.Matches(t, @"\(([^)]*)\)?"))
        {
            string note = m.Groups[1].Value.Trim();
            if (note.Length > 0) result.Notes.Add(note);
        }
        t = Regex.Replace(t, @"\([^)]*\)?", " ");

        if (!Regex.IsMatch(t, @"\d"))
        {
            string words = Regex.Replace(t, @"\s+", " ").Trim();
            if (words.Length == 0 && result.Notes.Count == 0) return result;

            string label = words.Length > 0 ? words : string.Join(", ", result.Notes);
            int days = DateTime.DaysInMonth(year, month);
            for (int d = 1; d <= days; d++) result.Dates.Add(new DateOnly(year, month, d));
            result.Notes.Add(label);
            result.Warnings.Add($"no dates in '{label}'; whole month marked as leave");
            return result;
        }

        t = t.ToLowerInvariant();
        t = Regex.Replace(t, "[–—−~]", "-");
        t = Regex.Replace(t, @"\b(to|hingga|sampai|until|till)\b", "-");
        t = Regex.Replace(t, @"\b(dan|and)\b", ",");

        // Any other words are kept as notes: "16-30 clear leave".
        foreach (Match w in Regex.Matches(t, @"[a-z][a-z' ]*[a-z]|[a-z]"))
            result.Notes.Add(w.Value.Trim());
        t = Regex.Replace(t, @"[a-z']+", " ");

        t = Regex.Replace(t, @"\s*-\s*", "-");
        t = Regex.Replace(t, @"\s*/\s*", "/");

        foreach (string token in Regex.Split(t, @"[,;&+\s]+"))
        {
            string tok = token.Trim('-', '/', '.');
            if (tok.Length == 0) continue;
            ParseToken(tok, year, month, result);
        }

        var sorted = result.Dates.Distinct().Order().ToList();
        result.Dates.Clear();
        result.Dates.AddRange(sorted);
        return result;
    }

    private static void ParseToken(string tok, int year, int month, CellParseResult result)
    {
        Match m;

        // 12
        if ((m = Regex.Match(tok, @"^(\d{1,2})$")).Success)
        {
            AddDay(result, year, month, Int(m.Groups[1].Value), tok);
            return;
        }

        // 3-10
        if ((m = Regex.Match(tok, @"^(\d{1,2})-(\d{1,2})$")).Success)
        {
            AddDayRange(result, year, month, Int(m.Groups[1].Value), Int(m.Groups[2].Value), tok);
            return;
        }

        // 11-13-18-20 (missing comma): pair the numbers up.
        if (Regex.IsMatch(tok, @"^\d{1,2}(-\d{1,2}){2,}$"))
        {
            var nums = tok.Split('-').Select(Int).ToList();
            if (nums.Count % 2 == 0)
            {
                var pairs = new List<string>();
                for (int i = 0; i < nums.Count; i += 2)
                {
                    AddDayRange(result, year, month, nums[i], nums[i + 1], tok);
                    pairs.Add($"{nums[i]}-{nums[i + 1]}");
                }
                result.Warnings.Add($"'{tok}' read as {string.Join(", ", pairs)}");
            }
            else
            {
                AddDayRange(result, year, month, nums.Min(), nums.Max(), tok);
                result.Warnings.Add($"'{tok}' read as {nums.Min()}-{nums.Max()}");
            }
            return;
        }

        // 27/29 - second number can't be a month: a range typed with '/'.
        if ((m = Regex.Match(tok, @"^(\d{1,2})[/.](\d{1,2})$")).Success && Int(m.Groups[2].Value) > 12)
        {
            int a = Int(m.Groups[1].Value), b = Int(m.Groups[2].Value);
            AddDayRange(result, year, month, a, b, tok);
            result.Warnings.Add($"'{tok}' read as {a}-{b}");
            return;
        }

        // 1/10 or 1/2/2026
        if (Regex.IsMatch(tok, "^" + DatePart + "$"))
        {
            var d = ToDate(tok, year, month, result);
            if (d.HasValue) result.Dates.Add(d.Value);
            return;
        }

        // 1/10-4/10
        if ((m = Regex.Match(tok, "^(" + DatePart + ")-(" + DatePart + ")$")).Success)
        {
            var a = ToDate(m.Groups[1].Value, year, month, result);
            var b = ToDate(m.Groups[2].Value, year, month, result);
            if (a.HasValue && b.HasValue) AddDateRange(result, a.Value, b.Value, tok);
            return;
        }

        // 1/10-4  ->  1/10 to 4/10
        if ((m = Regex.Match(tok, @"^(\d{1,2})[/.](\d{1,2})-(\d{1,2})$")).Success)
        {
            var a = ToDate($"{m.Groups[1].Value}/{m.Groups[2].Value}", year, month, result);
            var b = ToDate($"{m.Groups[3].Value}/{m.Groups[2].Value}", year, month, result);
            if (a.HasValue && b.HasValue) AddDateRange(result, a.Value, b.Value, tok);
            return;
        }

        // 1-4/10  ->  1/10 to 4/10
        if ((m = Regex.Match(tok, @"^(\d{1,2})-(\d{1,2})[/.](\d{1,2})$")).Success)
        {
            var a = ToDate($"{m.Groups[1].Value}/{m.Groups[3].Value}", year, month, result);
            var b = ToDate($"{m.Groups[2].Value}/{m.Groups[3].Value}", year, month, result);
            if (a.HasValue && b.HasValue) AddDateRange(result, a.Value, b.Value, tok);
            return;
        }

        result.Warnings.Add($"could not read '{tok}'");
    }

    private static int Int(string s) => int.Parse(s, CultureInfo.InvariantCulture);

    /// <summary>d/m or d/m/y (Malaysian day-first order).</summary>
    private static DateOnly? ToDate(string text, int columnYear, int columnMonth, CellParseResult result)
    {
        var parts = text.Split('/', '.');
        int day = Int(parts[0]);
        int month = Int(parts[1]);

        int year;
        if (parts.Length > 2)
        {
            year = Int(parts[2]);
            if (year < 100) year += 2000;
        }
        else
        {
            year = NearestYear(month, columnYear, columnMonth);
        }

        if (month < 1 || month > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            result.Warnings.Add($"invalid date '{text}'");
            return null;
        }

        var date = new DateOnly(year, month, day);
        if (month != columnMonth || year != columnYear)
            result.Warnings.Add($"'{text}' is {Format(date)}, outside this month's column");
        return date;
    }

    /// <summary>Year for a month typed without one: the one nearest the reference (Dec + "2/1" = next year).</summary>
    private static int NearestYear(int month, int referenceYear, int referenceMonth)
    {
        int diff = month - referenceMonth;
        return referenceYear + (diff > 6 ? -1 : diff < -6 ? 1 : 0);
    }

    private static void AddDay(CellParseResult result, int year, int month, int day, string tok)
    {
        if (day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            result.Warnings.Add($"day {day} does not exist in this month ('{tok}')");
            return;
        }
        result.Dates.Add(new DateOnly(year, month, day));
    }

    private static void AddDayRange(CellParseResult result, int year, int month, int from, int to, string tok)
    {
        if (from > to)
        {
            result.Warnings.Add($"range '{tok}' is backwards; skipped");
            return;
        }

        int days = DateTime.DaysInMonth(year, month);
        if (to > days)
        {
            result.Warnings.Add($"'{tok}' goes past day {days}; cut at {days}");
            to = days;
        }
        for (int d = Math.Max(1, from); d <= to; d++)
            result.Dates.Add(new DateOnly(year, month, d));
    }

    private static void AddDateRange(CellParseResult result, DateOnly from, DateOnly to, string tok)
    {
        if (from > to)
        {
            result.Warnings.Add($"range '{tok}' is backwards; skipped");
            return;
        }
        if (to.DayNumber - from.DayNumber > 62)
        {
            result.Warnings.Add($"range '{tok}' is longer than 2 months; skipped");
            return;
        }
        for (var d = from; d <= to; d = d.AddDays(1))
            result.Dates.Add(d);
    }

    /// <summary>
    /// Parses leave typed by a user, e.g. "3/10-5/10, 12/10, 2/1/2027".
    /// Dates are day/month[/year]; a missing year is the one nearest the reference date.
    /// Returns false with an error message if anything can't be read.
    /// </summary>
    public static bool TryParseDateList(string? text, DateOnly reference, out List<DateOnly> dates, out string? error)
    {
        dates = [];
        error = null;
        if (string.IsNullOrWhiteSpace(text)) return true;

        string stripped = Regex.Replace(text, @"\([^)]*\)?", " ");
        if (!Regex.IsMatch(stripped, @"\d"))
        {
            error = "Type dates as day/month, e.g. 3/10-5/10, 12/10";
            return false;
        }

        // Bare day numbers ("12", "3-5") are ambiguous across months.
        string normalised = Regex.Replace(Regex.Replace(stripped, @"\s*-\s*", "-"), @"\s*/\s*", "/");
        foreach (string token in Regex.Split(normalised, @"[,;&+\s]+"))
        {
            string tok = token.Trim('-', '/', '.');
            if (tok.Length == 0) continue;
            if (Regex.IsMatch(tok, @"^\d{1,2}(-\d{1,2})*$"))
            {
                error = $"'{tok}' has no month. Type dates as day/month, e.g. 3/10-5/10, 12/10";
                return false;
            }
        }

        var result = ParseCell(text, reference.Year, reference.Month);
        var problems = result.Warnings.Where(w => !w.Contains("outside this month's column")).ToList();
        if (problems.Count > 0)
        {
            error = string.Join("\n", problems);
            return false;
        }

        dates = result.Dates;
        return true;
    }

    /// <summary>
    /// Formats dates so they round-trip through <see cref="TryParseDateList"/>:
    /// "3/10-5/10, 12/10"; the year is shown only when it isn't the one nearest the reference date.
    /// </summary>
    public static string FormatDateList(IEnumerable<DateOnly> dates, DateOnly reference)
    {
        var list = dates.Distinct().Order().ToList();
        var parts = new List<string>();
        int i = 0;
        while (i < list.Count)
        {
            int j = i;
            while (j + 1 < list.Count && list[j + 1].DayNumber - list[j].DayNumber == 1) j++;
            parts.Add(i == j
                ? FormatDate(list[i], reference)
                : FormatDate(list[i], reference) + "-" + FormatDate(list[j], reference));
            i = j + 1;
        }
        return string.Join(", ", parts);
    }

    private static string FormatDate(DateOnly d, DateOnly reference) =>
        d.Year == NearestYear(d.Month, reference.Year, reference.Month)
            ? $"{d.Day}/{d.Month}"
            : $"{d.Day}/{d.Month}/{d.Year}";

    /// <summary>"1/10, 5/10-9/10" style summary of dates.</summary>
    public static string Compact(IEnumerable<DateOnly> dates)
    {
        var list = dates.Distinct().Order().ToList();
        var parts = new List<string>();
        int i = 0;
        while (i < list.Count)
        {
            int j = i;
            while (j + 1 < list.Count && list[j + 1].DayNumber - list[j].DayNumber == 1) j++;
            parts.Add(i == j
                ? $"{list[i].Day}/{list[i].Month}"
                : $"{list[i].Day}/{list[i].Month}-{list[j].Day}/{list[j].Month}");
            i = j + 1;
        }
        return string.Join(", ", parts);
    }

    public static string Format(DateOnly d) => d.ToString("d/M/yyyy", CultureInfo.InvariantCulture);
}
