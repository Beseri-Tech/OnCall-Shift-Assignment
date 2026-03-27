using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeTable_Generator
{
    public class Person    
    {
        public string Name { get; set; }
        public List<DateTime> LeaveDates { get; set; }
        public List<DateTime> AssignedShifts { get; set; }
        public List<DateTime> PreferredDates { get; set; }
        public int WeekdayShifts { get; set; }
        public int WeekendShifts { get; set; }
        public bool ExtraShift {  get; set; } 
        public DateTime? LastAssignedShift { get; set; } // Track last assigned shift
        public int TotalShifts { get; set; }


        public Person(string name)
        {
            Name = name;
            LeaveDates = new List<DateTime>();
            AssignedShifts = new List<DateTime>();
            PreferredDates = new List<DateTime>();
            WeekdayShifts = 0;
            WeekendShifts = 0;
            TotalShifts = 0;
            ExtraShift = false;
        }

        public string LeaveDatesString
        {
            get
            {
                return string.Join(", ", LeaveDates.Select(d => d.ToString("d")));
            }
        }

        public string AssignedShiftsString
        {
            get
            {
                return string.Join(", ", AssignedShifts.Select(d => d.ToString("d")));
            }
        }

        public int TotalLeaveDays
        {
            get
            { return LeaveDates.Count; }

        }
        // ✅ NEW
        public bool PreferWeekendHoliday { get; set; } = false;

        // Optional fine control
        public int WeekendPriorityWeight { get; set; } = 1;
    }
}
