namespace TimeTable_Generator
{
    partial class MainPage
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
            this.date_end = new Button_Control.DTP.RJDatePicker();
            this.date_start = new Button_Control.DTP.RJDatePicker();
            this.panel1 = new System.Windows.Forms.Panel();
            this.btn_reset = new CustomControls.RJControls.RJButton();
            this.label_import = new System.Windows.Forms.Label();
            this.btn_import = new CustomControls.RJControls.RJButton();
            this.panel_titlebar = new System.Windows.Forms.Panel();
            this.label4 = new System.Windows.Forms.Label();
            this.titleBar1 = new Button_Control.TitleBar();
            this.btn_continue = new CustomControls.RJControls.RJButton();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.panel1.SuspendLayout();
            this.panel_titlebar.SuspendLayout();
            this.SuspendLayout();
            // 
            // date_end
            // 
            this.date_end.BorderColor = System.Drawing.Color.Black;
            this.date_end.BorderSize = 1;
            this.date_end.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.5F);
            this.date_end.Location = new System.Drawing.Point(372, 127);
            this.date_end.MinimumSize = new System.Drawing.Size(4, 35);
            this.date_end.Name = "date_end";
            this.date_end.Size = new System.Drawing.Size(251, 35);
            this.date_end.SkinColor = System.Drawing.Color.Pink;
            this.date_end.TabIndex = 0;
            this.date_end.TextColor = System.Drawing.Color.Black;
            // 
            // date_start
            // 
            this.date_start.BorderColor = System.Drawing.Color.Black;
            this.date_start.BorderSize = 1;
            this.date_start.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.5F);
            this.date_start.Location = new System.Drawing.Point(69, 127);
            this.date_start.MinimumSize = new System.Drawing.Size(4, 35);
            this.date_start.Name = "date_start";
            this.date_start.Size = new System.Drawing.Size(251, 35);
            this.date_start.SkinColor = System.Drawing.Color.PaleGreen;
            this.date_start.TabIndex = 1;
            this.date_start.TextColor = System.Drawing.Color.Black;
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.AntiqueWhite;
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel1.Controls.Add(this.btn_reset);
            this.panel1.Controls.Add(this.label_import);
            this.panel1.Controls.Add(this.btn_import);
            this.panel1.Controls.Add(this.panel_titlebar);
            this.panel1.Controls.Add(this.btn_continue);
            this.panel1.Controls.Add(this.label3);
            this.panel1.Controls.Add(this.label2);
            this.panel1.Controls.Add(this.label1);
            this.panel1.Controls.Add(this.date_start);
            this.panel1.Controls.Add(this.date_end);
            this.panel1.Controls.Add(this.label5);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(699, 235);
            this.panel1.TabIndex = 2;
            // 
            // btn_reset
            // 
            this.btn_reset.BackColor = System.Drawing.Color.Plum;
            this.btn_reset.BackgroundColor = System.Drawing.Color.Plum;
            this.btn_reset.BorderColor = System.Drawing.Color.Black;
            this.btn_reset.BorderRadius = 0;
            this.btn_reset.BorderSize = 1;
            this.btn_reset.FlatAppearance.BorderSize = 0;
            this.btn_reset.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_reset.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_reset.ForeColor = System.Drawing.Color.Black;
            this.btn_reset.Location = new System.Drawing.Point(92, 175);
            this.btn_reset.Name = "btn_reset";
            this.btn_reset.Size = new System.Drawing.Size(91, 40);
            this.btn_reset.TabIndex = 9;
            this.btn_reset.Text = "Reset";
            this.btn_reset.TextColor = System.Drawing.Color.Black;
            this.btn_reset.UseVisualStyleBackColor = false;
            this.btn_reset.Visible = false;
            this.btn_reset.Click += new System.EventHandler(this.btn_reset_Click);
            // 
            // label_import
            // 
            this.label_import.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label_import.Location = new System.Drawing.Point(501, 179);
            this.label_import.Name = "label_import";
            this.label_import.Size = new System.Drawing.Size(139, 50);
            this.label_import.TabIndex = 8;
            this.label_import.Text = "Import Successful. Using Imported data.";
            this.label_import.Visible = false;
            // 
            // btn_import
            // 
            this.btn_import.BackColor = System.Drawing.Color.Gainsboro;
            this.btn_import.BackgroundColor = System.Drawing.Color.Gainsboro;
            this.btn_import.BorderColor = System.Drawing.Color.Black;
            this.btn_import.BorderRadius = 0;
            this.btn_import.BorderSize = 1;
            this.btn_import.FlatAppearance.BorderSize = 0;
            this.btn_import.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_import.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_import.ForeColor = System.Drawing.Color.Black;
            this.btn_import.Location = new System.Drawing.Point(189, 175);
            this.btn_import.Name = "btn_import";
            this.btn_import.Size = new System.Drawing.Size(150, 40);
            this.btn_import.TabIndex = 7;
            this.btn_import.Text = "Import Data";
            this.btn_import.TextColor = System.Drawing.Color.Black;
            this.btn_import.UseVisualStyleBackColor = false;
            this.btn_import.Click += new System.EventHandler(this.btn_import_Click);
            // 
            // panel_titlebar
            // 
            this.panel_titlebar.BackColor = System.Drawing.Color.Orange;
            this.panel_titlebar.Controls.Add(this.label4);
            this.panel_titlebar.Controls.Add(this.titleBar1);
            this.panel_titlebar.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel_titlebar.Location = new System.Drawing.Point(0, 0);
            this.panel_titlebar.Name = "panel_titlebar";
            this.panel_titlebar.Size = new System.Drawing.Size(697, 30);
            this.panel_titlebar.TabIndex = 6;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Dock = System.Windows.Forms.DockStyle.Left;
            this.label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.Location = new System.Drawing.Point(0, 0);
            this.label4.Name = "label4";
            this.label4.Padding = new System.Windows.Forms.Padding(0, 2, 0, 0);
            this.label4.Size = new System.Drawing.Size(97, 26);
            this.label4.TabIndex = 3;
            this.label4.Text = "ROTA-OC";
            this.label4.Click += new System.EventHandler(this.label4_Click);
            // 
            // titleBar1
            // 
            this.titleBar1.CloseMessage = "Exit Application?";
            this.titleBar1.CloseTitle = "Exit";
            this.titleBar1.Dock = System.Windows.Forms.DockStyle.Right;
            this.titleBar1.EnableDialog = true;
            this.titleBar1.Location = new System.Drawing.Point(593, 0);
            this.titleBar1.Name = "titleBar1";
            this.titleBar1.ShowMinimizeButton = false;
            this.titleBar1.Size = new System.Drawing.Size(104, 30);
            this.titleBar1.TabIndex = 0;
            // 
            // btn_continue
            // 
            this.btn_continue.BackColor = System.Drawing.Color.LightCoral;
            this.btn_continue.BackgroundColor = System.Drawing.Color.LightCoral;
            this.btn_continue.BorderColor = System.Drawing.Color.Black;
            this.btn_continue.BorderRadius = 0;
            this.btn_continue.BorderSize = 1;
            this.btn_continue.FlatAppearance.BorderSize = 0;
            this.btn_continue.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_continue.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_continue.ForeColor = System.Drawing.Color.Black;
            this.btn_continue.Location = new System.Drawing.Point(345, 175);
            this.btn_continue.Name = "btn_continue";
            this.btn_continue.Size = new System.Drawing.Size(150, 40);
            this.btn_continue.TabIndex = 5;
            this.btn_continue.Text = "Continue";
            this.btn_continue.TextColor = System.Drawing.Color.Black;
            this.btn_continue.UseVisualStyleBackColor = false;
            this.btn_continue.Click += new System.EventHandler(this.btn_continue_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Verdana", 26.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(239, 33);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(198, 42);
            this.label3.TabIndex = 4;
            this.label3.Text = "ROTA-OC";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 15.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(441, 99);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(101, 25);
            this.label2.TabIndex = 3;
            this.label2.Text = "End Date";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 15.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(130, 99);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(108, 25);
            this.label1.TabIndex = 2;
            this.label1.Text = "Start Date";
            // 
            // label5
            // 
            this.label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.Location = new System.Drawing.Point(137, 74);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(396, 50);
            this.label5.TabIndex = 10;
            this.label5.Text = "Rotational Optimisation Tool for On-Callers";
            this.label5.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            this.label5.Click += new System.EventHandler(this.label5_Click);
            // 
            // MainPage
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(699, 235);
            this.Controls.Add(this.panel1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "MainPage";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Form1";
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panel_titlebar.ResumeLayout(false);
            this.panel_titlebar.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private Button_Control.DTP.RJDatePicker date_end;
        private Button_Control.DTP.RJDatePicker date_start;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private CustomControls.RJControls.RJButton btn_continue;
        private System.Windows.Forms.Panel panel_titlebar;
        private Button_Control.TitleBar titleBar1;
        private System.Windows.Forms.Label label4;
        private CustomControls.RJControls.RJButton btn_import;
        private System.Windows.Forms.Label label_import;
        private CustomControls.RJControls.RJButton btn_reset;
        private System.Windows.Forms.Label label5;
    }
}

