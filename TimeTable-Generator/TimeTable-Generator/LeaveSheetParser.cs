using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace TimeTable_Generator
{
    public class LeaveRow
    {
        public string RawName { get; set; }
        public List<DateTime> Dates { get; } = new List<DateTime>();
        public List<string> Warnings { get; } = new List<string>();
        public List<string> Notes { get; } = new List<string>();
    }

    public class LeaveSheet
    {
        public List<LeaveRow> Rows { get; } = new List<LeaveRow>();

        /// <summary>First day of each month column found in the sheet.</summary>
        public List<DateTime> Months { get; } = new List<DateTime>();
    }

    public class CellParseResult
    {
        public List<DateTime> Dates { get; } = new List<DateTime>();
        public List<string> Warnings { get; } = new List<string>();
        public List<string> Notes { get; } = new List<string>();
    }

    /// <summary>
    /// Reads a leave sheet: column A = name (header NAMA/NAME), one column per month
    /// (header OKTOBER, NOVEMBER, DISEMBER, ...). Cells hold free-typed days such as
    /// "3,4,16", "25-31", "1/10-4/10", "13-26 (luar negara)", "Cuti bersalin".
    /// </summary>
    public static class LeaveSheetParser
    {
        private static readonly Dictionary<string, int> MonthNames = new Dictionary<string, int>
        {
            { "JAN", 1 }, { "JANUARI", 1 }, { "JANUARY", 1 },
            { "FEB", 2 }, { "FEBRUARI", 2 }, { "FEBRUARY", 2 },
            { "MAC", 3 }, { "MAR", 3 }, { "MARCH", 3 },
            { "APR", 4 }, { "APRIL", 4 },
            { "MEI", 5 }, { "MAY", 5 },
            { "JUN", 6 }, { "JUNE", 6 },
            { "JUL", 7 }, { "JULAI", 7 }, { "JULY", 7 },
            { "OGO", 8 }, { "OGOS", 8 }, { "AUG", 8 }, { "AUGUST", 8 },
            { "SEP", 9 }, { "SEPT", 9 }, { "SEPTEMBER", 9 },
            { "OKT", 10 }, { "OCT", 10 }, { "OKTOBER", 10 }, { "OCTOBER", 10 },
            { "NOV", 11 }, { "NOVEMBER", 11 },
            { "DIS", 12 }, { "DEC", 12 }, { "DISEMBER", 12 }, { "DECEMBER", 12 },
        };

        private static readonly HashSet<string> NameHeaders = new HashSet<string> { "NAMA", "NAME", "NAMES" };

        private const string DatePart = @"\d{1,2}[/.]\d{1,2}(?:[/.]\d{2,4})?";

        public static LeaveSheet Parse(string path, DateTime rotaStart, DateTime rotaEnd)
        {
            ExcelPackage.License.SetNonCommercialOrganization("My Noncommercial organization");

            var result = new LeaveSheet();

            using (var package = new ExcelPackage(new FileInfo(path)))
            {
                foreach (var ws in package.Workbook.Worksheets)
                {
                    if (ws.Dimension == null) continue;
                    if (ParseWorksheet(ws, rotaStart, rotaEnd, result)) break;
                }
            }

            if (result.Months.Count == 0)
                throw new InvalidDataException("Could not find a header row with a NAMA/NAME column and month columns.");

            return result;
        }

        private static bool ParseWorksheet(ExcelWorksheet ws, DateTime rotaStart, DateTime rotaEnd, LeaveSheet result)
        {
            int lastRow = ws.Dimension.End.Row;
            int lastCol = ws.Dimension.End.Column;

            for (int headerRow = 1; headerRow <= Math.Min(lastRow, 20); headerRow++)
            {
                int nameCol = -1;
                var monthCols = new Dictionary<int, DateTime>();

                for (int c = 1; c <= lastCol; c++)
                {
                    string header = (ws.Cells[headerRow, c].Text ?? "").Trim().ToUpperInvariant();
                    if (nameCol < 0 && NameHeaders.Contains(header))
                    {
                        nameCol = c;
                        continue;
                    }

                    var month = ParseMonthHeader(header, rotaStart, rotaEnd);
                    if (month.HasValue) monthCols[c] = month.Value;
                }

                if (nameCol < 0 || monthCols.Count == 0) continue;

                result.Months.AddRange(monthCols.Values.Distinct().OrderBy(m => m));

                for (int r = headerRow + 1; r <= lastRow; r++)
                {
                    string name = (ws.Cells[r, nameCol].Text ?? "").Trim();
                    if (name.Length == 0) continue;

                    var row = new LeaveRow { RawName = name };
                    foreach (var mc in monthCols)
                    {
                        var cell = ParseExcelCell(ws.Cells[r, mc.Key], mc.Value.Year, mc.Value.Month);
                        string monthLabel = mc.Value.ToString("MMM", CultureInfo.InvariantCulture);
                        row.Dates.AddRange(cell.Dates);
                        row.Warnings.AddRange(cell.Warnings.Select(w => $"{monthLabel}: {w}"));
                        row.Notes.AddRange(cell.Notes.Select(n => $"{monthLabel}: {n}"));
                    }

                    var distinct = row.Dates.Distinct().OrderBy(d => d).ToList();
                    row.Dates.Clear();
                    row.Dates.AddRange(distinct);
                    result.Rows.Add(row);
                }

                return true;
            }

            return false;
        }

        /// <summary>"OKTOBER", "Nov 2026", "DIS" -> first day of that month, year chosen nearest the rota.</summary>
        public static DateTime? ParseMonthHeader(string header, DateTime rotaStart, DateTime rotaEnd)
        {
            var m = Regex.Match(header ?? "", @"^([A-Za-z]+)\.?\s*(\d{4})?$");
            if (!m.Success) return null;
            if (!MonthNames.TryGetValue(m.Groups[1].Value.ToUpperInvariant(), out int month)) return null;

            if (m.Groups[2].Success)
                return new DateTime(int.Parse(m.Groups[2].Value), month, 1);

            DateTime best = DateTime.MinValue;
            double bestDistance = double.MaxValue;
            for (int y = rotaStart.Year - 1; y <= rotaEnd.Year + 1; y++)
            {
                var first = new DateTime(y, month, 1);
                var last = first.AddMonths(1).AddDays(-1);
                double distance = last < rotaStart.Date ? (rotaStart.Date - last).TotalDays
                                : first > rotaEnd.Date ? (first - rotaEnd.Date).TotalDays
                                : 0;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = first;
                }
            }
            return best;
        }

        private static CellParseResult ParseExcelCell(ExcelRange cell, int year, int month)
        {
            object value = cell.Value;
            if (value == null) return new CellParseResult();

            string format = cell.Style.Numberformat.Format ?? "";
            bool dateFormat = IsDateFormat(format, cell.Style.Numberformat.NumFmtID);

            if (value is DateTime || (value is double && dateFormat))
            {
                DateTime dt = value is DateTime ? (DateTime)value : DateTime.FromOADate((double)value);

                if (format.IndexOf('y') >= 0 || format.IndexOf('Y') >= 0)
                {
                    // A real full date.
                    var real = new CellParseResult();
                    real.Dates.Add(dt.Date);
                    if (dt.Month != month || dt.Year != year)
                        real.Warnings.Add($"date {dt:d} is outside this month's column");
                    return real;
                }

                // Excel turned something like "6-15" into 15 June. Read it back as typed.
                string typed = (cell.Text ?? "").Trim();
                if (typed.Length == 0)
                {
                    bool monthFirst = format.IndexOf('m') < format.IndexOf('d');
                    typed = monthFirst ? $"{dt.Month}-{dt.Day}" : $"{dt.Day}-{dt.Month}";
                }

                var parsed = ParseCell(typed, year, month);
                parsed.Warnings.Insert(0, $"Excel stored '{typed}' as a date; read as '{typed}' in this month");
                return parsed;
            }

            if (value is double)
            {
                double d = (double)value;
                var numeric = new CellParseResult();
                if (d == Math.Floor(d))
                    return ParseCell(((int)d).ToString(CultureInfo.InvariantCulture), year, month);

                numeric.Warnings.Add($"could not read number '{d}'");
                return numeric;
            }

            return ParseCell(Convert.ToString(value, CultureInfo.InvariantCulture), year, month);
        }

        private static bool IsDateFormat(string format, int numFmtId)
        {
            if ((numFmtId >= 14 && numFmtId <= 22) || (numFmtId >= 45 && numFmtId <= 47)) return true;
            if (string.IsNullOrEmpty(format) || format.Equals("General", StringComparison.OrdinalIgnoreCase)) return false;

            string stripped = Regex.Replace(format, "\"[^\"]*\"|\\[[^\\]]*\\]", "");   // quoted text, [colors]
            return Regex.IsMatch(stripped, "[dDmMyY]");
        }

        /// <summary>Parses one free-typed leave cell for the given month.</summary>
        public static CellParseResult ParseCell(string text, int year, int month)
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
                for (int d = 1; d <= days; d++) result.Dates.Add(new DateTime(year, month, d));
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

            var sorted = result.Dates.Distinct().OrderBy(d => d).ToList();
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
                AddDay(result, year, month, int.Parse(m.Groups[1].Value), tok);
                return;
            }

            // 3-10
            if ((m = Regex.Match(tok, @"^(\d{1,2})-(\d{1,2})$")).Success)
            {
                AddDayRange(result, year, month, int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), tok);
                return;
            }

            // 11-13-18-20 (missing comma): pair the numbers up.
            if ((m = Regex.Match(tok, @"^\d{1,2}(-\d{1,2}){2,}$")).Success)
            {
                var nums = tok.Split('-').Select(int.Parse).ToList();
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
            if ((m = Regex.Match(tok, @"^(\d{1,2})[/.](\d{1,2})$")).Success && int.Parse(m.Groups[2].Value) > 12)
            {
                int a = int.Parse(m.Groups[1].Value), b = int.Parse(m.Groups[2].Value);
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

        /// <summary>d/m or d/m/y (Malaysian day-first order).</summary>
        private static DateTime? ToDate(string text, int columnYear, int columnMonth, CellParseResult result)
        {
            var parts = text.Split('/', '.');
            int day = int.Parse(parts[0]);
            int month = int.Parse(parts[1]);

            int year;
            if (parts.Length > 2)
            {
                year = int.Parse(parts[2]);
                if (year < 100) year += 2000;
            }
            else
            {
                // Nearest year to the column's month (handles Dec column with "2/1").
                int diff = month - columnMonth;
                year = columnYear + (diff > 6 ? -1 : diff < -6 ? 1 : 0);
            }

            if (month < 1 || month > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
            {
                result.Warnings.Add($"invalid date '{text}'");
                return null;
            }

            var date = new DateTime(year, month, day);
            if (month != columnMonth || year != columnYear)
                result.Warnings.Add($"'{text}' is {date:d}, outside this month's column");
            return date;
        }

        private static void AddDay(CellParseResult result, int year, int month, int day, string tok)
        {
            if (day < 1 || day > DateTime.DaysInMonth(year, month))
            {
                result.Warnings.Add($"day {day} does not exist in this month ('{tok}')");
                return;
            }
            result.Dates.Add(new DateTime(year, month, day));
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
                result.Dates.Add(new DateTime(year, month, d));
        }

        private static void AddDateRange(CellParseResult result, DateTime from, DateTime to, string tok)
        {
            if (from > to)
            {
                result.Warnings.Add($"range '{tok}' is backwards; skipped");
                return;
            }
            if ((to - from).TotalDays > 62)
            {
                result.Warnings.Add($"range '{tok}' is longer than 2 months; skipped");
                return;
            }
            for (var d = from; d <= to; d = d.AddDays(1))
                result.Dates.Add(d);
        }

        /// <summary>
        /// Parses leave typed into the rota grid, e.g. "3/10-5/10, 12/10, 2/1/2027".
        /// Dates are day/month[/year]; a missing year is the one nearest the reference date.
        /// Returns null and fills <paramref name="error"/> if anything can't be read.
        /// </summary>
        public static List<DateTime> ParseDateList(string text, DateTime reference, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(text)) return new List<DateTime>();

            string stripped = Regex.Replace(text, @"\([^)]*\)?", " ");
            if (!Regex.IsMatch(stripped, @"\d"))
            {
                error = "Type dates as day/month, e.g. 3/10-5/10, 12/10";
                return null;
            }

            // Bare day numbers ("12", "3-5") are ambiguous across months in the grid.
            foreach (string token in Regex.Split(Regex.Replace(Regex.Replace(stripped, @"\s*-\s*", "-"), @"\s*/\s*", "/"), @"[,;&+\s]+"))
            {
                string tok = token.Trim('-', '/', '.');
                if (tok.Length == 0) continue;
                if (Regex.IsMatch(tok, @"^\d{1,2}(-\d{1,2})*$"))
                {
                    error = $"'{tok}' has no month. Type dates as day/month, e.g. 3/10-5/10, 12/10";
                    return null;
                }
            }

            var result = ParseCell(text, reference.Year, reference.Month);
            var problems = result.Warnings.Where(w => !w.Contains("outside this month's column")).ToList();
            if (problems.Count > 0)
            {
                error = string.Join("\n", problems);
                return null;
            }
            return result.Dates;
        }

        /// <summary>
        /// Formats dates for the rota grid so they round-trip through <see cref="ParseDateList"/>:
        /// "3/10-5/10, 12/10"; the year is shown only when it isn't the one nearest the reference date.
        /// </summary>
        public static string FormatDateList(IEnumerable<DateTime> dates, DateTime reference)
        {
            var list = (dates ?? Enumerable.Empty<DateTime>()).Select(d => d.Date).Distinct().OrderBy(d => d).ToList();
            var parts = new List<string>();
            int i = 0;
            while (i < list.Count)
            {
                int j = i;
                while (j + 1 < list.Count && (list[j + 1] - list[j]).TotalDays == 1) j++;
                parts.Add(i == j
                    ? FormatDate(list[i], reference)
                    : FormatDate(list[i], reference) + "-" + FormatDate(list[j], reference));
                i = j + 1;
            }
            return string.Join(", ", parts);
        }

        private static string FormatDate(DateTime d, DateTime reference)
        {
            int diff = d.Month - reference.Month;
            int inferredYear = reference.Year + (diff > 6 ? -1 : diff < -6 ? 1 : 0);
            return d.Year == inferredYear ? $"{d.Day}/{d.Month}" : $"{d.Day}/{d.Month}/{d.Year}";
        }

        /// <summary>"1, 2, 5-9, 12" style summary of dates.</summary>
        public static string Compact(IEnumerable<DateTime> dates)
        {
            var list = dates.Select(d => d.Date).Distinct().OrderBy(d => d).ToList();
            var parts = new List<string>();
            int i = 0;
            while (i < list.Count)
            {
                int j = i;
                while (j + 1 < list.Count && (list[j + 1] - list[j]).TotalDays == 1) j++;
                parts.Add(i == j
                    ? $"{list[i].Day}/{list[i].Month}"
                    : $"{list[i].Day}/{list[i].Month}-{list[j].Day}/{list[j].Month}");
                i = j + 1;
            }
            return string.Join(", ", parts);
        }
    }
}
