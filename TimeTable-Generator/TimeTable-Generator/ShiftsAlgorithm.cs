using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using TimeTable_Generator;

namespace TimeTable_Generator
{
    #region Algorithm 1
    public class ShiftsAlgorithm
    {
        public void AssignShifts(List<Person> people, DateTime startDate, DateTime endDate, List<DateTime> publicHolidays, Action<int> reportProgress)
        {
            // Get all dates in the range
            List<DateTime> allDates = GetAllDates(startDate, endDate);

            // Separate weekends and weekdays
            List<DateTime> weekends = allDates.Where(date => date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday).ToList();
            List<DateTime> weekdays = allDates.Except(weekends).ToList();

            // Exclude public holidays from the list of weekdays
            var weekdaysExcludingHolidays = weekdays
                .Where(day => !publicHolidays.Any(holiday => holiday.Date == day.Date))
                .ToList();

            // Combine weekends and public holidays into one list for weekend shifts
            var weekendAndHolidays = weekends.Union(publicHolidays).ToList();

            // Initialize a HashSet to track assigned shifts
            HashSet<DateTime> assignedShifts = new HashSet<DateTime>();

            // Calculate total shifts (weekdays + weekends, excluding public holidays from weekdays)
            int totalAvailableShifts = weekdaysExcludingHolidays.Count + weekendAndHolidays.Count;

            // Automatically calculate the total shifts for each person
            foreach (var person in people)
            {
                person.TotalShifts = totalAvailableShifts / people.Count;
            }

            // Monthly shift targets
            var monthlyShiftTargets = CalculateMonthlyShiftTargets(people, allDates);

            int progress = 0;

            // First pass: Assign regular shifts (weekdays + weekend/public holidays)
            AssignRegularShifts(people, weekdaysExcludingHolidays, weekendAndHolidays, assignedShifts, reportProgress, ref progress, totalAvailableShifts, monthlyShiftTargets);

            // Second pass: Assign remaining shifts to people with ExtraShift flag
            AssignExtraShifts(people, weekdaysExcludingHolidays, weekendAndHolidays, assignedShifts, reportProgress, ref progress, totalAvailableShifts, monthlyShiftTargets);

            // Validate that all shifts have been assigned
            ValidateAssignedShifts(people, totalAvailableShifts);
        }

        // Helper method to calculate the ideal number of shifts per person per month
        private Dictionary<Person, Dictionary<int, int>> CalculateMonthlyShiftTargets(List<Person> people, List<DateTime> allDates)
        {
            var targets = new Dictionary<Person, Dictionary<int, int>>();

            // Group all dates by month
            var monthGroups = allDates.GroupBy(date => date.Month).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var person in people)
            {
                targets[person] = new Dictionary<int, int>();
                int totalShiftsForPerson = person.TotalShifts;  // The total number of shifts this person should get

                foreach (var month in monthGroups.Keys)
                {
                    int totalDaysInMonth = monthGroups[month].Count;
                    int monthProportion = (totalDaysInMonth * totalShiftsForPerson) / allDates.Count;  // Proportional distribution
                    targets[person][month] = monthProportion;
                }
            }

            return targets;
        }

        private void LogDebugMessage(string message)
        {
            Debug.WriteLine(message);
        }
        // Assign regular shifts (both weekdays and weekends) across months
        private void AssignRegularShifts(List<Person> people, List<DateTime> weekdays, List<DateTime> weekendAndHolidays, HashSet<DateTime> assignedShifts, Action<int> reportProgress, ref int progress, int totalShifts, Dictionary<Person, Dictionary<int, int>> monthlyShiftTargets)
        {
            // Assign weekday shifts
            foreach (DateTime weekday in weekdays)
            {
                AssignShift(people, weekday, isWeekend: false, ref progress, totalShifts, reportProgress, assignedShifts, monthlyShiftTargets);
            }

            // Assign weekend/public holiday shifts
            foreach (DateTime weekend in weekendAndHolidays)
            {
                AssignShift(people, weekend, isWeekend: true, ref progress, totalShifts, reportProgress, assignedShifts, monthlyShiftTargets);
            }
        }

