using DocumentFormat.OpenXml.Office2013.Word;
using DocumentFormat.OpenXml.Wordprocessing;
using IMS_Project.Class.Styles;
using MongoDB.Bson.IO;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static TimeTable_Generator.DataTransferClass;

namespace TimeTable_Generator
{
    
    public partial class MainPage : Form
    {
        public List<Person> people;
        public List<DateTime> publicHolidays;
        bool Imported = false;  
        public MainPage()
        {
            InitializeComponent();
            titleBar1.SetParentForm(this);
            TitleBarPanelStyler.ApplyTitleBarPanelStyle(panel_titlebar, this);

            people = new List<Person>();

        }

        private void btn_continue_Click(object sender, EventArgs e)
        {
            DateTime startdate = date_start.Value;
            DateTime enddate = date_end.Value;

            if (Imported)
            {
                var result = MessageBox.Show($"Create timetable from {startdate.ToString("d")} to {enddate.ToString("d")}?", "Confirmation", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);

                if (result == DialogResult.OK)
                {
                    frmAddPerson addPerson = new frmAddPerson(startdate, enddate, people, publicHolidays);
                    addPerson.ShowDialog();
                }

            }
            else
            {
                var result = MessageBox.Show($"Create timetable from {startdate.ToString("d")} to {enddate.ToString("d")}?", "Confirmation", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);

                if (result == DialogResult.OK)
                {
                    frmAddPerson addPerson = new frmAddPerson(startdate, enddate);
                    addPerson.ShowDialog();
                }
            }
            
        }

        private void btn_import_Click(object sender, EventArgs e)
        {
            ExportData importedData = ImportDataFromFile();
            if (importedData != null)
            {
                // Clear the existing list and add the imported people
                people.Clear();
                people.AddRange(importedData.People);

                date_start.Value = importedData.StartDate;
                date_end.Value = importedData.EndDate;
                publicHolidays = importedData.PublicHolidays;
                               

                date_start.Invalidate();
                date_end.Invalidate();
                date_start.Update();
                date_end.Update();

                label_import.Visible = true;
                Imported = true;
                btn_reset.Visible = true;
            }
        }
        private ExportData ImportDataFromFile()
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                openFileDialog.Title = "Select Data File to Import";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string filePath = openFileDialog.FileName;

                    // Read the JSON file
                    string jsonData = File.ReadAllText(filePath);

                    // Deserialize the JSON back into the ExportData object
                    ExportData importedData = Newtonsoft.Json.JsonConvert.DeserializeObject<ExportData>(jsonData);

                    MessageBox.Show("Data imported successfully from " + filePath);
                    return importedData;
                }
            }

            return null;
        }

        private void btn_reset_Click(object sender, EventArgs e)
        {
            date_end.Value = DateTime.Now;
            date_start.Value= DateTime.Now;

            Imported = false;

            label_import.Visible = false;

            btn_reset.Visible = false;
        }

        private void label4_Click(object sender, EventArgs e)
        {

        }

        private void label5_Click(object sender, EventArgs e)
        {

        }
    }
}
