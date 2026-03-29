using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace TimeTable_Generator
{
    #region ShiftsAlgorithmV3 (Preferred Dates + Weekend/Holiday + Fairness + Caps)
    public class ShiftsAlgorithmV3
    {
        private static readonly Random _rng = new Random();

        public void AssignShifts(
            List<Person> people,
            DateTime startDate,
            DateTime endDate,
            List<DateTime> publicHolidays,
            Action<int> reportProgress)
        {
            if (people == null || people.Count == 0)
                throw new ArgumentException("People list is empty.");

            var holidayDates = new HashSet<DateTime>(publicHolidays.Select(d => d.Date));

            var allDates = GetAllDates(startDate, endDate);
            var weekends = allDates.Where(d => d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday).ToList();
            var weekdays = allDates.Except(weekends).ToList();

            var weekdaysExcludingHolidays = weekdays.Where(d => !holidayDates.Contains(d)).ToList();
            var weekendAndHolidays = weekends.Union(holidayDates).OrderBy(d => d).ToList();

            var assignedShifts = new HashSet<DateTime>();
            int totalAvailableShifts = weekdaysExcludingHolidays.Count + weekendAndHolidays.Count;

            // 1️⃣ Distribute total targets
            DistributeTotalTargets(people, totalAvailableShifts);

            // 2️⃣ Monthly targets
            var monthlyShiftTargets = CalculateMonthlyShiftTargets(people, weekdaysExcludingHolidays, weekendAndHolidays);

            int progress = 0;

            // 3️⃣ Assign all dates
            var allShiftDates = weekdaysExcludingHolidays.Concat(weekendAndHolidays).OrderBy(d => d).ToList();
            foreach (var day in allShiftDates)
            {
                bool isWeekend = weekendAndHolidays.Contains(day);

                // STEP 1: preferred-date candidates first
                var preferredCandidates = people
                    .Where(p => p.AssignPreferredDate) // now computed
                    .Where(p => p.LeaveDates == null || !p.LeaveDates.Any(ld => ld.Date == day.Date))
                    .Where(p => !HasAdjacentConflict(p, day))
                    .Where(p => (p.WeekdayShifts + p.WeekendShifts) < p.TotalShifts || p.ExtraShift)
                    .ToList();

                bool assigned = false;
                if (preferredCandidates.Any())
                {
                    var pick = preferredCandidates
                        .OrderBy(p => FairnessScore(p, day, isWeekend, monthlyShiftTargets))
                        .First();

                    AssignShiftToPerson(pick, day, isWeekend, assignedShifts, monthlyShiftTargets);
                    assigned = true;
                }

                // STEP 2: fallback assignment (weekend preference + quota)
                if (!assigned)
                {
                    AssignRegularShift(day, people, isWeekend, assignedShifts, monthlyShiftTargets);
                }

                // progress
                progress++;
                reportProgress(Math.Min((progress * 100) / Math.Max(1, totalAvailableShifts), 100));
            }

            // STEP 3: Validation
            ValidateAssignedShifts(people);
        }

        // ---------- TARGET DISTRIBUTION ----------

        private void DistributeTotalTargets(List<Person> people, int totalAvailableShifts)
        {
            int n = people.Count;
            int baseShare = totalAvailableShifts / n;
            int leftovers = totalAvailableShifts % n;

            foreach (var p in people)
                p.TotalShifts = baseShare;

            foreach (var p in people.OrderBy(p => p.LeaveDates?.Count ?? 0).ThenBy(p => p.Name))
            {
                if (leftovers == 0) break;
                p.TotalShifts++;
                leftovers--;
            }
        }

        private static int MonthKey(DateTime d) => d.Year * 100 + d.Month;

        private Dictionary<Person, Dictionary<int, int>> CalculateMonthlyShiftTargets(
            List<Person> people,
            List<DateTime> weekdaysExcludingHolidays,
            List<DateTime> weekendAndHolidays)
        {
            var byMonthAvail = weekdaysExcludingHolidays
                .Concat(weekendAndHolidays)
                .GroupBy(d => MonthKey(d))
                .ToDictionary(g => g.Key, g => g.Count());

            int totalAvail = byMonthAvail.Values.Sum();
            if (totalAvail == 0)
                throw new InvalidOperationException("No available shifts in the period.");

            var targets = new Dictionary<Person, Dictionary<int, int>>();
            foreach (var p in people)
            {
                targets[p] = new Dictionary<int, int>();
                foreach (var kv in byMonthAvail)
                {
                    int monthTarget = (int)Math.Floor((double)kv.Value * p.TotalShifts / totalAvail);
                    targets[p][kv.Key] = monthTarget;
                }

                int remainder = p.TotalShifts - targets[p].Values.Sum();
                foreach (var month in byMonthAvail.OrderByDescending(kv => kv.Value).Select(kv => kv.Key))
                {
                    if (remainder == 0) break;
                    targets[p][month]++;
                    remainder--;
                }
            }
            return targets;
        }

        // ---------- SHIFT ASSIGNMENT ----------

        private void AssignRegularShift(
            DateTime day,
            List<Person> people,
            bool isWeekend,
            HashSet<DateTime> assignedShifts,
            Dictionary<Person, Dictionary<int, int>> monthlyShiftTargets)
        {
            var candidates = people
                .Where(p => p.LeaveDates == null || !p.LeaveDates.Any(ld => ld.Date == day.Date))
                .Where(p => !HasAdjacentConflict(p, day))
                .Where(p => (p.WeekdayShifts + p.WeekendShifts) < p.TotalShifts || p.ExtraShift)
                .ToList();

            if (!candidates.Any()) return;

            // Weekend/Holiday preference
            if (isWeekend)
            {
                var weekendPreferred = candidates.Where(p => p.PreferWeekendHoliday).ToList();
                if (weekendPreferred.Any())
                    candidates = weekendPreferred;
            }

            var pickPerson = candidates
                .OrderBy(p => FairnessScore(p, day, isWeekend, monthlyShiftTargets))
                .First();

            AssignShiftToPerson(pickPerson, day, isWeekend, assignedShifts, monthlyShiftTargets);
        }

        private void AssignShiftToPerson(
            Person p,
            DateTime day,
            bool isWeekend,
            HashSet<DateTime> assignedShifts,
            Dictionary<Person, Dictionary<int, int>> monthlyShiftTargets)
        {
            p.AssignedShifts.Add(day.Date);
            if (isWeekend) p.WeekendShifts++; else p.WeekdayShifts++;
            p.LastAssignedShift = day.Date;
            assignedShifts.Add(day.Date);

            int monthKey = MonthKey(day);
            if (monthlyShiftTargets.TryGetValue(p, out var perMonth) &&
                perMonth.TryGetValue(monthKey, out int tgt) && tgt > 0)
            {
                perMonth[monthKey] = tgt - 1;
            }
        }

        // ---------- FAIRNESS ----------

        private double FairnessScore(
            Person p,
            DateTime date,
            bool isWeekend,
            Dictionary<Person, Dictionary<int, int>> monthlyShiftTargets)
        {
            int monthKey = MonthKey(date);
            int totalAssigned = p.WeekdayShifts + p.WeekendShifts;

            int remainingTarget = 1;
            if (monthlyShiftTargets.TryGetValue(p, out var perMonth) &&
                perMonth.TryGetValue(monthKey, out var tgt))
                remainingTarget = Math.Max(1, tgt);

            double score = (double)totalAssigned / remainingTarget;
            double balancePenalty = isWeekend
                ? Math.Max(0, p.WeekendShifts - p.WeekdayShifts) * 0.02
                : Math.Max(0, p.WeekdayShifts - p.WeekendShifts) * 0.01;

            double jitter = _rng.NextDouble() * 0.001;
            return score + balancePenalty + jitter;
        }

        // ---------- UTIL ----------

        private List<DateTime> GetAllDates(DateTime start, DateTime end)
        {
            var dates = new List<DateTime>();
            for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
                dates.Add(d);
            return dates;
        }

        private bool HasAdjacentConflict(Person p, DateTime date)
        {
            return p.AssignedShifts.Any(s => Math.Abs((date.Date - s.Date).Days) == 1);
        }

        private void ValidateAssignedShifts(List<Person> people)
        {
            foreach (var p in people)
            {
                var shifts = p.AssignedShifts.OrderBy(d => d).ToList();
                for (int i = 1; i < shifts.Count; i++)
                {
                    if ((shifts[i] - shifts[i - 1]).Days == 1)
                    {
                        MessageBox.Show(
                            $"Consecutive shifts detected for {p.Name} on {shifts[i - 1]:yyyy-MM-dd} and {shifts[i]:yyyy-MM-dd}.",
                            "Consecutive Shifts", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
        }
    }
    #endregion
}