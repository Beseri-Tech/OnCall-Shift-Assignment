using DocumentFormat.OpenXml.Office2010.Excel;
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
        
        public void GenerateTimetableExcel(List<Person> people, string filePath, DateTime startDate, DateTime endDate, List<DateTime> publicHolidays)
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

                // Step 2: Group the shifts by month
                var shiftsByMonth = shifts.GroupBy(shift => new { shift.Date.Year, shift.Date.Month }).ToList();

                // Step 3: Create a worksheet for each month
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

                    // Step 4: Populate the Excel worksheet with shifts for this month
                    int row = 2;
                    foreach (var shift in monthGroup)
                    {
                        worksheet.Cells[row, 1].Value = shift.Date.ToShortDateString();  // Date of the shift
                        worksheet.Cells[row, 2].Value = shift.Date.DayOfWeek.ToString();  // Day of the shift

                        if (weekendAndHolidays.Contains(shift.Date))
                        {

                            if (publicHolidays.Contains(shift.Date))
                            {
                                worksheet.Cells[row, 2].Value = worksheet.Cells[row, 2].Value + " (Public Holiday)";
                                worksheet.Cells[row, 2].Style.Fill.PatternType = ExcelFillStyle.Solid;
                                worksheet.Cells[row, 2].Style.Fill.BackgroundColor.SetColor(Color.PaleGreen);
                            }
                            worksheet.Cells[row, 4].Value = shift.Name;
                        }
                        else
                        {
                            worksheet.Cells[row, 3].Value = shift.Name;
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
