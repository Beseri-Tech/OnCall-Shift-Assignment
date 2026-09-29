using IMS_Project.Class.Styles;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace TimeTable_Generator
{
    /// <summary>
    /// Review screen for a parsed leave sheet: confirm/fix name matches, see parsed dates and warnings,
    /// then apply the leave to the rota (replacing leave inside the sheet's months).
    /// </summary>
    public partial class frmImportLeave : Form
    {
        private const string ADD_NEW = "(Add as new person)";
        private const string SKIP = "(Skip)";

        private readonly List<Person> people;
        private readonly LeaveSheet sheet;
        private readonly DateTime startDate;
        private readonly DateTime endDate;
        private readonly List<DateTime> publicHolidays;
        private readonly List<DateTime> unassignedShifts;

        public frmImportLeave(LeaveSheet sheet, List<Person> people, DateTime startDate, DateTime endDate,
            List<DateTime> publicHolidays, List<DateTime> unassignedShifts)
        {
            InitializeComponent();
            titleBar1.SetParentForm(this);
            TitleBarPanelStyler.ApplyTitleBarPanelStyle(panel_titlebar, this);
            TitleBarPanelStyler.EnableMaximize(panel_titlebar, this, new Size(1100, 720));

            this.sheet = sheet;
            this.people = people;
            this.startDate = startDate;
            this.endDate = endDate;
            this.publicHolidays = publicHolidays;
            this.unassignedShifts = unassignedShifts;

            this.Load += FrmImportLeave_Load;
        }

        private void FrmImportLeave_Load(object sender, EventArgs e)
        {
            col_person.Items.Add(ADD_NEW);
            col_person.Items.Add(SKIP);
            foreach (var name in people.Select(p => p.Name).Distinct().OrderBy(n => n))
                col_person.Items.Add(name);

            var matches = NameMatcher.MatchAll(sheet.Rows.Select(r => r.RawName).ToList(), people);

            for (int i = 0; i < sheet.Rows.Count; i++)
            {
                var row = sheet.Rows[i];
                var match = matches[i];

                var messages = row.Warnings.Concat(row.Notes.Select(n => "note - " + n));
                int index = dataGridView1.Rows.Add(
                    row.RawName,
                    match.Person != null ? match.Person.Name : ADD_NEW,
                    "",
                    row.Dates.Count,
                    LeaveSheetParser.Compact(row.Dates),
                    string.Join("; ", messages));

                dataGridView1.Rows[index].Tag = row;
                UpdateMatchCell(dataGridView1.Rows[index], match.Type);
            }

            string months = string.Join(", ", sheet.Months.Select(m => m.ToString("MMM yyyy")));
            int warned = sheet.Rows.Count(r => r.Warnings.Count > 0);
            label_summary.Text = $"{sheet.Rows.Count} names, months: {months}. {warned} row(s) have warnings. " +
                                 "Yellow = fuzzy name match (please check), green = will be added as a new person. " +
                                 "Change 'Rota Person' to fix a match. Leave inside these months is replaced.";
        }

        private void UpdateMatchCell(DataGridViewRow gridRow, NameMatchType? type = null)
        {
            string choice = Convert.ToString(gridRow.Cells[col_person.Index].Value);

            string label;
            Color color;
            if (choice == ADD_NEW) { label = "New"; color = Color.PaleGreen; }
            else if (choice == SKIP) { label = "Skip"; color = Color.LightGray; }
            else if (type == NameMatchType.Fuzzy) { label = "Fuzzy"; color = Color.Khaki; }
            else if (type == NameMatchType.Exact) { label = "Exact"; color = Color.White; }
            else { label = "Manual"; color = Color.White; }

            gridRow.Cells[col_match.Index].Value = label;
            gridRow.DefaultCellStyle.BackColor = color;
        }

        private void dataGridView1_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dataGridView1.IsCurrentCellDirty)
                dataGridView1.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void dataGridView1_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == col_person.Index)
                UpdateMatchCell(dataGridView1.Rows[e.RowIndex]);
        }

        private bool Apply()
        {
            var chosen = dataGridView1.Rows.Cast<DataGridViewRow>()
                .Select(r => Convert.ToString(r.Cells[col_person.Index].Value))
                .Where(c => c != ADD_NEW && c != SKIP)
                .GroupBy(c => c)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (chosen.Count > 0)
            {
                var answer = MessageBox.Show(
                    "These people are chosen for more than one sheet row; their leave will be combined:\n\n" +
                    string.Join("\n", chosen) + "\n\nContinue?",
                    "Duplicate matches", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                if (answer != DialogResult.OK) return false;
            }

            var cleared = new HashSet<Person>();
            int updated = 0, added = 0;

            foreach (DataGridViewRow gridRow in dataGridView1.Rows)
            {
                var row = (LeaveRow)gridRow.Tag;
                string choice = Convert.ToString(gridRow.Cells[col_person.Index].Value);
                if (choice == SKIP) continue;

                Person person;
                if (choice == ADD_NEW)
                {
                    person = new Person(row.RawName.Trim());
                    people.Add(person);
                    added++;
                }
                else
                {
                    person = people.First(p => p.Name == choice);
                    updated++;
                }

                if (person.LeaveDates == null) person.LeaveDates = new List<DateTime>();

                if (cleared.Add(person))
                    person.LeaveDates.RemoveAll(InImportedMonths);

                var merged = person.LeaveDates.Concat(row.Dates).Select(d => d.Date).Distinct().OrderBy(d => d).ToList();
                person.LeaveDates.Clear();
                person.LeaveDates.AddRange(merged);
            }

            MessageBox.Show($"Leave applied: {updated} existing people updated, {added} new people added.",
                "Import Leave", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }

        private bool InImportedMonths(DateTime d)
        {
            return sheet.Months.Any(m => m.Year == d.Year && m.Month == d.Month);
        }

        private void btn_apply_Click(object sender, EventArgs e)
        {
            if (!Apply()) return;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btn_apply_save_Click(object sender, EventArgs e)
        {
            if (!Apply()) return;
            new DataTransferClass().ExportDataToFile(people, startDate, endDate, publicHolidays, unassignedShifts);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btn_cancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
