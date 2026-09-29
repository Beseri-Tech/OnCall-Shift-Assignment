using DocumentFormat.OpenXml.Drawing.Charts;
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
        public List<DateTime> unassignedShifts;
        private List<string> assignWarnings = new List<string>();
        private string historySourcePath;

        private BackgroundWorker backgroundWorker;

        // Bind through a BindingSource: binding the plain List<Person> while it is empty and adding
        // people later leaves the grid's position at -1 ("Index -1 does not have a value" on click).
        private readonly BindingSource peopleBinding = new BindingSource();
        public frmAddPerson(DateTime startDate, DateTime endDate)
        {
            InitializeComponent();
            titleBar1.SetParentForm(this);
            TitleBarPanelStyler.ApplyTitleBarPanelStyle(panel_titlebar, this);
            TitleBarPanelStyler.EnableMaximize(panel_titlebar, this, new System.Drawing.Size(1280, 800));

            StartDate = startDate;
            EndDate = endDate;
            this.Load += FrmAddPerson_Load;
            people = new List<Person>();
            publicHolidays = new List<DateTime>();
            unassignedShifts = new List<DateTime>();
            dataGridView1.AutoGenerateColumns = false;
            this.Shown += FrmAddPerson_Shown;
        }
        public frmAddPerson(DateTime startDate, DateTime endDate, List<Person> people, List<DateTime> publicHolidays, List<DateTime> unassignedDates)
        {
            InitializeComponent();
            titleBar1.SetParentForm(this);
            TitleBarPanelStyler.ApplyTitleBarPanelStyle(panel_titlebar, this);
            TitleBarPanelStyler.EnableMaximize(panel_titlebar, this, new System.Drawing.Size(1280, 800));

            StartDate = startDate;
            EndDate = endDate;
            this.Load += FrmAddPerson_Load;
            this.people = people;           
            unassignedShifts = unassignedDates ?? new List<DateTime>();
            this.publicHolidays = publicHolidays ?? new List<DateTime>();
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

        private const int EM_SETCUEBANNER = 0x1501;

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private void flow_toolbar_Resize(object sender, EventArgs e)
        {
            // A docked FlowLayoutPanel does not grow when its buttons wrap to a new line;
            // fit its height to the wrapped content so the header shows every row.
            int height = flow_toolbar.GetPreferredSize(new System.Drawing.Size(flow_toolbar.Width, 0)).Height;
            if (flow_toolbar.Height != height)
                flow_toolbar.Height = height;

            // The header's AutoSize doesn't pick up the child's new height, so set it too.
            int headerHeight = panel_header.Padding.Vertical + panel_info.Height + flow_toolbar.Height;
            if (panel_header.Height != headerHeight)
                panel_header.Height = headerHeight;
        }

        private void FrmAddPerson_Load(object sender, EventArgs e)
        {
            // Placeholder text in the search box (.NET Framework TextBox has no PlaceholderText).
            SendMessage(textBox1.Handle, EM_SETCUEBANNER, (IntPtr)1, "Search name…");

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
            dataGridView1.Columns["preferred_date"].DataPropertyName = "AssignPreferredDate";
            dataGridView1.Columns["PriorWeekendShifts"].DataPropertyName = "PriorWeekendShifts";
            dataGridView1.Columns["ExtraShift"].DataPropertyName = "ExtraShift";
            // Edited as text ("3/10-5/10, 12/10") via CellFormatting/CellParsing.
            dataGridView1.Columns["LeaveDatesString"].DataPropertyName = "LeaveDates";
            dataGridView1.Columns["AssignedShiftsString"].DataPropertyName = "AssignedShifts";

            RefreshDGV();
        }

        private void rjButton2_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show("Assign shifts for this rota?",
                "Information", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);

            if (result == DialogResult.OK)
            {
                progressBar1.Visible = true;
                progressBar1.Value = 0; // Reset progress bar
                btn_assign.Enabled = false;
                dataGridView1.EndEdit();
                dataGridView1.Enabled = false; // people are being rewritten on the worker thread
                // Start background worker
                backgroundWorker.RunWorkerAsync();
            }
                
        }

        // Event handler for the BackgroundWorker's DoWork event
        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            ShiftsAlgorithmV2 shiftsAlgorithmV2 = new ShiftsAlgorithmV2();
            unassignedShifts = new List<DateTime>();
            assignWarnings = new List<string>();

            shiftsAlgorithmV2.AssignShifts(people, StartDate, EndDate, publicHolidays, unassignedShifts, assignWarnings, progress =>
            {
                backgroundWorker.ReportProgress(progress); // Report progress to UI
            });
        }

        // Event handler for reporting progress
        private void BackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressBar1.Value = e.ProgressPercentage;
        }

        // Event handler for when the background worker completes
        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // Hide progress bar when work is done
            progressBar1.Visible = false;
            btn_assign.Enabled = true;
            dataGridView1.Enabled = true;

            // Refresh the DataGridView or perform other tasks
            RefreshDGV();
            ShiftCount();

            if (e.Error != null)
            {
                MessageBox.Show($"Shift assignment failed: {e.Error.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (assignWarnings.Count > 0)
            {
                const int maxLines = 25;
                var lines = assignWarnings.Take(maxLines).ToList();
                if (assignWarnings.Count > maxLines)
                    lines.Add($"...and {assignWarnings.Count - maxLines} more.");

                MessageBox.Show(string.Join(Environment.NewLine, lines), "Assignment Summary",
                    MessageBoxButtons.OK, unassignedShifts.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            }
        }

        private void btn_continue_Click(object sender, EventArgs e)
        {
            frmAddPersonDetails addPersonDetails = new frmAddPersonDetails(people);
            addPersonDetails.FormClosed += Refresh_DGV_On_FormClosed;
            addPersonDetails.ShowDialog();
        }

        private void RefreshDGV()
        {
            label_date.Text = $"Rota  {StartDate:d MMM yyyy} – {EndDate:d MMM yyyy}";

            if (dataGridView1.IsCurrentCellInEditMode)
                dataGridView1.EndEdit();

            if (!ReferenceEquals(peopleBinding.DataSource, people))
            {
                peopleBinding.DataSource = people;
                dataGridView1.DataSource = peopleBinding;
            }
            else
            {
                peopleBinding.ResetBindings(false);
            }

            int rowcount = dataGridView1.Rows.Count;
            int holidayscount = 0;
            if (publicHolidays != null)
            {
                holidayscount = publicHolidays.Count;
            }
            

            label_total.Text = $"People  {rowcount}";
            label_holidays.Text = $"Holidays  {holidayscount}";

            List<DateTime> allDates = GetAllDates(StartDate, EndDate);

            int shiftcounts = 0;
            
            
                shiftcounts = allDates.Count;
            

            label_shift_total.Text = $"Shifts  {shiftcounts}";

            int shiftassigned = 0;
            foreach(DataGridViewRow row in dataGridView1.Rows)
            {
                shiftassigned += (Convert.ToInt32(row.Cells["WeekdayShifts"].Value)) + (Convert.ToInt32(row.Cells["WeekendShifts"].Value));
                ColorRow(row);
            }

            label_shift_assign.Text = $"Assigned  {shiftassigned}";
            ShiftCount();
        }

        private void ColorRow(DataGridViewRow row)
        {
            var person = row.DataBoundItem as Person;
            if (person == null) return;

            if (person.AssignPreferredDate)
                row.DefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(224, 231, 255); // preferred dates
            else if (person.PreferWeekendHoliday)
                row.DefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(220, 252, 231); // weekend preference
            else
                row.DefaultCellStyle.BackColor = System.Drawing.Color.Empty;
        }

        // ---------------- IN-PLACE EDITING ----------------

        private string ColumnName(int columnIndex) => dataGridView1.Columns[columnIndex].Name;

        private void dataGridView1_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            string column = ColumnName(e.ColumnIndex);
            if (e.RowIndex < 0 || (column != "LeaveDatesString" && column != "AssignedShiftsString")) return;

            e.Value = LeaveSheetParser.FormatDateList(e.Value as List<DateTime>, StartDate);
            e.FormattingApplied = true;
        }

        private void dataGridView1_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            if (e.RowIndex < 0 || !dataGridView1.IsCurrentCellInEditMode) return;

            var person = dataGridView1.Rows[e.RowIndex].DataBoundItem as Person;
            string text = Convert.ToString(e.FormattedValue).Trim();
            string error = null;

            switch (ColumnName(e.ColumnIndex))
            {
                case "PersonName":
                    if (text.Length == 0)
                        error = "Name cannot be empty.";
                    else if (people.Any(p => p != person && string.Equals(p.Name, text, StringComparison.OrdinalIgnoreCase)))
                        error = $"{text} is already in the list.";
                    break;

                case "PriorWeekendShifts":
                    if (text.Length > 0 && (!int.TryParse(text, out int prior) || prior < 0))
                        error = "Prior weekend shifts must be a whole number (or blank to use the group average).";
                    break;

                case "LeaveDatesString":
                    LeaveSheetParser.ParseDateList(text, StartDate, out error);
                    break;
            }

            if (error != null)
            {
                e.Cancel = true;
                MessageBox.Show(error, "Invalid value", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void dataGridView1_CellParsing(object sender, DataGridViewCellParsingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            string text = Convert.ToString(e.Value).Trim();

            switch (ColumnName(e.ColumnIndex))
            {
                case "PersonName":
                    e.Value = text;
                    e.ParsingApplied = true;
                    break;

                case "PriorWeekendShifts":
                    e.Value = text.Length == 0 ? (int?)null : int.Parse(text);
                    e.ParsingApplied = true;
                    break;

                case "LeaveDatesString":
                    e.Value = LeaveSheetParser.ParseDateList(text, StartDate, out _) ?? new List<DateTime>();
                    e.ParsingApplied = true;
                    break;
            }
        }

        private void dataGridView1_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            // Commit checkbox clicks immediately instead of when the cell loses focus.
            if (dataGridView1.IsCurrentCellDirty && dataGridView1.CurrentCell is DataGridViewCheckBoxCell)
                dataGridView1.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void dataGridView1_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            int rowIndex = e.RowIndex;
            // Refresh derived cells (Total Leave, colours) once the edit has fully finished.
            BeginInvoke((Action)(() =>
            {
                if (rowIndex < 0 || rowIndex >= dataGridView1.Rows.Count) return;
                peopleBinding.ResetItem(rowIndex);
                ColorRow(dataGridView1.Rows[rowIndex]);
                ShiftCount();
            }));
        }

        private void dataGridView1_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
            MessageBox.Show($"Could not save that value: {e.Exception?.Message}", "Invalid value",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            // Editable cells are edited in place; double-click a read-only column (or the row header)
            // to open the full details form.
            if (e.RowIndex < 0 || (e.ColumnIndex >= 0 && !dataGridView1.Columns[e.ColumnIndex].ReadOnly))
                return;

            Person personToUpdate = dataGridView1.Rows[e.RowIndex].DataBoundItem as Person;
            if (personToUpdate == null) return;

            frmAddPersonDetails personDetails = new frmAddPersonDetails(people, personToUpdate);
            int indexToScroll = e.RowIndex;

            personDetails.FormClosed += (s, args) =>
            {
                RefreshDGV();
                if (indexToScroll < dataGridView1.Rows.Count)
                    dataGridView1.FirstDisplayedScrollingRowIndex = indexToScroll;
            };

            personDetails.ShowDialog();
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
                    generateClass.GenerateTimetableExcel(people, filePath, StartDate,EndDate, publicHolidays, unassignedShifts);

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

            dataTransfer.ExportDataToFile(people, StartDate, EndDate, publicHolidays, unassignedShifts);
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

        private void btn_import_leave_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "Excel files (*.xlsx)|*.xlsx";
                dialog.Title = "Select Leave Sheet";
                if (dialog.ShowDialog() != DialogResult.OK) return;

                LeaveSheet sheet;
                try
                {
                    sheet = LeaveSheetParser.Parse(dialog.FileName, StartDate, EndDate);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not read the leave sheet: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                using (var review = new frmImportLeave(sheet, people, StartDate, EndDate, publicHolidays, unassignedShifts))
                {
                    review.ShowDialog();
                }
                RefreshDGV();
            }
        }

        private void btn_import_history_Click(object sender, EventArgs e)
        {
            if (people.Count == 0)
            {
                MessageBox.Show("Add people (or import leave) before importing the weekend history.");
                return;
            }

            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "Excel files (*.xlsx)|*.xlsx";
                dialog.Title = "Select Weekend/Public Holiday History Sheet";
                if (dialog.ShowDialog() != DialogResult.OK) return;

                HistoryImportResult result;
                try
                {
                    result = WeekendHistoryExcel.Import(dialog.FileName, people);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not read the history sheet: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                historySourcePath = dialog.FileName;
                RefreshDGV();

                var lines = new List<string>
                {
                    $"Matched {result.Matches.Count} of {people.Count} people " +
                    $"(average used for the rest: {WeekendHistoryExcel.Average(people):0.#})."
                };

                var fuzzy = result.Matches.Where(m => m.Type == NameMatchType.Fuzzy).ToList();
                if (fuzzy.Count > 0)
                {
                    lines.Add("");
                    lines.Add("Fuzzy name matches (please check):");
                    lines.AddRange(fuzzy.Select(m => $"  {m.RawName}  ->  {m.Person.Name}"));
                }
                if (result.WithoutHistory.Count > 0)
                {
                    lines.Add("");
                    lines.Add("No history count (using average):");
                    lines.AddRange(result.WithoutHistory.Select(n => "  " + n));
                }
                if (result.NotInRota.Count > 0)
                {
                    lines.Add("");
                    lines.Add($"{result.NotInRota.Count} name(s) in the sheet are not in this rota.");
                }

                MessageBox.Show(string.Join(Environment.NewLine, lines), "Weekend History Imported",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void btn_export_history_Click(object sender, EventArgs e)
        {
            if (!people.Any(p => p.AssignedShifts != null && p.AssignedShifts.Count > 0))
            {
                MessageBox.Show("Assign shifts first - the updated history adds this rota's weekend/holiday shifts.");
                return;
            }

            string source = historySourcePath;
            if (string.IsNullOrEmpty(source) || !System.IO.File.Exists(source))
            {
                using (var open = new OpenFileDialog())
                {
                    open.Filter = "Excel files (*.xlsx)|*.xlsx";
                    open.Title = "Select the Current Weekend History Sheet";
                    if (open.ShowDialog() != DialogResult.OK) return;
                    source = open.FileName;
                }
            }

            using (var save = new SaveFileDialog())
            {
                save.Filter = "Excel files (*.xlsx)|*.xlsx";
                save.Title = "Save Updated Weekend History";
                save.FileName = $"Oncaller Total Weekend And Public Shift {EndDate:yyyy-MM}.xlsx";
                if (save.ShowDialog() != DialogResult.OK) return;

                if (string.Equals(System.IO.Path.GetFullPath(save.FileName), System.IO.Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Please save to a new file so the original history is kept.");
                    return;
                }

                try
                {
                    WeekendHistoryExcel.ExportUpdated(source, save.FileName, people, EndDate);
                    MessageBox.Show("Updated weekend history saved to " + save.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not save the history: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
