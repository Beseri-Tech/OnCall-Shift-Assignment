using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

namespace TimeTable_Generator
{
    public class ShiftsAlgorithmV2
    {
        private static readonly Random _rng = new Random();

        private const double ADJACENT_PENALTY = 0.5;
        private const int BACKTRACK_DEPTH = 1;

        public void AssignShifts(
            List<Person> people,
            DateTime startDate,
            DateTime endDate,
            List<DateTime> publicHolidays,
            List<DateTime> unassignedDates,
            Action<int> reportProgress)
        {
            if (people == null || people.Count == 0)
                throw new ArgumentException("People list is empty.");

            var holidays = new HashSet<DateTime>(publicHolidays.Select(d => d.Date));
            var allDates = GetAllDates(startDate, endDate);

            var weekends = allDates
                .Where(d => d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday)
                .Select(d => d.Date)
                .ToList();

            var weekdays = allDates
                .Where(d => d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
                .Select(d => d.Date)
                .Where(d => !holidays.Contains(d))
                .ToList();

            var weekendAndHolidays = weekends
                .Union(holidays)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            var allShiftDates = weekdays
                .Concat(weekendAndHolidays)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            var assigned = new Dictionary<DateTime, Person>();
            int totalShifts = allShiftDates.Count;

            DistributeTotalTargets(people, totalShifts);

            var monthlyTargets = CalculateMonthlyTargetsWithAvailability(
                people,
                allShiftDates);

            int progress = 0;

            foreach (var day in allShiftDates)
            {
                bool isWeekend = IsWeekend(day, weekendAndHolidays);

                if (!TryAssignWithBacktracking(
                    people,
                    day,
                    isWeekend,
                    assigned,
                    monthlyTargets,
                    BACKTRACK_DEPTH))
                {
                    MessageBox.Show($"Failed to assign {day:yyyy-MM-dd}");
                    unassignedDates.Add(day);
                }

                progress++;
                reportProgress(progress * 100 / totalShifts);
            }
        }

        // ---------------- TARGETS ----------------

        private void DistributeTotalTargets(List<Person> people, int total)
        {
            int baseShare = total / people.Count;
            int leftover = total % people.Count;

            foreach (var p in people)
                p.TotalShifts = baseShare;

            foreach (var p in people.OrderBy(p => p.LeaveDates?.Count ?? 0))
            {
                if (leftover-- <= 0) break;
                p.TotalShifts++;
            }
        }

        private Dictionary<Person, Dictionary<int, int>> CalculateMonthlyTargetsWithAvailability(
            List<Person> people,
            List<DateTime> allDates)
        {
            var result = new Dictionary<Person, Dictionary<int, int>>();
            var monthGroups = allDates.GroupBy(MonthKey)
                                      .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var p in people)
            {
                result[p] = new Dictionary<int, int>();

                int totalAvailable = allDates.Count(d =>
                    p.LeaveDates == null || !p.LeaveDates.Any(ld => ld.Date == d.Date));

                foreach (var kv in monthGroups)
                {
                    int month = kv.Key;
                    var dates = kv.Value;

                    int available = dates.Count(d =>
                        p.LeaveDates == null || !p.LeaveDates.Any(ld => ld.Date == d.Date));

                    int target = (int)Math.Round(
                        (double)available / Math.Max(1, totalAvailable) * p.TotalShifts);

                    result[p][month] = target;
                }
            }

            return result;
        }

        // ---------------- ASSIGNMENT ----------------

        private bool TryAssignWithBacktracking(
            List<Person> people,
            DateTime date,
            bool isWeekend,
            Dictionary<DateTime, Person> assigned,
            Dictionary<Person, Dictionary<int, int>> monthlyTargets,
            int depth)
        {
            var candidates = GetCandidates(people, date, isWeekend);

            if (!candidates.Any())
                return false;

            var ordered = candidates
                .OrderBy(p => FairnessScore(p, date, isWeekend, monthlyTargets))
                .ToList();

            foreach (var person in ordered)
            {
                Assign(person, date, isWeekend, assigned);

                // success
                return true;
            }

            return false;
        }

        private List<Person> GetCandidates(
         List<Person> people,
         DateTime date,
         bool isWeekend)
        {
            var baseCandidates = people
                .Where(p => p.LeaveDates == null || !p.LeaveDates.Any(ld => ld.Date == date.Date))
                .Where(p => (p.WeekdayShifts + p.WeekendShifts) < p.TotalShifts || p.ExtraShift)
                .ToList();

            if (!baseCandidates.Any())
                return baseCandidates;

            // ✅ PRIORITY 1: Preferred specific date
            var preferredDatePeople = baseCandidates
                .Where(p => p.PreferredDates != null && p.PreferredDates.Any(d => d.Date == date.Date))
                .ToList();

            if (preferredDatePeople.Any())
                return preferredDatePeople;

            // ✅ PRIORITY 2: Weekend/Holiday preference
            if (isWeekend)
            {
                var weekendPreferred = baseCandidates
                    .Where(p => p.PreferWeekendHoliday)
                    .ToList();

                if (weekendPreferred.Any())
                    return weekendPreferred;
            }

            // ✅ FALLBACK
            return baseCandidates;
        }
        private void Assign(Person p, DateTime date, bool isWeekend, Dictionary<DateTime, Person> assigned)
        {
            assigned[date] = p;
            p.AssignedShifts.Add(date);

            if (isWeekend) p.WeekendShifts++;
            else p.WeekdayShifts++;
        }

        private void Unassign(Person p, DateTime date, Dictionary<DateTime, Person> assigned)
        {
            assigned.Remove(date);
            p.AssignedShifts.Remove(date);

            if (p.WeekendShifts > 0) p.WeekendShifts--;
            if (p.WeekdayShifts > 0) p.WeekdayShifts--;
        }

        // ---------------- FAIRNESS ----------------

        private double FairnessScore(
            Person p,
            DateTime date,
            bool isWeekend,
            Dictionary<Person, Dictionary<int, int>> monthlyTargets)
        {
            int totalAssigned = p.WeekdayShifts + p.WeekendShifts;
            int month = MonthKey(date);

            int expected = 0;
            if (monthlyTargets.TryGetValue(p, out var m) && m.TryGetValue(month, out var val))
                expected = val;

            double score = totalAssigned - expected;

            // Soft adjacency penalty
            if (HasAdjacent(p, date))
                score += ADJACENT_PENALTY;

            // Preference bonus
            if (p.PreferredDates != null && p.PreferredDates.Any(d => d.Date == date.Date))
                score -= 0.2;

            // Optional: small boost for weekend-preferred (if not hard-filtered)
            if (isWeekend && p.PreferWeekendHoliday)
                score -= 0.1;

            score += _rng.NextDouble() * 0.01;

            return score;
        }

        private bool HasAdjacent(Person p, DateTime date)
        {
            return p.AssignedShifts.Any(s => Math.Abs((s.Date - date.Date).Days) == 1);
        }

        // ---------------- UTIL ----------------

        private bool IsWeekend(DateTime d, List<DateTime> weekendList)
        {
            return weekendList.Contains(d.Date);
        }

        private int MonthKey(DateTime d) => d.Year * 100 + d.Month;

        private List<DateTime> GetAllDates(DateTime start, DateTime end)
        {
            var list = new List<DateTime>();
            for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
                list.Add(d);
            return list;
        }

        private void LogDebugMessage(string message)
        {
            Debug.WriteLine(message);
        }
    }
}