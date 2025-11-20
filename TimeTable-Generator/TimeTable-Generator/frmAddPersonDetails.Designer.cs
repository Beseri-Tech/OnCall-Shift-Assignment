namespace TimeTable_Generator
{
    partial class frmAddPersonDetails
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
            this.label6 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.btn_clear = new CustomControls.RJControls.RJButton();
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            this.tx_preferred = new System.Windows.Forms.TextBox();
            this.rjButton2 = new CustomControls.RJControls.RJButton();
            this.list_preferred = new System.Windows.Forms.ListBox();
            this.label3 = new System.Windows.Forms.Label();
            this.tx_leavedate = new System.Windows.Forms.TextBox();
            this.btn_add = new CustomControls.RJControls.RJButton();
            this.rjButton1 = new CustomControls.RJControls.RJButton();
            this.btn_save = new CustomControls.RJControls.RJButton();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.list_leavedates = new System.Windows.Forms.ListBox();
            this.tx_name = new CustomControls.RJControls.RJTextBox();
            this.panel_titlebar = new System.Windows.Forms.Panel();
            this.label4 = new System.Windows.Forms.Label();
            this.titleBar1 = new Button_Control.TitleBar();
            this.monthCalendar1 = new System.Windows.Forms.MonthCalendar();
            this.buttonAddRange = new CustomControls.RJControls.RJButton();
            this.panel1.SuspendLayout();
            this.panel_titlebar.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.AntiqueWhite;
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel1.Controls.Add(this.buttonAddRange);
            this.panel1.Controls.Add(this.monthCalendar1);
            this.panel1.Controls.Add(this.label6);
            this.panel1.Controls.Add(this.label5);
            this.panel1.Controls.Add(this.btn_clear);
            this.panel1.Controls.Add(this.checkBox1);
            this.panel1.Controls.Add(this.tx_preferred);
            this.panel1.Controls.Add(this.rjButton2);
            this.panel1.Controls.Add(this.list_preferred);
            this.panel1.Controls.Add(this.label3);
            this.panel1.Controls.Add(this.tx_leavedate);
            this.panel1.Controls.Add(this.btn_add);
            this.panel1.Controls.Add(this.rjButton1);
            this.panel1.Controls.Add(this.btn_save);
            this.panel1.Controls.Add(this.label2);
            this.panel1.Controls.Add(this.label1);
            this.panel1.Controls.Add(this.list_leavedates);
            this.panel1.Controls.Add(this.tx_name);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(893, 448);
            this.panel1.TabIndex = 0;
            this.panel1.Paint += new System.Windows.Forms.PaintEventHandler(this.panel1_Paint);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label6.Location = new System.Drawing.Point(26, 213);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(71, 13);
            this.label6.TabIndex = 30;
            this.label6.Text = "Single  : Date";
            this.label6.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.Location = new System.Drawing.Point(26, 228);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(109, 13);
            this.label5.TabIndex = 29;
            this.label5.Text = "Range : Date to Date";
            this.label5.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // btn_clear
            // 
            this.btn_clear.BackColor = System.Drawing.Color.Crimson;
            this.btn_clear.BackgroundColor = System.Drawing.Color.Crimson;
            this.btn_clear.BorderColor = System.Drawing.Color.Black;
            this.btn_clear.BorderRadius = 0;
            this.btn_clear.BorderSize = 1;
            this.btn_clear.FlatAppearance.BorderSize = 0;
            this.btn_clear.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_clear.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_clear.ForeColor = System.Drawing.Color.White;
            this.btn_clear.Location = new System.Drawing.Point(477, 240);
            this.btn_clear.Name = "btn_clear";
            this.btn_clear.Size = new System.Drawing.Size(141, 41);
            this.btn_clear.TabIndex = 28;
            this.btn_clear.Text = "Clear All Date";
            this.btn_clear.TextColor = System.Drawing.Color.White;
            this.btn_clear.UseVisualStyleBackColor = false;
            this.btn_clear.Click += new System.EventHandler(this.btn_clear_Click);
            // 
            // checkBox1
            // 
            this.checkBox1.AutoSize = true;
            this.checkBox1.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.checkBox1.Location = new System.Drawing.Point(211, 404);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(154, 24);
            this.checkBox1.TabIndex = 27;
            this.checkBox1.Text = "Assign Extra Shift";
            this.checkBox1.UseVisualStyleBackColor = true;
            // 
            // tx_preferred
            // 
            this.tx_preferred.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F);
            this.tx_preferred.Location = new System.Drawing.Point(29, 311);
            this.tx_preferred.Name = "tx_preferred";
            this.tx_preferred.Size = new System.Drawing.Size(150, 26);
            this.tx_preferred.TabIndex = 26;
            // 
            // rjButton2
            // 
            this.rjButton2.BackColor = System.Drawing.Color.Snow;
            this.rjButton2.BackgroundColor = System.Drawing.Color.Snow;
            this.rjButton2.BorderColor = System.Drawing.Color.Black;
            this.rjButton2.BorderRadius = 0;
            this.rjButton2.BorderSize = 1;
            this.rjButton2.FlatAppearance.BorderSize = 0;
            this.rjButton2.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rjButton2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.rjButton2.ForeColor = System.Drawing.Color.Black;
            this.rjButton2.Location = new System.Drawing.Point(29, 344);
            this.rjButton2.Name = "rjButton2";
            this.rjButton2.Size = new System.Drawing.Size(150, 25);
            this.rjButton2.TabIndex = 25;
            this.rjButton2.Text = "Add Preferred Date";
            this.rjButton2.TextColor = System.Drawing.Color.Black;
            this.rjButton2.UseVisualStyleBackColor = false;
            this.rjButton2.Click += new System.EventHandler(this.rjButton2_Click);
            // 
            // list_preferred
            // 
            this.list_preferred.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.list_preferred.FormattingEnabled = true;
            this.list_preferred.ItemHeight = 20;
            this.list_preferred.Location = new System.Drawing.Point(211, 265);
            this.list_preferred.Name = "list_preferred";
            this.list_preferred.Size = new System.Drawing.Size(250, 124);
            this.list_preferred.TabIndex = 24;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 15.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(24, 265);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(175, 25);
            this.label3.TabIndex = 23;
            this.label3.Text = "Preferred Dates :";
            // 
            // tx_leavedate
            // 
            this.tx_leavedate.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F);
            this.tx_leavedate.Location = new System.Drawing.Point(29, 150);
            this.tx_leavedate.Name = "tx_leavedate";
            this.tx_leavedate.Size = new System.Drawing.Size(150, 26);
            this.tx_leavedate.TabIndex = 22;
            this.tx_leavedate.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.tx_leavedate_KeyPress);
            // 
            // btn_add
            // 
            this.btn_add.BackColor = System.Drawing.Color.LightBlue;
            this.btn_add.BackgroundColor = System.Drawing.Color.LightBlue;
            this.btn_add.BorderColor = System.Drawing.Color.Black;
            this.btn_add.BorderRadius = 0;
            this.btn_add.BorderSize = 1;
            this.btn_add.FlatAppearance.BorderSize = 0;
            this.btn_add.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_add.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_add.ForeColor = System.Drawing.Color.Black;
            this.btn_add.Location = new System.Drawing.Point(29, 183);
            this.btn_add.Name = "btn_add";
            this.btn_add.Size = new System.Drawing.Size(150, 25);
            this.btn_add.TabIndex = 18;
            this.btn_add.Text = "Add Leave Date";
            this.btn_add.TextColor = System.Drawing.Color.Black;
            this.btn_add.UseVisualStyleBackColor = false;
            this.btn_add.Click += new System.EventHandler(this.btn_add_Click);
            // 
            // rjButton1
            // 
            this.rjButton1.BackColor = System.Drawing.Color.LightPink;
            this.rjButton1.BackgroundColor = System.Drawing.Color.LightPink;
            this.rjButton1.BorderColor = System.Drawing.Color.Black;
            this.rjButton1.BorderRadius = 0;
            this.rjButton1.BorderSize = 1;
            this.rjButton1.FlatAppearance.BorderSize = 0;
            this.rjButton1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rjButton1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.rjButton1.ForeColor = System.Drawing.Color.Black;
            this.rjButton1.Location = new System.Drawing.Point(477, 193);
            this.rjButton1.Name = "rjButton1";
            this.rjButton1.Size = new System.Drawing.Size(141, 41);
            this.rjButton1.TabIndex = 16;
            this.rjButton1.Text = "Delete Selected Date";
            this.rjButton1.TextColor = System.Drawing.Color.Black;
            this.rjButton1.UseVisualStyleBackColor = false;
            this.rjButton1.Click += new System.EventHandler(this.rjButton1_Click);
            // 
            // btn_save
            // 
            this.btn_save.BackColor = System.Drawing.Color.PaleGreen;
            this.btn_save.BackgroundColor = System.Drawing.Color.PaleGreen;
            this.btn_save.BorderColor = System.Drawing.Color.Black;
            this.btn_save.BorderRadius = 0;
            this.btn_save.BorderSize = 1;
            this.btn_save.FlatAppearance.BorderSize = 0;
            this.btn_save.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_save.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_save.ForeColor = System.Drawing.Color.Black;
            this.btn_save.Location = new System.Drawing.Point(477, 287);
            this.btn_save.Name = "btn_save";
            this.btn_save.Size = new System.Drawing.Size(141, 52);
            this.btn_save.TabIndex = 15;
            this.btn_save.Text = "Save";
            this.btn_save.TextColor = System.Drawing.Color.Black;
            this.btn_save.UseVisualStyleBackColor = false;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 15.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(24, 105);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(145, 25);
            this.label2.TabIndex = 13;
            this.label2.Text = "Leave Dates :";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 15.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(24, 50);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(80, 25);
            this.label1.TabIndex = 12;
            this.label1.Text = "Name :";
            // 
            // list_leavedates
            // 
            this.list_leavedates.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.list_leavedates.FormattingEnabled = true;
            this.list_leavedates.ItemHeight = 20;
            this.list_leavedates.Location = new System.Drawing.Point(211, 109);
            this.list_leavedates.Name = "list_leavedates";
            this.list_leavedates.Size = new System.Drawing.Size(250, 124);
            this.list_leavedates.TabIndex = 1;
            // 
            // tx_name
            // 
            this.tx_name.BackColor = System.Drawing.SystemColors.Window;
            this.tx_name.BorderColor = System.Drawing.Color.MidnightBlue;
            this.tx_name.BorderFocusColor = System.Drawing.Color.HotPink;
            this.tx_name.BorderRadius = 0;
            this.tx_name.BorderSize = 2;
            this.tx_name.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tx_name.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.tx_name.Location = new System.Drawing.Point(211, 49);
            this.tx_name.Margin = new System.Windows.Forms.Padding(4);
            this.tx_name.Multiline = false;
            this.tx_name.Name = "tx_name";
            this.tx_name.Padding = new System.Windows.Forms.Padding(10, 7, 10, 7);
            this.tx_name.PasswordChar = false;
            this.tx_name.PlaceholderColor = System.Drawing.Color.DarkGray;
            this.tx_name.PlaceholderText = "";
            this.tx_name.ReadOnly = false;
            this.tx_name.Size = new System.Drawing.Size(381, 35);
            this.tx_name.TabIndex = 0;
            this.tx_name.TextAlign = System.Windows.Forms.HorizontalAlignment.Left;
            this.tx_name.TextBoxTextAlign = System.Windows.Forms.HorizontalAlignment.Left;
            this.tx_name.Texts = "";
            this.tx_name.UnderlinedStyle = false;
            this.tx_name.VScroll = true;
            // 
            // panel_titlebar
            // 
            this.panel_titlebar.BackColor = System.Drawing.Color.Orange;
            this.panel_titlebar.Controls.Add(this.label4);
            this.panel_titlebar.Controls.Add(this.titleBar1);
            this.panel_titlebar.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel_titlebar.Location = new System.Drawing.Point(0, 0);
            this.panel_titlebar.Name = "panel_titlebar";
            this.panel_titlebar.Size = new System.Drawing.Size(893, 30);
            this.panel_titlebar.TabIndex = 11;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Dock = System.Windows.Forms.DockStyle.Left;
            this.label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.Location = new System.Drawing.Point(0, 0);
            this.label4.Name = "label4";
            this.label4.Padding = new System.Windows.Forms.Padding(0, 2, 0, 0);
            this.label4.Size = new System.Drawing.Size(170, 26);
            this.label4.TabIndex = 3;
            this.label4.Text = "Add Person Details";
            // 
            // titleBar1
            // 
            this.titleBar1.CloseMessage = "Exit Application?";
            this.titleBar1.CloseTitle = "Exit";
            this.titleBar1.Dock = System.Windows.Forms.DockStyle.Right;
            this.titleBar1.EnableDialog = false;
            this.titleBar1.Location = new System.Drawing.Point(789, 0);
            this.titleBar1.Name = "titleBar1";
            this.titleBar1.ShowMinimizeButton = false;
            this.titleBar1.Size = new System.Drawing.Size(104, 30);
            this.titleBar1.TabIndex = 0;
            // 
            // monthCalendar1
            // 
            this.monthCalendar1.Location = new System.Drawing.Point(647, 79);
            this.monthCalendar1.Name = "monthCalendar1";
            this.monthCalendar1.TabIndex = 31;
            // 
            // buttonAddRange
            // 
            this.buttonAddRange.BackColor = System.Drawing.Color.Thistle;
            this.buttonAddRange.BackgroundColor = System.Drawing.Color.Thistle;
            this.buttonAddRange.BorderColor = System.Drawing.Color.Black;
            this.buttonAddRange.BorderRadius = 0;
            this.buttonAddRange.BorderSize = 1;
            this.buttonAddRange.FlatAppearance.BorderSize = 0;
            this.buttonAddRange.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonAddRange.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonAddRange.ForeColor = System.Drawing.Color.Black;
            this.buttonAddRange.Location = new System.Drawing.Point(688, 248);
            this.buttonAddRange.Name = "buttonAddRange";
            this.buttonAddRange.Size = new System.Drawing.Size(150, 25);
            this.buttonAddRange.TabIndex = 32;
            this.buttonAddRange.Text = "Add Selected Dates";
            this.buttonAddRange.TextColor = System.Drawing.Color.Black;
            this.buttonAddRange.UseVisualStyleBackColor = false;
            this.buttonAddRange.Click += new System.EventHandler(this.buttonAddRange_Click);
            // 
            // frmAddPersonDetails
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(893, 448);
            this.Controls.Add(this.panel_titlebar);
            this.Controls.Add(this.panel1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "frmAddPersonDetails";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "t";
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panel_titlebar.ResumeLayout(false);
            this.panel_titlebar.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private CustomControls.RJControls.RJTextBox tx_name;
        private System.Windows.Forms.ListBox list_leavedates;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private CustomControls.RJControls.RJButton btn_save;
        private System.Windows.Forms.Panel panel_titlebar;
        private System.Windows.Forms.Label label4;
        private Button_Control.TitleBar titleBar1;
        private CustomControls.RJControls.RJButton rjButton1;
        private CustomControls.RJControls.RJButton btn_add;
        private System.Windows.Forms.TextBox tx_leavedate;
        private System.Windows.Forms.ListBox list_preferred;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox tx_preferred;
        private CustomControls.RJControls.RJButton rjButton2;
        private System.Windows.Forms.CheckBox checkBox1;
        private CustomControls.RJControls.RJButton btn_clear;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label5;
        private CustomControls.RJControls.RJButton buttonAddRange;
        private System.Windows.Forms.MonthCalendar monthCalendar1;
    }
}