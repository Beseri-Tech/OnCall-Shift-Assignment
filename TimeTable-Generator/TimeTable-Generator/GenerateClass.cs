using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Wordprocessing;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Color = System.Drawing.Color;


namespace TimeTable_Generator
{
    public class GenerateClass
    {
        
        public void GenerateTimetableExcel(List<Person> people, string filePath, DateTime startDate, DateTime endDate, List<DateTime> publicHolidays, List<DateTime> unassignedDates)
        {
            // Initialize the Excel package
            ExcelPackage.License.SetNonCommercialOrganization("My Noncommercial organization"); //This will also set the Company property to the organization name provided in the argument.

            using (ExcelPackage package = new ExcelPackage())
            {
                // Get all the dates between the start and end date
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

                // Step 1: Flatten the list of people and their shifts into a list of (Date, Name) pairs
                var shifts = people.SelectMany(person => person.AssignedShifts.Select(shiftDate => new { Date = shiftDate, Name = person.Name }))
                                   .OrderBy(shift => shift.Date)  // Step 2: Sort the list by Date
                                   .ToList();

                var unassignedPeople = people
                .Where(person => person.AssignedShifts == null || person.AssignedShifts.Count == 0)
                .ToList(); // <-- full Person objects, not just names

                // Step 2: Group the shifts by month
                var shiftsByMonth = shifts.GroupBy(shift => new { shift.Date.Year, shift.Date.Month }).ToList();

                // Step 3: Create a worksheet for each month
                if (unassignedPeople.Count > 0)
                {
                    ExcelWorksheet unassignedWorksheet = package.Workbook.Worksheets.Add("Assignment Error");
                    unassignedWorksheet.Cells[1, 1].Value = "Unassigned Dates";

                    int unassignedDatesRow = 2;

                    foreach (var date in unassignedDates)
                    {
                        unassignedWorksheet.Cells[unassignedDatesRow, 1].Value = date.ToString("dd/MM/yyyy");
                        unassignedDatesRow++;

                    }

                    unassignedWorksheet.Cells[unassignedDatesRow + 1, 1].Value = "People with No Assigned Shifts";
                    unassignedWorksheet.Cells[unassignedDatesRow + 1, 2].Value = "Leave Dates";

                    int unassignedRow = unassignedDatesRow + 2;
                    
                    foreach (var name in unassignedPeople)
                    {
                        unassignedWorksheet.Cells[unassignedRow, 1].Value = name.Name;
                        unassignedWorksheet.Cells[unassignedRow, 2].Value = name.LeaveDatesString;
                        unassignedRow++;
                    }
                    
                }
                foreach (var monthGroup in shiftsByMonth)
                {
                    var year = monthGroup.Key.Year;
                    var month = monthGroup.Key.Month;
                    var monthName = new DateTime(year, month, 1).ToString("MMMM yyyy");

                    // Create a worksheet for this month
                    ExcelWorksheet worksheet = package.Workbook.Worksheets.Add(monthName);

                    // Add headers
                    worksheet.Cells[1, 1].Value = "Date";
                    worksheet.Cells[1, 2].Value = "Day";
                    worksheet.Cells[1, 3].Value = "WeekDayShift";
                    worksheet.Cells[1, 4].Value = "WeekEndShift";

                    // Step 4: Get all dates of this month
                    var firstOfMonth = new DateTime(year, month, 1);
                    var lastOfMonth = firstOfMonth.AddMonths(1).AddDays(-1);
                    var allDatesInMonth = GetAllDates(firstOfMonth, lastOfMonth);

                    int row = 2;
                    foreach (var date in allDatesInMonth)
                    {
                        worksheet.Cells[row, 1].Value = date.ToShortDateString();  // Date
                        worksheet.Cells[row, 2].Value = date.DayOfWeek.ToString(); // Day

                        // Check if the date is a weekend or public holiday
                        bool isWeekendOrHoliday = weekendAndHolidays.Any(d => d.Date == date.Date);
                        bool isPublicHoliday = publicHolidays.Any(d => d.Date == date.Date);

                        if (isPublicHoliday)
                        {
                            worksheet.Cells[row, 2].Value += " (Public Holiday)";
                            worksheet.Cells[row, 2].Style.Fill.PatternType = ExcelFillStyle.Solid;
                            worksheet.Cells[row, 2].Style.Fill.BackgroundColor.SetColor(Color.PaleGreen);
                        }

                        // Find people assigned on this date
                        var assignedPeople = shifts.Where(s => s.Date.Date == date.Date).Select(s => s.Name).ToList();

                        if (isWeekendOrHoliday)
                        {
                            worksheet.Cells[row, 4].Value = assignedPeople.Count > 0 ? string.Join(", ", assignedPeople) : "";
                        }
                        else
                        {
                            worksheet.Cells[row, 3].Value = assignedPeople.Count > 0 ? string.Join(", ", assignedPeople) : "";
                        }

                        row++;
                    }
                
            }

                // Save the file
                FileInfo fileInfo = new FileInfo(filePath);
                package.SaveAs(fileInfo);
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