        // Assign a single shift to an eligible person
        private bool AssignShift(List<Person> people, DateTime date, bool isWeekend, ref int progress, int totalShifts, Action<int> reportProgress, HashSet<DateTime> assignedShifts, Dictionary<Person, Dictionary<int, int>> monthlyShiftTargets, bool allowRelaxation = false)
        {
            bool shiftAssigned = false;
            string reason = "";  // Track the reason for skipping
            int month = date.Month;

            // Separate people with preferred dates and others
            var preferredPeople = people.Where(p => p.PreferredDates.Contains(date.Date)).ToList();
            var nonPreferredPeople = people.Where(p => !p.PreferredDates.Contains(date.Date)).ToList();

            // Shuffle the list of people before sorting
            Random rng = new Random();

            // Shuffle preferred people
            preferredPeople = preferredPeople.OrderBy(_ => rng.Next()).ToList();

            // Shuffle non-preferred people
            nonPreferredPeople = nonPreferredPeople.OrderBy(_ => rng.Next()).ToList();

            // Sort the shuffled lists
            var sortedPeople = preferredPeople.Any()
                ? preferredPeople.OrderBy(p => p.WeekendShifts + p.WeekdayShifts).ToList()
                : isWeekend
                    ? nonPreferredPeople.OrderBy(p => p.WeekendShifts).ThenBy(p => p.WeekdayShifts).ToList()
                    : nonPreferredPeople.OrderBy(p => p.WeekdayShifts).ThenBy(p => p.WeekendShifts).ToList();

            foreach (var currentPerson in sortedPeople)
            {
                // Relax the constraint for monthly shift target if necessary
                if (!allowRelaxation && monthlyShiftTargets[currentPerson][month] <= 0)
                {
                    reason = $"{currentPerson.Name} has reached their monthly shift limit for {month}.";
                    continue;  // Move to the next person
                }

                // Check if the person is on leave or has consecutive shift issues
                if (currentPerson.LeaveDates.Contains(date.Date))
                {
                    reason = $"{currentPerson.Name} is on leave for {date.ToShortDateString()}.";
                }
                else if (currentPerson.AssignedShifts.Any(existingShift => Math.Abs((date - existingShift).Days) == 1))
                {
                    reason = $"{currentPerson.Name} has a consecutive shift issue for {date.ToShortDateString()}.";
                }
                else
                {
                    // Assign the shift to the person
                    currentPerson.AssignedShifts.Add(date);
                    if (isWeekend)
                    {
                        currentPerson.WeekendShifts++;
                    }
                    else
                    {
                        currentPerson.WeekdayShifts++;
                    }
                    currentPerson.LastAssignedShift = date;
                    shiftAssigned = true;

                    // Add the assigned shift to the HashSet to track it
                    assignedShifts.Add(date);

                    // Decrease monthly shift target, unless we're relaxing the constraint
                    if (!allowRelaxation)
                    {
                        monthlyShiftTargets[currentPerson][month]--;
                    }

                    break;  // Exit once a shift is assigned
                }
            }

            // Fallback: If no one fits under the strict constraints, relax the rules and try again
            if (!shiftAssigned && !allowRelaxation)
            {
                // Try again with relaxed constraints (allow people to exceed their monthly targets)
                return AssignShift(people, date, isWeekend, ref progress, totalShifts, reportProgress, assignedShifts, monthlyShiftTargets, allowRelaxation: true);
            }

            if (!shiftAssigned)
            {
                // If reason is still empty, ensure we have a default reason set
                if (string.IsNullOrEmpty(reason))
                {
                    reason = "No eligible person fits the criteria for this shift.";
                }

                // Log skipped weekends specifically
                if (isWeekend)
                {
                    MessageBox.Show($"No eligible person found for weekend/public holiday {date.ToShortDateString()}.\nReason: {reason}", "Skipped Weekend", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    MessageBox.Show($"No eligible person found for {date.ToShortDateString()}.\nReason: {reason}", "Skipped Shift", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else
            {
                progress++;
                reportProgress((progress * 100) / totalShifts);
            }

            return shiftAssigned;  // Return whether the shift was assigned
        }

        // Assign extra shifts after regular shifts are assigned
        private void AssignExtraShifts(List<Person> people, List<DateTime> weekdays, List<DateTime> weekendAndHolidays, HashSet<DateTime> assignedShifts, Action<int> reportProgress, ref int progress, int totalShifts, Dictionary<Person, Dictionary<int, int>> monthlyShiftTargets)
        {
            // Calculate the remaining shifts (weekdays and weekends that were not assigned)
            var remainingShifts = weekdays.Concat(weekendAndHolidays)
                                          .Except(assignedShifts)
                                          .ToList();  // Create the list of unassigned shifts

            // Sort people with ExtraShift flag and assign remaining shifts
            foreach (var date in remainingShifts)
            {
                bool shiftAssigned = false;
                int month = date.Month;

                // Prioritize people with ExtraShift flag, maintaining balance of total shifts
                var extraShiftPeople = people
                    .Where(p => p.ExtraShift /*&& monthlyShiftTargets[p][month] > 0*/)  // Only consider people eligible for extra shifts and who need shifts in this month
                    .OrderBy(p => p.WeekendShifts + p.WeekdayShifts)  // Prioritize people with fewer total shifts (weekday + weekend)
                    .ThenBy(p => Math.Abs(p.WeekendShifts - p.WeekdayShifts))  // Prioritize balancing weekday and weekend shifts
                    .ToList();

                foreach (var currentPerson in extraShiftPeople)
                {
                    if (!currentPerson.LeaveDates.Contains(date.Date) &&
                        (!currentPerson.LastAssignedShift.HasValue || (date - currentPerson.LastAssignedShift.Value).Days > 1))
                    {
                        // Assign the shift (weekend or weekday based on the day of the week)
                        currentPerson.AssignedShifts.Add(date);
                        if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday || weekendAndHolidays.Contains(date))
                        {
                            currentPerson.WeekendShifts++;
                        }
                        else
                        {
                            currentPerson.WeekdayShifts++;
                        }
                        currentPerson.LastAssignedShift = date;
                        shiftAssigned = true;

                        // Decrease the monthly shift target
                        monthlyShiftTargets[currentPerson][month]--;
                        break; // Exit the loop once a shift is assigned
                    }
                }

                if (shiftAssigned)
                {
                    progress++;
                    reportProgress(Math.Min((progress * 100) / totalShifts, 100));
                }
            }
        }

        // Validation for assigned shifts (not implemented yet)
        private void ValidateAssignedShifts(List<Person> people, int totalAvailableShifts)
        {
            ValidateNoConsecutiveShifts(people);
        }

        private List<DateTime> GetAllDates(DateTime startDate, DateTime endDate)
        {
            List<DateTime> allDates = new List<DateTime>();
            for (DateTime date = startDate; date <= endDate; date = date.AddDays(1))
            {
                allDates.Add(date);
            }
            return allDates;
        }
        private void ValidateNoConsecutiveShifts(List<Person> people)
        {
            foreach (var person in people)
            {
                // Ensure shifts are sorted
                var shifts = person.AssignedShifts.OrderBy(d => d).ToList();

                // Check for consecutive shifts
                for (int i = 1; i < shifts.Count; i++)
                {
                    if (((shifts[i] - shifts[i - 1]).Days >= 1) && ((shifts[i] - shifts[i - 1]).Days <= 2))
                    {
                        MessageBox.Show(
                            $"Consecutive shifts detected for {person.Name} on {shifts[i - 1]:yyyy-MM-dd} and {shifts[i]:yyyy-MM-dd}."
                        );
                    }
                }
            }
        }



    }
    #endregion



}
