namespace TimeTable_Generator
{
    partial class frmImportLeave
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.panel1 = new System.Windows.Forms.Panel();
            this.label_summary = new System.Windows.Forms.Label();
            this.btn_cancel = new CustomControls.RJControls.RJButton();
            this.btn_apply_save = new CustomControls.RJControls.RJButton();
            this.btn_apply = new CustomControls.RJControls.RJButton();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.col_sheet_name = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.col_person = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.col_match = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.col_days = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.col_dates = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.col_warnings = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.panel_titlebar = new System.Windows.Forms.Panel();
            this.label4 = new System.Windows.Forms.Label();
            this.titleBar1 = new Button_Control.TitleBar();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.panel_titlebar.SuspendLayout();
            this.SuspendLayout();
            //
            // panel1
            //
            this.panel1.BackColor = System.Drawing.Color.AntiqueWhite;
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel1.Controls.Add(this.label_summary);
            this.panel1.Controls.Add(this.btn_cancel);
            this.panel1.Controls.Add(this.btn_apply_save);
            this.panel1.Controls.Add(this.btn_apply);
            this.panel1.Controls.Add(this.dataGridView1);
            this.panel1.Controls.Add(this.panel_titlebar);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(1000, 620);
            this.panel1.TabIndex = 0;
            //
            // label_summary
            //
            this.label_summary.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F);
            this.label_summary.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label_summary.Location = new System.Drawing.Point(11, 38);
            this.label_summary.Name = "label_summary";
            this.label_summary.Size = new System.Drawing.Size(976, 40);
            this.label_summary.TabIndex = 1;
            this.label_summary.Text = "Review the matches below. Yellow = fuzzy name match, green = new person. Change the Rota Person column to fix a match.";
            //
            // btn_cancel
            //
            this.btn_cancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btn_cancel.BackColor = System.Drawing.Color.LightPink;
            this.btn_cancel.BackgroundColor = System.Drawing.Color.LightPink;
            this.btn_cancel.BorderColor = System.Drawing.Color.Black;
            this.btn_cancel.BorderRadius = 0;
            this.btn_cancel.BorderSize = 1;
            this.btn_cancel.FlatAppearance.BorderSize = 0;
            this.btn_cancel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_cancel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_cancel.ForeColor = System.Drawing.Color.Black;
            this.btn_cancel.Location = new System.Drawing.Point(837, 575);
            this.btn_cancel.Name = "btn_cancel";
            this.btn_cancel.Size = new System.Drawing.Size(150, 32);
            this.btn_cancel.TabIndex = 5;
            this.btn_cancel.Text = "Cancel";
            this.btn_cancel.TextColor = System.Drawing.Color.Black;
            this.btn_cancel.UseVisualStyleBackColor = false;
            this.btn_cancel.Click += new System.EventHandler(this.btn_cancel_Click);
            //
            // btn_apply_save
            //
            this.btn_apply_save.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btn_apply_save.BackColor = System.Drawing.Color.LightBlue;
            this.btn_apply_save.BackgroundColor = System.Drawing.Color.LightBlue;
            this.btn_apply_save.BorderColor = System.Drawing.Color.Black;
            this.btn_apply_save.BorderRadius = 0;
            this.btn_apply_save.BorderSize = 1;
            this.btn_apply_save.FlatAppearance.BorderSize = 0;
            this.btn_apply_save.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_apply_save.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_apply_save.ForeColor = System.Drawing.Color.Black;
            this.btn_apply_save.Location = new System.Drawing.Point(661, 575);
            this.btn_apply_save.Name = "btn_apply_save";
            this.btn_apply_save.Size = new System.Drawing.Size(170, 32);
            this.btn_apply_save.TabIndex = 4;
            this.btn_apply_save.Text = "Apply && Save JSON";
            this.btn_apply_save.TextColor = System.Drawing.Color.Black;
            this.btn_apply_save.UseVisualStyleBackColor = false;
            this.btn_apply_save.Click += new System.EventHandler(this.btn_apply_save_Click);
            //
            // btn_apply
            //
            this.btn_apply.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btn_apply.BackColor = System.Drawing.Color.PaleGreen;
            this.btn_apply.BackgroundColor = System.Drawing.Color.PaleGreen;
            this.btn_apply.BorderColor = System.Drawing.Color.Black;
            this.btn_apply.BorderRadius = 0;
            this.btn_apply.BorderSize = 1;
            this.btn_apply.FlatAppearance.BorderSize = 0;
            this.btn_apply.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_apply.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_apply.ForeColor = System.Drawing.Color.Black;
            this.btn_apply.Location = new System.Drawing.Point(505, 575);
            this.btn_apply.Name = "btn_apply";
            this.btn_apply.Size = new System.Drawing.Size(150, 32);
            this.btn_apply.TabIndex = 3;
            this.btn_apply.Text = "Apply";
            this.btn_apply.TextColor = System.Drawing.Color.Black;
            this.btn_apply.UseVisualStyleBackColor = false;
            this.btn_apply.Click += new System.EventHandler(this.btn_apply_Click);
            //
            // dataGridView1
            //
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dataGridView1.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.col_sheet_name,
            this.col_person,
            this.col_match,
            this.col_days,
            this.col_dates,
            this.col_warnings});
            this.dataGridView1.Location = new System.Drawing.Point(11, 81);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.RowHeadersVisible = false;
            this.dataGridView1.Size = new System.Drawing.Size(976, 486);
            this.dataGridView1.TabIndex = 2;
            this.dataGridView1.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView1_CellValueChanged);
            this.dataGridView1.CurrentCellDirtyStateChanged += new System.EventHandler(this.dataGridView1_CurrentCellDirtyStateChanged);
            //
            // col_sheet_name
            //
            this.col_sheet_name.HeaderText = "Name in Sheet";
            this.col_sheet_name.Name = "col_sheet_name";
            this.col_sheet_name.ReadOnly = true;
            this.col_sheet_name.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.col_sheet_name.DefaultCellStyle.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.col_sheet_name.FillWeight = 20F;
            this.col_sheet_name.MinimumWidth = 160;
            //
            // col_person
            //
            this.col_person.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.col_person.HeaderText = "Rota Person";
            this.col_person.Name = "col_person";
            this.col_person.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.col_person.FillWeight = 22F;
            this.col_person.MinimumWidth = 180;
            //
            // col_match
            //
            this.col_match.HeaderText = "Match";
            this.col_match.Name = "col_match";
            this.col_match.ReadOnly = true;
            this.col_match.Width = 60;
            //
            // col_days
            //
            this.col_days.HeaderText = "Days";
            this.col_days.Name = "col_days";
            this.col_days.ReadOnly = true;
            this.col_days.Width = 45;
            //
            // col_dates
            //
            this.col_dates.HeaderText = "Leave Dates";
            this.col_dates.Name = "col_dates";
            this.col_dates.ReadOnly = true;
            this.col_dates.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.col_dates.DefaultCellStyle.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.col_dates.FillWeight = 30F;
            this.col_dates.MinimumWidth = 180;
            //
            // col_warnings
            //
            this.col_warnings.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.col_warnings.DefaultCellStyle.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.col_warnings.FillWeight = 28F;
            this.col_warnings.MinimumWidth = 180;
            this.col_warnings.HeaderText = "Warnings / Notes";
            this.col_warnings.Name = "col_warnings";
            this.col_warnings.ReadOnly = true;
            //
            // panel_titlebar
            //
            this.panel_titlebar.BackColor = System.Drawing.Color.Orange;
            this.panel_titlebar.Controls.Add(this.label4);
            this.panel_titlebar.Controls.Add(this.titleBar1);
            this.panel_titlebar.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel_titlebar.Location = new System.Drawing.Point(0, 0);
            this.panel_titlebar.Name = "panel_titlebar";
            this.panel_titlebar.Size = new System.Drawing.Size(998, 30);
            this.panel_titlebar.TabIndex = 0;
            //
            // label4
            //
            this.label4.AutoSize = true;
            this.label4.Dock = System.Windows.Forms.DockStyle.Left;
            this.label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.Location = new System.Drawing.Point(0, 0);
            this.label4.Name = "label4";
            this.label4.Padding = new System.Windows.Forms.Padding(0, 2, 0, 0);
            this.label4.Size = new System.Drawing.Size(117, 26);
            this.label4.TabIndex = 3;
            this.label4.Text = "Import Leave";
            //
            // titleBar1
            //
            this.titleBar1.CloseMessage = "Exit Application?";
            this.titleBar1.CloseTitle = "Exit";
            this.titleBar1.Dock = System.Windows.Forms.DockStyle.Right;
            this.titleBar1.EnableDialog = false;
            this.titleBar1.Location = new System.Drawing.Point(894, 0);
            this.titleBar1.Name = "titleBar1";
            this.titleBar1.ShowMinimizeButton = false;
            this.titleBar1.Size = new System.Drawing.Size(104, 30);
            this.titleBar1.TabIndex = 0;
            //
            // frmImportLeave
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1000, 620);
            this.Controls.Add(this.panel1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "frmImportLeave";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "frmImportLeave";
            this.panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.panel_titlebar.ResumeLayout(false);
            this.panel_titlebar.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Panel panel_titlebar;
        private System.Windows.Forms.Label label4;
        private Button_Control.TitleBar titleBar1;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.Label label_summary;
        private CustomControls.RJControls.RJButton btn_apply;
        private CustomControls.RJControls.RJButton btn_apply_save;
        private CustomControls.RJControls.RJButton btn_cancel;
        private System.Windows.Forms.DataGridViewTextBoxColumn col_sheet_name;
        private System.Windows.Forms.DataGridViewComboBoxColumn col_person;
        private System.Windows.Forms.DataGridViewTextBoxColumn col_match;
        private System.Windows.Forms.DataGridViewTextBoxColumn col_days;
        private System.Windows.Forms.DataGridViewTextBoxColumn col_dates;
        private System.Windows.Forms.DataGridViewTextBoxColumn col_warnings;
    }
}
