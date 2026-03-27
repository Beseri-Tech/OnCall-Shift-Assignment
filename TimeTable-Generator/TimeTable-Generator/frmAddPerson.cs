using DocumentFormat.OpenXml.Wordprocessing;
using IMS_Project.Class.Styles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace TimeTable_Generator
{
    public partial class frmAddPerson : Form
    {
        public DateTime StartDate;
        public DateTime EndDate;

        public List<Person> people;

        public List<DateTime> publicHolidays;

        private BackgroundWorker backgroundWorker;
        public frmAddPerson(DateTime startDate, DateTime endDate)
        {
            InitializeComponent();
            titleBar1.SetParentForm(this);
            TitleBarPanelStyler.ApplyTitleBarPanelStyle(panel_titlebar, this);

            StartDate = startDate;
            EndDate = endDate;
            this.Load += FrmAddPerson_Load;
            people = new List<Person>();
            publicHolidays = new List<DateTime>();
            dataGridView1.AutoGenerateColumns = false;
            this.Shown += FrmAddPerson_Shown;
        }
        public frmAddPerson(DateTime startDate, DateTime endDate, List<Person> people, List<DateTime> publicHolidays)
        {
            InitializeComponent();
            titleBar1.SetParentForm(this);
            TitleBarPanelStyler.ApplyTitleBarPanelStyle(panel_titlebar, this);

            StartDate = startDate;
            EndDate = endDate;
            this.Load += FrmAddPerson_Load;
            this.people = people;
            if(publicHolidays == null)
            {
                publicHolidays= new List<DateTime>();
            }
            this.publicHolidays = publicHolidays;
            dataGridView1.AutoGenerateColumns = false;
            this.Shown += FrmAddPerson_Shown;
        }

        private void FrmAddPerson_Shown(object sender, EventArgs e)
        {
            RefreshDGV();
        }

        private void ClearDates()
        {
            foreach (Person person in this.people)
            {
                person.LeaveDates.Clear();
                person.PreferredDates.Clear();
            }

            if (publicHolidays != null)
            {
                publicHolidays.Clear();
            }
            RefreshDGV();
        }

        private void FrmAddPerson_Load(object sender, EventArgs e)
        {
            // Initialize BackgroundWorker
            backgroundWorker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = false
            };

            // Assign event handlers for background worker
            backgroundWorker.DoWork += BackgroundWorker_DoWork;
            backgroundWorker.ProgressChanged += BackgroundWorker_ProgressChanged;
            backgroundWorker.RunWorkerCompleted += BackgroundWorker_RunWorkerCompleted;

            dataGridView1.Columns["PersonName"].DataPropertyName = "Name";
            dataGridView1.Columns["AssignedShiftsString"].DataPropertyName = "AssignedShiftsString";
            dataGridView1.Columns["LeaveDatesString"].DataPropertyName = "LeaveDatesString";
            dataGridView1.Columns["WeekendShifts"].DataPropertyName = "WeekendShifts";
            dataGridView1.Columns["WeekdayShifts"].DataPropertyName = "WeekdayShifts";
            dataGridView1.Columns["TotalLeaveDays"].DataPropertyName = "TotalLeaveDays";
            dataGridView1.Columns["weekend"].DataPropertyName = "PreferWeekendHoliday";

            RefreshDGV();
        }

        private void rjButton2_Click(object sender, EventArgs e)
        {
            string algorithmVersion = algo_version_toggle.Checked ? $"Algorithm V2?\n(Weekend and Holidays priority)" : "Algorithm V1?";

            var result = MessageBox.Show($"Assign Shifts using {algorithmVersion}",
                "Information", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);

            if (result == DialogResult.OK)
            {
                progressBar1.Visible = true;
                progressBar1.Value = 0; // Reset progress bar
                btn_assign.Enabled = false;
                // Start background worker
                backgroundWorker.RunWorkerAsync();
            }
                
        }

        // Event handler for the BackgroundWorker's DoWork event
        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            ShiftsAlgorithm shiftsAlgorithm = new ShiftsAlgorithm();
            ShiftsAlgorithmV2 shiftsAlgorithmV2 = new ShiftsAlgorithmV2();

            if (!algo_version_toggle.Checked)
            {
               
                shiftsAlgorithm.AssignShifts(people, StartDate, EndDate, publicHolidays, progress =>
                {
                    backgroundWorker.ReportProgress(progress); // Report progress to UI
                });
            }
            else
            {

                shiftsAlgorithmV2.AssignShifts(people, StartDate, EndDate, publicHolidays, progress =>
                {
                    backgroundWorker.ReportProgress(progress); // Report progress to UI
                });
            }

               
            
        }

        // Event handler for reporting progress
        private void BackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressBar1.Value = e.ProgressPercentage;
            RefreshDGV();
        }

        // Event handler for when the background worker completes
        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // Hide progress bar when work is done
            progressBar1.Visible = false;
            btn_assign.Enabled = true;

            // Refresh the DataGridView or perform other tasks
            RefreshDGV();
            ShiftCount();
        }

        private void btn_continue_Click(object sender, EventArgs e)
        {
            frmAddPersonDetails addPersonDetails = new frmAddPersonDetails(people);
            addPersonDetails.FormClosed += Refresh_DGV_On_FormClosed;
            addPersonDetails.ShowDialog();
        }

        private void RefreshDGV()
        {
            label_date.Text = $"Creating Timetable From {StartDate.ToString("d")} to {EndDate.ToString("d")}";

            dataGridView1.DataSource = null;
            dataGridView1.DataSource = people;
            int rowcount = dataGridView1.Rows.Count;
            int holidayscount = 0;
            if (publicHolidays != null)
            {
                holidayscount = publicHolidays.Count;
            }
            

            label_total.Text = $"Total No of Person : {rowcount.ToString()}";
            label_holidays.Text = $"Total No of Holidays : {holidayscount.ToString()}";

            List<DateTime> allDates = GetAllDates(StartDate, EndDate);

            int shiftcounts = 0;
            
            
                shiftcounts = allDates.Count;
            

            label_shift_total.Text = $"Total Shifts : {shiftcounts.ToString()}";

            int shiftassigned = 0;
            foreach(DataGridViewRow row in dataGridView1.Rows)
            {
                shiftassigned += (Convert.ToInt32(row.Cells["WeekdayShifts"].Value)) + (Convert.ToInt32(row.Cells["WeekendShifts"].Value));
                if (Convert.ToBoolean(row.Cells["weekend"].Value) == true)
                {
                    row.DefaultCellStyle.BackColor = System.Drawing.Color.YellowGreen;
                }
            }

            label_shift_assign.Text = $"Total Shifts Assigned: {shiftassigned.ToString()}";
            ShiftCount();
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
        private void Refresh_DGV_On_FormClosed(object sender, FormClosedEventArgs e)
        {
            RefreshDGV();            
        }

        private void rjButton1_Click(object sender, EventArgs e)
        {


//single cell
            if (dataGridView1.SelectedCells.Count > 0)
            {
                List<DataGridViewCell> selectedCells = dataGridView1.SelectedCells.Cast<DataGridViewCell>().ToList();
                List<string> namesToRemove = new List<string>();

                foreach (DataGridViewCell cell in selectedCells)
                {
                    if (cell != null)
                    {
                        int rowIndex = cell.RowIndex;

                        DataGridViewRow row = dataGridView1.Rows[rowIndex];

                        string name = row.Cells["PersonName"].Value.ToString();

                        Person personToRemove = people.FirstOrDefault(p => p.Name == name);

                        if (personToRemove != null)
                        {
                            // Remove the person from the list
                            people.Remove(personToRemove);
                            namesToRemove.Add(name);                            
                        }

                        else
                        {
                            MessageBox.Show($"{name} not found in the list.");
                        }
                    }
                                                 
                }
                string names = string.Join(", ", namesToRemove);
                MessageBox.Show($"{names} has been removed from the list.");
            }
            else
            {
                MessageBox.Show($"Select a row in the list to delete.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            RefreshDGV();
        }

        private void dataGridView1_CellMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (dataGridView1.SelectedCells.Count > 0)
            {
                DataGridViewCell cell = dataGridView1.SelectedCells[0];
                if (cell != null)
                {
                    int rowIndex = cell.RowIndex;

                    DataGridViewRow row = dataGridView1.Rows[rowIndex];

                    string name = row.Cells["PersonName"].Value.ToString();

                    Person personToUpdate = people.FirstOrDefault(p => p.Name == name);

                    if (personToUpdate != null)
                    {
                        frmAddPersonDetails personDetails = new frmAddPersonDetails(people,personToUpdate);
                        personDetails.FormClosed += Refresh_DGV_On_FormClosed;
                        personDetails.ShowDialog();
                    }

                }
            }
        }


        private void btn_download_Click(object sender, EventArgs e)
        {
           GenerateClass generateClass = new GenerateClass();

            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "Excel Files|*.xlsx";
                saveFileDialog.Title = "Save the Timetable";
                saveFileDialog.FileName = "timetable_shifts.xlsx";  // Default filename

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    // Get the chosen file path
                    string filePath = saveFileDialog.FileName;

                    // Generate the Excel file
                    generateClass.GenerateTimetableExcel(people, filePath, StartDate,EndDate, publicHolidays);

                    MessageBox.Show("Timetable has been exported to Excel!");
                }
            }
        }

        private void btn_reset_Click(object sender, EventArgs e)
        {
            foreach (Person person in people)
            {
                person.AssignedShifts.Clear();
                person.WeekendShifts = 0;
                person.WeekdayShifts = 0;
                person.LastAssignedShift = null;
            }
            
            RefreshDGV();
        }

        private void rjButton2_Click_1(object sender, EventArgs e)
        {
            DataTransferClass dataTransfer = new DataTransferClass();

            dataTransfer.ExportDataToFile(people, StartDate, EndDate, publicHolidays);
        }

        private void btn_holidays_Click(object sender, EventArgs e)
        {
            frmPublicHolidays holidays = new frmPublicHolidays(publicHolidays);
            holidays.FormClosed += Refresh_DGV_On_FormClosed;
            holidays.ShowDialog();
                
        }

        private void dataGridView1_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            
        }

        private void btn_date_Click(object sender, EventArgs e)
        {
            frmChangeDates changeDates = new frmChangeDates(StartDate, EndDate);
            changeDates.FormClosed += (s, args) =>
            {
                StartDate = changeDates.StartDate;
                EndDate = changeDates.EndDate;
                Refresh_DGV_On_FormClosed(s, args); // Call your refresh method here
            };
            changeDates.ShowDialog();
        }

        private void dataGridView1_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (dataGridView1.Rows.Count > 0)
            {
                btn_clear.Visible = true;
            }
            else { btn_clear.Visible = false; }
        }

        private void btn_clear_Click(object sender, EventArgs e)
        {
            ClearDates();
        }

        private void dataGridView1_Paint(object sender, PaintEventArgs e)
        {
            if (dataGridView1.Rows.Count > 0)
            {
                btn_clear.Visible = true;
            }
            else { btn_clear.Visible = false; }
        }

        private void label_shift_total_Click(object sender, EventArgs e)
        {

        }

        private void chk_double_CheckedChanged(object sender, EventArgs e)
        {
            RefreshDGV();
        }

        private void ShiftCount()
        {
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                var count = 0;
               

                var weekdays = Convert.ToInt32(row.Cells["WeekdayShifts"].Value);
                var weekends = Convert.ToInt32(row.Cells["WeekendShifts"].Value);

                count = weekdays + weekends;

                row.Cells["TotalShift"].Value = count.ToString();
            }
        }

        private void btn_batch_Click(object sender, EventArgs e)
        {
            frmAddPeople addPeople = new frmAddPeople(people);
            addPeople.FormClosed += Refresh_DGV_On_FormClosed;
            addPeople.ShowDialog();
        }

   

        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                dataGridView1.ClearSelection();

                foreach (DataGridViewRow row in dataGridView1.Rows)
                {
                    if (!row.IsNewRow && row.Cells["PersonName"].Value != null)
                    {
                        string cellValue = row.Cells["PersonName"].Value.ToString();

                        if (cellValue.ToLower().Contains(textBox1.Text.ToLower()))
                        {
                            row.Cells["PersonName"].Selected = true;
                        }
                    }
                }
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            dataGridView1.ClearSelection();
            if(!string.IsNullOrEmpty(textBox1.Text))
            {
                foreach (DataGridViewRow row in dataGridView1.Rows)
                {
                    if (!row.IsNewRow && row.Cells["PersonName"].Value != null)
                    {
                        string cellValue = row.Cells["PersonName"].Value.ToString();

                        if (cellValue.ToLower().Contains(textBox1.Text.ToLower()))
                        {
                            row.Cells["PersonName"].Selected = true;
                            dataGridView1.FirstDisplayedScrollingRowIndex = row.Index;
                        }
                    }
                }
            }
            else
            {
                dataGridView1.ClearSelection();
            }
           
        }
    }
}
