namespace TimeTable_Generator
{
    partial class frmAddPeople
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
            this.label1 = new System.Windows.Forms.Label();
            this.tx_name = new CustomControls.RJControls.RJTextBox();
            this.panel_titlebar = new System.Windows.Forms.Panel();
            this.label4 = new System.Windows.Forms.Label();
            this.titleBar1 = new Button_Control.TitleBar();
            this.btn_save = new CustomControls.RJControls.RJButton();
            this.panel1.SuspendLayout();
            this.panel_titlebar.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.AntiqueWhite;
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel1.Controls.Add(this.btn_save);
            this.panel1.Controls.Add(this.label1);
            this.panel1.Controls.Add(this.tx_name);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(631, 453);
            this.panel1.TabIndex = 0;
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
            // tx_name
            // 
            this.tx_name.BackColor = System.Drawing.SystemColors.Window;
            this.tx_name.BorderColor = System.Drawing.Color.MidnightBlue;
            this.tx_name.BorderFocusColor = System.Drawing.Color.HotPink;
            this.tx_name.BorderRadius = 0;
            this.tx_name.BorderSize = 2;
            this.tx_name.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tx_name.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.tx_name.Location = new System.Drawing.Point(139, 50);
            this.tx_name.Margin = new System.Windows.Forms.Padding(4);
            this.tx_name.Multiline = true;
            this.tx_name.Name = "tx_name";
            this.tx_name.Padding = new System.Windows.Forms.Padding(10, 7, 10, 7);
            this.tx_name.PasswordChar = false;
            this.tx_name.PlaceholderColor = System.Drawing.Color.DarkGray;
            this.tx_name.PlaceholderText = "";
            this.tx_name.ReadOnly = false;
            this.tx_name.Size = new System.Drawing.Size(439, 314);
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
            this.panel_titlebar.Size = new System.Drawing.Size(631, 30);
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
            this.titleBar1.Location = new System.Drawing.Point(527, 0);
            this.titleBar1.Name = "titleBar1";
            this.titleBar1.ShowMinimizeButton = false;
            this.titleBar1.Size = new System.Drawing.Size(104, 30);
            this.titleBar1.TabIndex = 0;
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
            this.btn_save.Location = new System.Drawing.Point(437, 371);
            this.btn_save.Name = "btn_save";
            this.btn_save.Size = new System.Drawing.Size(141, 52);
            this.btn_save.TabIndex = 16;
            this.btn_save.Text = "Save";
            this.btn_save.TextColor = System.Drawing.Color.Black;
            this.btn_save.UseVisualStyleBackColor = false;
            this.btn_save.Click += new System.EventHandler(this.btn_save_Click);
            // 
            // frmAddPeople
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(631, 453);
            this.Controls.Add(this.panel_titlebar);
            this.Controls.Add(this.panel1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "frmAddPeople";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "frmAddPersonDetails";
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panel_titlebar.ResumeLayout(false);
            this.panel_titlebar.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private CustomControls.RJControls.RJTextBox tx_name;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Panel panel_titlebar;
        private System.Windows.Forms.Label label4;
        private Button_Control.TitleBar titleBar1;
        private CustomControls.RJControls.RJButton btn_save;
    }
}