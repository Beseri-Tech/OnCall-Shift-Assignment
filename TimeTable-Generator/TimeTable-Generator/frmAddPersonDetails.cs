using IMS_Project.Class.Styles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace TimeTable_Generator
{
    public partial class frmAddPersonDetails : Form
    {
        private List<Person> people;
        Person PersonToUpdate;
        public frmAddPersonDetails(List<Person> people)
        {
            InitializeComponent();
            this.people = people;
            titleBar1.SetParentForm(this);
            TitleBarPanelStyler.ApplyTitleBarPanelStyle(panel_titlebar, this);
            btn_save.Click += btn_save_Click;
            this.KeyPreview = true;
            this.KeyUp += FrmAddPersonDetails_KeyUp;
        }

        private void FrmAddPersonDetails_KeyUp(object sender, KeyEventArgs e)
        {
            if(e.KeyCode == Keys.Delete)
            {
                if (list_leavedates.SelectedItem != null)
                {
                    list_leavedates.Items.Remove(list_leavedates.SelectedItem);
                }
                else if (list_preferred.SelectedItem != null)
                {
                    list_preferred.Items.Remove(list_preferred.SelectedItem);
                }   
            }
        }

        public frmAddPersonDetails(List<Person> people,Person personToUpdate)
        {
            InitializeComponent();
            this.people = people;
            this.PersonToUpdate = personToUpdate;
            this.Load += FrmAddPersonDetails_Load;
            btn_save.Click += Btn_save_Click_update;
            titleBar1.SetParentForm(this);
            TitleBarPanelStyler.ApplyTitleBarPanelStyle(panel_titlebar, this);
            this.KeyPreview = true;
            this.KeyUp += FrmAddPersonDetails_KeyUp;

        }

        private void Btn_save_Click_update(object sender, EventArgs e)
        {
            PersonToUpdate.LeaveDates.Clear();
            PersonToUpdate.PreferredDates.Clear();
            PersonToUpdate.Name = tx_name.Texts;
            // Loop through the items in the ListBox and update the LeaveDates list
            foreach (var item in list_leavedates.Items)
            {
                DateTime leaveDate = DateTime.Parse(item.ToString());
                PersonToUpdate.LeaveDates.Add(leaveDate.Date);
            }
            foreach (var item in list_preferred.Items)
            {
                DateTime preferredDate = DateTime.Parse(item.ToString());
                PersonToUpdate.PreferredDates.Add(preferredDate.Date);
            }

            PersonToUpdate.ExtraShift = checkBox1.Checked;
            this.Close();
        }

        private void FrmAddPersonDetails_Load(object sender, EventArgs e)
        {
            tx_name.Texts = PersonToUpdate.Name;

            list_leavedates.Items.Clear();
            list_preferred.Items.Clear();
            foreach (var leaveDate in PersonToUpdate.LeaveDates)
            {
                list_leavedates.Items.Add(leaveDate.Date);
            }
            foreach (var preferredDate in PersonToUpdate.PreferredDates)
            {
                list_preferred.Items.Add(preferredDate.Date);
            }

            checkBox1.Checked = PersonToUpdate.ExtraShift;
        }

      
        private void rjButton1_Click(object sender, EventArgs e)
        {
            if (list_leavedates.SelectedItem != null)
            {                
                list_leavedates.Items.Remove(list_leavedates.SelectedItem);
            }
            else if(list_preferred.SelectedItem != null)
            {
                list_preferred.Items.Remove(list_preferred.SelectedItem);
            }
            else
            {
                MessageBox.Show("Please select a date in either list to delete.");
            }
        }

        private void btn_save_Click(object sender, EventArgs e)
        {
            string name = tx_name.Texts;

            if (!string.IsNullOrEmpty(name))
            {
                // Create a new Person object
                Person newPerson = new Person(name);

                // Loop through the ListBox to add all leave dates

                foreach (var item in list_leavedates.Items)
                {
                    DateTime leaveDate = DateTime.Parse(item.ToString());
                    newPerson.LeaveDates.Add(leaveDate);
                }        
                foreach (var item in list_preferred.Items)
                {
                    DateTime preferredDates = DateTime.Parse(item.ToString());
                    newPerson.PreferredDates.Add(preferredDates);
                }

                newPerson.ExtraShift = checkBox1.Checked;

                // Add the new person to the people list
                people.Add(newPerson);

                // Close the details form
                this.Close();
            }
            else
            {
                MessageBox.Show("Please enter a name.");
            }
        }

        private void btn_add_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(tx_leavedate.Text))
            {
                string input = tx_leavedate.Text.Trim();

                // Check if input contains a range indicator, like "to"
                if (input.Contains("to"))
                {
                    string[] parts = input.Split(new string[] { "to" }, StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length == 2 &&
                        DateTime.TryParse(parts[0].Trim(), out DateTime startDate) &&
                        DateTime.TryParse(parts[1].Trim(), out DateTime endDate))
                    {
                        if (startDate <= endDate)
                        {
                            for (DateTime date = startDate; date <= endDate; date = date.AddDays(1))
                            {
                                list_leavedates.Items.Add(date.ToString("yyyy-MM-dd")); // Customize the format if needed
                            }
                        }
                        else
                        {
                            MessageBox.Show("The start date must be earlier than or equal to the end date.", "Invalid Range", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Please enter a valid date range (e.g., 2024-11-01 to 2024-11-05).", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                else
                {
                    // Add a single date
                    if (DateTime.TryParse(input, out DateTime singleDate))
                    {
                        list_leavedates.Items.Add(singleDate.ToString("yyyy-MM-dd")); // Customize the format if needed
                    }
                    else
                    {
                        MessageBox.Show("Please enter a valid date (e.g., 2024-11-01) or a range (e.g., 2024-11-01 to 2024-11-05).", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }

                // Refocus and reset the textbox
                FocusAndSelectTextBeforeFirstSlash(tx_leavedate);
            }
        }

        private void FocusAndSelectTextBeforeFirstSlash(TextBox textBox)
        {
            // Focus the TextBox
            textBox.Focus();

            // Find the index of the first '/'
            int slashIndex = textBox.Text.IndexOf('/');

            // If a slash is found, select the text before the first one
            if (slashIndex > 0)
            {
                textBox.Select(0, slashIndex);
            }
            else
            {
                // Optionally handle the case where no slash is found
                textBox.SelectAll();
            }
        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void rjButton2_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(tx_preferred.Text))
            {
                list_preferred.Items.Add(tx_preferred.Text);
                FocusAndSelectTextBeforeFirstSlash(tx_preferred);
            }
        }

        private void btn_clear_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show("Clear all dates?", "Confirmation", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (result == DialogResult.OK) {

                if (list_leavedates.Items.Count > 0)
                {
                    list_leavedates.Items.Clear();
                }
                if (list_preferred.Items.Count > 0)
                {
                    list_preferred.Items.Clear();
                }
            }
        }

        private void tx_leavedate_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter) 
            { 
                btn_add_Click(btn_add, null);
            }
        }

        private void buttonAddRange_Click(object sender, EventArgs e)
        {
            DateTime start = monthCalendar1.SelectionStart;
            DateTime end = monthCalendar1.SelectionEnd;

            // Ensure start <= end (just in case)
            if (start > end)
            {
                var temp = start;
                start = end;
                end = temp;
            }

            for (DateTime d = start; d <= end; d = d.AddDays(1))
            {
                string formatted = d.ToString("dd/MM/yy"); // your 15/12/25 style

                // avoid duplicates in ListBox
                if (!list_leavedates.Items.Contains(formatted))
                {
                    list_leavedates.Items.Add(formatted);
                }
            }
        }
    }   
}
