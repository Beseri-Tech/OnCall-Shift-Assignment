using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TimeTable_Generator
{
    public class DataTransferClass
    {
        public class ExportData
        {
            public List<Person> People { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public List<DateTime> PublicHolidays { get; set; }
            public List<DateTime> UnassignedDates { get; set; }
        }

        public void ExportDataToFile(List<Person> people, DateTime date1, DateTime date2, List<DateTime> publicHolidays, List<DateTime> unassignedDates)
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                saveFileDialog.DefaultExt = "json";
                saveFileDialog.Title = "Save Exported Data";

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string filePath = saveFileDialog.FileName;

                    // Create an instance of the ExportData class
                    ExportData dataToExport = new ExportData
                    {
                        People = people,
                        StartDate = date1,
                        EndDate = date2,
                        PublicHolidays = publicHolidays,
                        UnassignedDates = unassignedDates
                    };

                    // Serialize the data to JSON
                    string jsonData = JsonConvert.SerializeObject(dataToExport, Formatting.Indented);

                    // Save the JSON data to the selected file
                    File.WriteAllText(filePath, jsonData);

                    MessageBox.Show("Data exported successfully to " + filePath);
                }
            }
        }



    }
}
