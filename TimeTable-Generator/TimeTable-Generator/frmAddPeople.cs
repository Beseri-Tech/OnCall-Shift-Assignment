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
    public partial class frmAddPeople : Form
    {
        private List<Person> people;
        Person PersonToUpdate;
        public frmAddPeople(List<Person> people)
        {
            InitializeComponent();
            this.people = people;
            titleBar1.SetParentForm(this);
            TitleBarPanelStyler.ApplyTitleBarPanelStyle(panel_titlebar, this);
         
        }

        private void btn_save_Click(object sender, EventArgs e)
        {
            string input = tx_name.Texts;

            if (string.IsNullOrWhiteSpace(input))
            {
                MessageBox.Show("Please enter at least one name.");
                return;
            }

            // Split lines safely
            string[] lines = input
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            int addedCount = 0;

            foreach (string line in lines)
            {
                string name = line.Trim();

                if (!string.IsNullOrEmpty(name))
                {
                    Person newPerson = new Person(name);
                    people.Add(newPerson);
                    addedCount++;
                }
            }

            MessageBox.Show($"{addedCount} person(s) added.");

            this.Close();
        }

    }
}
