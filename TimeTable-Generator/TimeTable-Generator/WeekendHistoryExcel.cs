using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace TimeTable_Generator
{
    public class HistoryImportResult
    {
        public List<NameMatch> Matches { get; } = new List<NameMatch>();

        /// <summary>Names in the history sheet that are not in the rota.</summary>
        public List<string> NotInRota { get; } = new List<string>();

        /// <summary>Rota people with no count (blank or missing) - they use the group average.</summary>
        public List<string> WithoutHistory { get; } = new List<string>();
    }

    /// <summary>
    /// History sheet layout: B1 = last updated, C1 = note, a header row with NAMES/NAMA in column A,
    /// then name (column A) + total weekend/public holiday shifts (column B).
    /// </summary>
    public static class WeekendHistoryExcel
    {
        private static readonly HashSet<string> NameHeaders = new HashSet<string> { "NAMA", "NAME", "NAMES" };

        private static readonly string[] MalayMonths =
        {
            "JANUARI", "FEBRUARI", "MAC", "APRIL", "MEI", "JUN",
            "JULAI", "OGOS", "SEPTEMBER", "OKTOBER", "NOVEMBER", "DISEMBER"
        };

        /// <summary>Value used for people without a known history count.</summary>
        public static double Average(IEnumerable<Person> people)
        {
            var known = people.Where(p => p.PriorWeekendShifts.HasValue).Select(p => p.PriorWeekendShifts.Value).ToList();
            return known.Count > 0 ? known.Average() : 0;
        }

        public static HistoryImportResult Import(string path, List<Person> people)
        {
            ExcelPackage.License.SetNonCommercialOrganization("My Noncommercial organization");
            var result = new HistoryImportResult();

            using (var package = new ExcelPackage(new FileInfo(path)))
            {
                var ws = package.Workbook.Worksheets.FirstOrDefault(w => w.Dimension != null)
                         ?? throw new InvalidDataException("The workbook is empty.");

                int headerRow = FindHeaderRow(ws);
                var rows = ReadRows(ws, headerRow);

                var matches = NameMatcher.MatchAll(rows.Select(r => r.Item2).ToList(), people);

                foreach (var p in people)
                    p.PriorWeekendShifts = null;

                for (int i = 0; i < rows.Count; i++)
                {
                    var match = matches[i];
                    if (match.Person == null)
                    {
                        result.NotInRota.Add(rows[i].Item2);
                        continue;
                    }

                    match.Person.PriorWeekendShifts = rows[i].Item3;
                    result.Matches.Add(match);
                }
            }

            result.WithoutHistory.AddRange(people.Where(p => !p.PriorWeekendShifts.HasValue).Select(p => p.Name));
            return result;
        }

        /// <summary>
        /// Writes a copy of the history sheet with each rota person's count increased by the
        /// weekend/holiday shifts in the current rota. Rows for people not in the rota are kept as is;
        /// rota people missing from the sheet are appended.
        /// </summary>
        public static void ExportUpdated(string sourcePath, string savePath, List<Person> people, DateTime rotaEnd)
        {
            ExcelPackage.License.SetNonCommercialOrganization("My Noncommercial organization");

            int baseline = (int)Math.Round(Average(people), MidpointRounding.AwayFromZero);

            using (var package = new ExcelPackage(new FileInfo(sourcePath)))
            {
                var ws = package.Workbook.Worksheets.FirstOrDefault(w => w.Dimension != null)
                         ?? throw new InvalidDataException("The workbook is empty.");

                int headerRow = FindHeaderRow(ws);
                var rows = ReadRows(ws, headerRow);
                var matches = NameMatcher.MatchAll(rows.Select(r => r.Item2).ToList(), people);
                var written = new HashSet<Person>();

                for (int i = 0; i < rows.Count; i++)
                {
                    var person = matches[i].Person;
                    if (person == null) continue;

                    ws.Cells[rows[i].Item1, 2].Value = (person.PriorWeekendShifts ?? baseline) + person.WeekendShifts;
                    written.Add(person);
                }

                int nextRow = Math.Max(headerRow, rows.Count > 0 ? rows.Max(r => r.Item1) : headerRow) + 1;
                foreach (var person in people.Where(p => !written.Contains(p)))
                {
                    ws.Cells[nextRow, 1].Value = person.Name;
                    ws.Cells[nextRow, 2].Value = (person.PriorWeekendShifts ?? baseline) + person.WeekendShifts;
                    nextRow++;
                }

                ws.Cells[1, 1].Value = "last updated";
                ws.Cells[1, 2].Value = DateTime.Today.ToString("d/M/yyyy", CultureInfo.InvariantCulture);
                ws.Cells[1, 3].Value = "sampai oncall " + MalayMonths[rotaEnd.Month - 1];

                package.SaveAs(new FileInfo(savePath));
            }
        }

        private static int FindHeaderRow(ExcelWorksheet ws)
        {
            int last = Math.Min(ws.Dimension.End.Row, 20);
            for (int r = 1; r <= last; r++)
            {
                string a = (ws.Cells[r, 1].Text ?? "").Trim().ToUpperInvariant();
                if (NameHeaders.Contains(a)) return r;
            }
            throw new InvalidDataException("Could not find a header row with NAMES/NAMA in column A.");
        }

        /// <summary>(row number, name, count or null) for each named row below the header.</summary>
        private static List<Tuple<int, string, int?>> ReadRows(ExcelWorksheet ws, int headerRow)
        {
            var rows = new List<Tuple<int, string, int?>>();
            for (int r = headerRow + 1; r <= ws.Dimension.End.Row; r++)
            {
                string name = (ws.Cells[r, 1].Text ?? "").Trim();
                if (name.Length == 0) continue;
                rows.Add(Tuple.Create(r, name, ReadCount(ws.Cells[r, 2].Value)));
            }
            return rows;
        }

        private static int? ReadCount(object value)
        {
            if (value == null) return null;
            if (value is double) return (int)Math.Round((double)value);

            string s = Convert.ToString(value, CultureInfo.InvariantCulture).Trim();
            return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double d)
                ? (int)Math.Round(d)
                : (int?)null;
        }
    }
}
