using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

namespace TimeTable_Generator
{
    #region Algorithm 2 (Improved + Global Cap + Zero-First Fair Fill)
    public class ShiftsAlgorithm
    {
        // One RNG to avoid deterministic shuffles when called in quick succession
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

            // Normalize holiday dates to Date (no time component)
            var holidayDates = new HashSet<DateTime>(publicHolidays.Select(d => d.Date));

            // All calendar days
            List<DateTime> allDates = GetAllDates(startDate, endDate);

            // Weekends
            List<DateTime> weekends = allDates
                .Where(date => date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                .Select(d => d.Date)
                .ToList();

            // Weekdays (calendar weekdays only)
            List<DateTime> weekdays = allDates
                .Where(date => date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
                .Select(d => d.Date)
                .ToList();

            // Exclude public holidays from weekdays
            var weekdaysExcludingHolidays = weekdays
                .Where(day => !holidayDates.Contains(day))
                .ToList();

            // Weekend shifts pool = weekends ∪ public holidays
            var weekendAndHolidays = weekends
                .Union(holidayDates)
                .OrderBy(d => d)
                .ToList();

            // Set of assigned dates (Date only)
            HashSet<DateTime> assignedShifts = new HashSet<DateTime>();

            // Total available shifts
            int totalAvailableShifts = weekdaysExcludingHolidays.Count + weekendAndHolidays.Count;

            // Base share + leftovers to avoid starving anyone
            DistributeTotalTargets(people, totalAvailableShifts);

            // Monthly targets proportional to *available* shifts in each month
            var monthlyShiftTargets = CalculateMonthlyShiftTargets(
                people,
                weekdaysExcludingHolidays,
                weekendAndHolidays);

            int progress = 0;

            // PASS 1: Assign across all available weekdays and weekend/holidays using fairness scoring (respect cap)
            AssignRegularShifts(
                people,
                weekdaysExcludingHolidays,
                weekendAndHolidays,
                assignedShifts,
                reportProgress,
                ref progress,
                totalAvailableShifts,
                monthlyShiftTargets);

            // PASS 2A: Try to fill remaining dates fairly (prefer people with 0, still respect cap)
            FillRemainingShiftsFair(
                people,
                weekdaysExcludingHolidays,
                weekendAndHolidays,
                assignedShifts,
                reportProgress,
                ref progress,
                totalAvailableShifts,
                monthlyShiftTargets);

            // PASS 2B: If still any left, give to ExtraShift folks (allow exceeding caps)
            FillRemainingWithExtraShift(
                people,
                weekdaysExcludingHolidays,
                weekendAndHolidays,
                assignedShifts,
                reportProgress,
                ref progress,
                totalAvailableShifts,
                monthlyShiftTargets);

            // Validate (adjacent day check)
            ValidateAssignedShifts(people, totalAvailableShifts);
        }

        // ---------- TARGET DISTRIBUTION ----------

        private void DistributeTotalTargets(List<Person> people, int totalAvailableShifts)
        {
            int n = people.Count;
            int baseShare = totalAvailableShifts / n;
            int leftovers = totalAvailableShifts % n;

            foreach (var p in people)
                p.TotalShifts = baseShare;

            // Heuristic: give leftovers to people with fewer leave days, then by name for stability
            foreach (var p in people
                         .OrderBy(p => p.LeaveDates?.Count ?? 0)
                         .ThenBy(p => p.Name))
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
            // Available shifts by (Year,Month)
            var byMonthAvail = weekdaysExcludingHolidays
                .Concat(weekendAndHolidays)
                .GroupBy(d => MonthKey(d))
                .ToDictionary(g => g.Key, g => g.Count());

            int totalAvail = byMonthAvail.Values.Sum();
            if (totalAvail == 0)
                throw new InvalidOperationException("No available shifts in the selected period.");

            var targets = new Dictionary<Person, Dictionary<int, int>>();

            foreach (var p in people)
            {
                targets[p] = new Dictionary<int, int>();

                // Initial proportional split by availability per month
                foreach (KeyValuePair<int, int> kv in byMonthAvail)
                {
                    int monthKey = kv.Key;
                    int monthAvail = kv.Value;
                    int monthTarget = (int)Math.Floor((double)monthAvail * p.TotalShifts / totalAvail);
                    targets[p][monthKey] = monthTarget;
                }

                // Distribute remainder so the sum equals p.TotalShifts
                int assigned = targets[p].Values.Sum();
                int remainder = p.TotalShifts - assigned;
                if (remainder > 0)
                {
                    foreach (var month in byMonthAvail.OrderByDescending(kv => kv.Value).Select(kv => kv.Key))
                    {
                        if (remainder == 0) break;
                        targets[p][month]++;
                        remainder--;
                    }
                }
            }

            return targets;
        }

        // ---------- ASSIGNMENT PASSES ----------

        private void AssignRegularShifts(
            List<Person> people,
            List<DateTime> weekdays,
            List<DateTime> weekendAndHolidays,
            HashSet<DateTime> assignedShifts,
            Action<int> reportProgress,
            ref int progress,
            int totalShifts,
            Dictionary<Person, Dictionary<int, int>> monthlyShiftTargets)
        {
            // Weekdays
            foreach (var day in weekdays)
            {
                AssignShift(
                    people,
                    day,
                    isWeekend: false,
                    assignedShifts,
                    reportProgress,
                    ref progress,
                    totalShifts,
                    monthlyShiftTargets,
                    softOnly: true,
                    respectGlobalCap: true); // respect cap
            }

            // Weekend/Public Holidays
            foreach (var day in weekendAndHolidays)
            {
                AssignShift(
                    people,
                    day,
                    isWeekend: true,
                    assignedShifts,
                    reportProgress,
                    ref progress,
                    totalShifts,
                    monthlyShiftTargets,
                    softOnly: true,
                    respectGlobalCap: true); // respect cap
            }
        }

        private void FillRemainingShiftsFair(
            List<Person> people,
            List<DateTime> weekdays,
            List<DateTime> weekendAndHolidays,
            HashSet<DateTime> assignedShifts,
            Action<int> reportProgress,
            ref int progress,
            int totalShifts,
            Dictionary<Person, Dictionary<int, int>> monthlyShiftTargets)
        {
            var remaining = weekdays.Concat(weekendAndHolidays)
                                    .Select(d => d.Date)
                                    .Where(d => !assignedShifts.Contains(d))
                                    .OrderBy(d => d)
                                    .ToList();

            foreach (var day in remaining)
            {
                bool isWeekend = IsWeekendOrHoliday(day, weekendAndHolidays);

                // Try with zero-first bias & cap
                bool assigned = AssignShift(
                    people,
                    day,
                    isWeekend,
                    assignedShifts,
                    reportProgress,
                    ref progress,
                    totalShifts,
                    monthlyShiftTargets,
                    softOnly: true,
                    respectGlobalCap: true); // respect cap

                if (!assigned)
                {
                    // nothing; will be handled in ExtraShift pass
                }
            }
        }

        private void FillRemainingWithExtraShift(
            List<Person> people,
            List<DateTime> weekdays,
            List<DateTime> weekendAndHolidays,
            HashSet<DateTime> assignedShifts,
            Action<int> reportProgress,
            ref int progress,
            int totalShifts,
            Dictionary<Person, Dictionary<int, int>> monthlyShiftTargets)
        {
            var remaining = weekdays.Concat(weekendAndHolidays)
                                    .Select(d => d.Date)
                                    .Where(d => !assignedShifts.Contains(d))
                                    .OrderBy(d => d)
                                    .ToList();

            foreach (var day in remaining)
            {
                bool isWeekend = IsWeekendOrHoliday(day, weekendAndHolidays);

                // Prioritize ExtraShift folks, allow exceeding global caps if necessary
                var extraPeople = people.Where(p => p.ExtraShift).ToList();
                bool assigned = AssignShift(
                                extraPeople,
                                day,
                                isWeekend,
                                assignedShifts,
                                reportProgress,
                                ref progress,
                                totalShifts,
                                monthlyShiftTargets,
                                softOnly: false,
                                respectGlobalCap: false);


                if (!assigned)
                {
                    var reason = "No eligible person fits the criteria for this shift.";
                    var caption = isWeekend ? "Skipped Weekend/Public Holiday" : "Skipped Shift";
                    MessageBox.Show($"No eligible person found for {day:yyyy-MM-dd}.\nReason: {reason}", caption,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        // ---------- CORE ASSIGNMENT ----------

        /// <summary>
        /// Assign a single date to the fairest eligible person.
        /// - Soft monthly caps (decrement if >0; allow exceed when softOnly=false)
        /// - Optional global cap: don't assign if person already reached TotalShifts
        /// - Zero-first bias: if any candidate has 0 total shifts, pick among those
        /// </summary>
        private bool AssignShift(
            List<Person> people,
            DateTime date,
            bool isWeekend,
            HashSet<DateTime> assignedShifts,
            Action<int> reportProgress,
            ref int progress,
            int totalShifts,
            Dictionary<Person, Dictionary<int, int>> monthlyShiftTargets,
            bool softOnly,
            bool respectGlobalCap)
        {
            // Already assigned elsewhere?
            if (assignedShifts.Contains(date.Date))
                return true;

            int mKey = MonthKey(date);

            // Candidates: not on leave, no adjacent-day conflict
            var candidates = people
                .Where(p => p.LeaveDates == null || !p.LeaveDates.Contains(date.Date))
                .Where(p => !HasAdjacentConflict(p, date))
                .ToList();

            if (respectGlobalCap)
            {
                candidates = candidates
                    .Where(p => (p.WeekdayShifts + p.WeekendShifts) < p.TotalShifts)
                    .ToList();
            }

            if (!candidates.Any())
                return false;

            // Zero-first bias: if anyone has 0 total shifts, restrict to them
            bool existsZero = candidates.Any(p => (p.WeekdayShifts + p.WeekendShifts) == 0);
            if (existsZero)
            {
                candidates = candidates
                    .Where(p => (p.WeekdayShifts + p.WeekendShifts) == 0)
                    .ToList();
            }

            // Select person with lowest fairness score
            var pick = candidates
                .OrderBy(p => FairnessScore(p, mKey, date, isWeekend, monthlyShiftTargets))
                .First();

            // Assign
            pick.AssignedShifts.Add(date.Date);
            if (isWeekend) pick.WeekendShifts++; else pick.WeekdayShifts++;
            pick.LastAssignedShift = date.Date;
            assignedShifts.Add(date.Date);

            // Decrement monthly target if still > 0 (soft cap)
            if (monthlyShiftTargets.TryGetValue(pick, out var perMonth) &&
                perMonth.TryGetValue(mKey, out var tgt) && tgt > 0)
            {
                perMonth[mKey] = tgt - 1;
            }
            else if (softOnly)
            {
                // In soft-only mode and target already 0, we still allow assignment to avoid starvation.
                // No decrement needed.
            }

            // Progress
            progress++;
            reportProgress(Math.Min((progress * 100) / Math.Max(1, totalShifts), 100));
            return true;
        }

        /// <summary>
        /// Fairness score: lower is better.
        /// - Base: totalAssigned / max(1, targetRemainingThisMonth)
        /// - Preference bonus: -0.1 if prefers this date
        /// - Balance nudges weekday/weekend (small)
        /// - Tiny jitter for tie-breaks
        /// </summary>
        private double FairnessScore(
            Person p,
            int monthKey,
            DateTime date,
            bool isWeekend,
            Dictionary<Person, Dictionary<int, int>> monthlyShiftTargets)
        {
            int totalAssigned = p.WeekdayShifts + p.WeekendShifts;

            int remainingTarget = 1;
            if (monthlyShiftTargets.TryGetValue(p, out var perMonth) &&
                perMonth.TryGetValue(monthKey, out var tgt))
            {
                remainingTarget = Math.Max(1, tgt);
            }

            double baseScore = (double)totalAssigned / remainingTarget;

            bool prefers = p.PreferredDates != null && p.PreferredDates.Contains(date.Date);
            double prefBonus = prefers ? -0.10 : 0.0;

            // Small balance nudges (keep subtle so it doesn’t dominate)
            double balancePenalty = 0.0;
            if (isWeekend)
            {
                // If p already has many more weekend shifts, nudge up a bit
                balancePenalty = Math.Max(0, (p.WeekendShifts - p.WeekdayShifts)) * 0.02;
            }
            else
            {
                // If p already has many more weekdays, nudge up a bit
                balancePenalty = Math.Max(0, (p.WeekdayShifts - p.WeekendShifts)) * 0.01;
            }

            double jitter = _rng.NextDouble() * 0.001;
            return baseScore + prefBonus + balancePenalty + jitter;
        }

        private bool HasAdjacentConflict(Person p, DateTime date)
        {
            if (p.AssignedShifts == null || p.AssignedShifts.Count == 0)
                return false;

            // Only disallow exact adjacent day (±1)
            return p.AssignedShifts.Any(s => Math.Abs((date.Date - s.Date).Days) == 1);
        }

        private bool IsWeekendOrHoliday(DateTime date, List<DateTime> weekendAndHolidays)
        {
            if (weekendAndHolidays.Contains(date.Date)) return true;
            var dow = date.DayOfWeek;
            return dow == DayOfWeek.Saturday || dow == DayOfWeek.Sunday;
        }

        // ---------- VALIDATION ----------

        private void ValidateAssignedShifts(List<Person> people, int totalAvailableShifts)
        {
            ValidateNoConsecutiveShifts(people);
        }

        private void ValidateNoConsecutiveShifts(List<Person> people)
        {
            foreach (var person in people)
            {
                var shifts = person.AssignedShifts?.OrderBy(d => d).ToList() ?? new List<DateTime>();
                for (int i = 1; i < shifts.Count; i++)
                {
                    if ((shifts[i] - shifts[i - 1]).Days == 1)
                    {
                        MessageBox.Show(
                            $"Consecutive shifts detected for {person.Name} on {shifts[i - 1]:yyyy-MM-dd} and {shifts[i]:yyyy-MM-dd}.",
                            "Consecutive Shifts",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                }
            }
        }

        // ---------- UTIL ----------

        private List<DateTime> GetAllDates(DateTime startDate, DateTime endDate)
        {
            List<DateTime> allDates = new List<DateTime>();
            for (DateTime date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
                allDates.Add(date);
            return allDates;
        }

        private void LogDebugMessage(string message) => Debug.WriteLine(message);
    }
    #endregion
}
