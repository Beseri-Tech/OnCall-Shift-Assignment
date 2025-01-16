using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

namespace TimeTable_Generator
{
    public class DoubleShiftsAlgorithm
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

            // Calculate total shifts (two people per date)
            int totalAvailableShifts = (weekdaysExcludingHolidays.Count + weekendAndHolidays.Count) * 2;

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

        private Dictionary<Person, Dictionary<int, int>> CalculateMonthlyShiftTargets(List<Person> people, List<DateTime> allDates)
        {
            var targets = new Dictionary<Person, Dictionary<int, int>>();

            // Group all dates by month
            var monthGroups = allDates.GroupBy(date => date.Month).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var person in people)
            {
                targets[person] = new Dictionary<int, int>();
                int totalShiftsForPerson = person.TotalShifts;

                foreach (var month in monthGroups.Keys)
                {
                    int totalDaysInMonth = monthGroups[month].Count;
                    int monthProportion = (totalDaysInMonth * totalShiftsForPerson) / allDates.Count;
                    targets[person][month] = monthProportion;
                }
            }

            return targets;
        }

        private void AssignRegularShifts(List<Person> people, List<DateTime> weekdays, List<DateTime> weekendAndHolidays, HashSet<DateTime> assignedShifts, Action<int> reportProgress, ref int progress, int totalShifts, Dictionary<Person, Dictionary<int, int>> monthlyShiftTargets)
        {
            foreach (DateTime date in weekdays.Concat(weekendAndHolidays))
            {
                AssignShift(people, date, isWeekend: weekendAndHolidays.Contains(date), ref progress, totalShifts, reportProgress, assignedShifts, monthlyShiftTargets);
            }
        }

        private bool AssignShift(List<Person> people, DateTime date, bool isWeekend, ref int progress, int totalShifts, Action<int> reportProgress, HashSet<DateTime> assignedShifts, Dictionary<Person, Dictionary<int, int>> monthlyShiftTargets, bool allowRelaxation = false)
        {
            bool shiftAssigned = false;
            int month = date.Month;
            List<Person> assignedPeople = new List<Person>();
            Random rng = new Random();

            // Shuffle preferred and non-preferred people before sorting
            var preferredPeople = people.Where(p => p.PreferredDates.Contains(date.Date)).OrderBy(_ => rng.Next()).ToList();
            var nonPreferredPeople = people.Where(p => !p.PreferredDates.Contains(date.Date)).OrderBy(_ => rng.Next()).ToList();

            // Sort the shuffled lists
            var sortedPeople = preferredPeople.Any()
                ? preferredPeople.OrderBy(p => p.WeekendShifts + p.WeekdayShifts).ToList()
                : isWeekend
                    ? nonPreferredPeople.OrderBy(p => p.WeekendShifts).ThenBy(p => p.WeekdayShifts).ToList()
                    : nonPreferredPeople.OrderBy(p => p.WeekdayShifts).ThenBy(p => p.WeekendShifts).ToList();

            foreach (var currentPerson in sortedPeople)
            {
                if (!allowRelaxation && monthlyShiftTargets[currentPerson][month] <= 0)
                {
                    continue;
                }

                if (currentPerson.LeaveDates.Contains(date.Date) ||
                    currentPerson.AssignedShifts.Any(existingShift => Math.Abs((date - existingShift).Days) <= 1) ||
                    assignedPeople.Contains(currentPerson))
                {
                    continue;
                }

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
                assignedPeople.Add(currentPerson);

                if (!allowRelaxation)
                {
                    monthlyShiftTargets[currentPerson][month]--;
                }

                if (assignedPeople.Count == 2)
                {
                    break;
                }
            }

            if (assignedPeople.Count < 2 && !allowRelaxation)
            {
                return AssignShift(people, date, isWeekend, ref progress, totalShifts, reportProgress, assignedShifts, monthlyShiftTargets, allowRelaxation: true);
            }

            if (assignedPeople.Count == 2)
            {
                assignedShifts.Add(date);
                progress += 2;
                reportProgress((progress * 100) / totalShifts);
                shiftAssigned = true;
            }

            return shiftAssigned;
        }

        private void AssignExtraShifts(List<Person> people, List<DateTime> weekdays, List<DateTime> weekendAndHolidays, HashSet<DateTime> assignedShifts, Action<int> reportProgress, ref int progress, int totalShifts, Dictionary<Person, Dictionary<int, int>> monthlyShiftTargets)
        {
            var remainingShifts = weekdays.Concat(weekendAndHolidays).Except(assignedShifts).ToList();

            foreach (var date in remainingShifts)
            {
                AssignShift(people, date, isWeekend: weekendAndHolidays.Contains(date), ref progress, totalShifts, reportProgress, assignedShifts, monthlyShiftTargets);
            }
        }

        private void ValidateAssignedShifts(List<Person> people, int totalAvailableShifts)
        {
            ValidateNoConsecutiveShifts(people);
        }

        private void ValidateNoConsecutiveShifts(List<Person> people)
        {
            foreach (var person in people)
            {
                var shifts = person.AssignedShifts.OrderBy(d => d).ToList();

                for (int i = 1; i < shifts.Count; i++)
                {
                    if ((shifts[i] - shifts[i - 1]).Days <= 1)
                    {
                        MessageBox.Show($"Consecutive shifts detected for {person.Name} on {shifts[i - 1]:yyyy-MM-dd} and {shifts[i]:yyyy-MM-dd}.");
                    }
                }
            }
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
    }


}
