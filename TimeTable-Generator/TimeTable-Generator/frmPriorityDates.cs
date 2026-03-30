using CustomControls.RJControls;
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

namespace TimeTable_Generator
{
    public partial class frmPriorityDates : Form
    {
        private List<DateTime> priorityDates;
        public frmPriorityDates(List<DateTime> priorityDates)
        {
            InitializeComponent();
            this.priorityDates = priorityDates ?? new List<DateTime>();
            this.Load += FrmPriorityDates_Load;
            titleBar1.SetParentForm(this);
            TitleBarPanelStyler.ApplyTitleBarPanelStyle(panel_titlebar, this);

        }

        private void FrmPriorityDates_Load(object sender, EventArgs e)
        {
            list_prioritydates.Items.Clear();

            if(priorityDates != null)
            {
                foreach (DateTime date in priorityDates)
                {
                    list_prioritydates.Items.Add(date.Date);
                }
            }
            
           
        }

        private void btn_close_Click(object sender, EventArgs e)
        {
            if(priorityDates != null)
            {
                priorityDates.Clear();
            }            

            foreach (var item in list_prioritydates.Items)
            {
                DateTime preferred = DateTime.Parse(item.ToString());
                priorityDates.Add(preferred.Date);
            }
            this.Close();
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

        private void btn_add_Click(object sender, EventArgs e)
        {
            if(!string.IsNullOrEmpty(tx_date.Text))
            {
                list_prioritydates.Items.Add(tx_date.Text);
                FocusAndSelectTextBeforeFirstSlash(tx_date);
            }
            
        }

        private void rjButton1_Click(object sender, EventArgs e)
        {
            if (list_prioritydates.SelectedItem != null)
            {
                // Remove the selected item
                list_prioritydates.Items.Remove(list_prioritydates.SelectedItem);
            }
            else
            {
                MessageBox.Show("Please select a leave date to delete.");
            }
        }
    }
}
