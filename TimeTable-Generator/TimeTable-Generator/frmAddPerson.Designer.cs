namespace TimeTable_Generator
{
    partial class frmAddPerson
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
            this.components = new System.ComponentModel.Container();
            this.panel1 = new System.Windows.Forms.Panel();
            this.panel_titlebar = new System.Windows.Forms.Panel();
            this.label4 = new System.Windows.Forms.Label();
            this.titleBar1 = new Button_Control.TitleBar();
            this.panel_header = new System.Windows.Forms.Panel();
            this.panel_info = new System.Windows.Forms.Panel();
            this.label_date = new System.Windows.Forms.Label();
            this.flow_stats = new System.Windows.Forms.FlowLayoutPanel();
            this.label_total = new System.Windows.Forms.Label();
            this.label_holidays = new System.Windows.Forms.Label();
            this.label_shift_total = new System.Windows.Forms.Label();
            this.label_shift_assign = new System.Windows.Forms.Label();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.flow_toolbar = new System.Windows.Forms.FlowLayoutPanel();
            this.group_people = new System.Windows.Forms.FlowLayoutPanel();
            this.caption_people = new System.Windows.Forms.Label();
            this.flow_people = new System.Windows.Forms.FlowLayoutPanel();
            this.btn_continue = new CustomControls.RJControls.RJButton();
            this.btn_batch = new CustomControls.RJControls.RJButton();
            this.rjButton1 = new CustomControls.RJControls.RJButton();
            this.separator_people = new System.Windows.Forms.Panel();
            this.group_data = new System.Windows.Forms.FlowLayoutPanel();
            this.caption_data = new System.Windows.Forms.Label();
            this.flow_data = new System.Windows.Forms.FlowLayoutPanel();
            this.btn_import_leave = new CustomControls.RJControls.RJButton();
            this.btn_import_history = new CustomControls.RJControls.RJButton();
            this.btn_holidays = new CustomControls.RJControls.RJButton();
            this.btn_date = new CustomControls.RJControls.RJButton();
            this.btn_clear = new CustomControls.RJControls.RJButton();
            this.separator_data = new System.Windows.Forms.Panel();
            this.group_rota = new System.Windows.Forms.FlowLayoutPanel();
            this.caption_rota = new System.Windows.Forms.Label();
            this.flow_rota = new System.Windows.Forms.FlowLayoutPanel();
            this.btn_assign = new CustomControls.RJControls.RJButton();
            this.btn_reset = new CustomControls.RJControls.RJButton();
            this.separator_rota = new System.Windows.Forms.Panel();
            this.group_export = new System.Windows.Forms.FlowLayoutPanel();
            this.caption_export = new System.Windows.Forms.Label();
            this.flow_export = new System.Windows.Forms.FlowLayoutPanel();
            this.btn_download = new CustomControls.RJControls.RJButton();
            this.rjButton2 = new CustomControls.RJControls.RJButton();
            this.btn_export_history = new CustomControls.RJControls.RJButton();
            this.separator_export = new System.Windows.Forms.Panel();
            this.group_search = new System.Windows.Forms.FlowLayoutPanel();
            this.caption_search = new System.Windows.Forms.Label();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.panel_grid = new System.Windows.Forms.Panel();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.PersonName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.weekend = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.ExtraShift = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.preferred_date = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.LeaveDatesString = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.TotalLeaveDays = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.AssignedShiftsString = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.LeaveDates = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.AssignedShifts = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.WeekdayShifts = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.WeekendShifts = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.PriorWeekendShifts = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.TotalShift = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.panel1.SuspendLayout();
            this.panel_titlebar.SuspendLayout();
            this.panel_header.SuspendLayout();
            this.panel_info.SuspendLayout();
            this.flow_stats.SuspendLayout();
            this.flow_toolbar.SuspendLayout();
            this.group_people.SuspendLayout();
            this.flow_people.SuspendLayout();
            this.group_data.SuspendLayout();
            this.flow_data.SuspendLayout();
            this.group_rota.SuspendLayout();
            this.flow_rota.SuspendLayout();
            this.group_export.SuspendLayout();
            this.flow_export.SuspendLayout();
            this.group_search.SuspendLayout();
            this.panel_grid.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(241)))), ((int)(((byte)(245)))), ((int)(((byte)(249)))));
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel1.Controls.Add(this.panel_grid);
            this.panel1.Controls.Add(this.panel_header);
            this.panel1.Controls.Add(this.panel_titlebar);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(1280, 800);
            this.panel1.TabIndex = 0;
            // 
            // panel_titlebar
            // 
            this.panel_titlebar.BackColor = System.Drawing.Color.Orange;
            this.panel_titlebar.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel_titlebar.Controls.Add(this.label4);
            this.panel_titlebar.Controls.Add(this.titleBar1);
            this.panel_titlebar.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel_titlebar.Location = new System.Drawing.Point(0, 0);
            this.panel_titlebar.Name = "panel_titlebar";
            this.panel_titlebar.Size = new System.Drawing.Size(798, 30);
            this.panel_titlebar.TabIndex = 10;
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
            // 
            // titleBar1
            // 
            this.titleBar1.CloseMessage = "Exit Application?";
            this.titleBar1.CloseTitle = "Exit";
            this.titleBar1.Dock = System.Windows.Forms.DockStyle.Right;
            this.titleBar1.EnableDialog = false;
            this.titleBar1.Location = new System.Drawing.Point(692, 0);
            this.titleBar1.Name = "titleBar1";
            this.titleBar1.ShowMinimizeButton = false;
            this.titleBar1.Size = new System.Drawing.Size(104, 28);
            this.titleBar1.TabIndex = 0;
            // 
            // panel_header
            // 
            this.panel_header.BackColor = System.Drawing.Color.White;
            this.panel_header.Controls.Add(this.flow_toolbar);
            this.panel_header.Controls.Add(this.panel_info);
            this.panel_header.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel_header.Name = "panel_header";
            this.panel_header.Size = new System.Drawing.Size(1278, 126);
            this.panel_header.Padding = new System.Windows.Forms.Padding(12, 6, 12, 10);
            this.panel_header.TabIndex = 1;
            // 
            // panel_info
            // 
            this.panel_info.Controls.Add(this.label_date);
            this.panel_info.Controls.Add(this.flow_stats);
            this.panel_info.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel_info.Name = "panel_info";
            this.panel_info.Size = new System.Drawing.Size(1254, 44);
            this.panel_info.TabIndex = 0;
            // 
            // label_date
            // 
            this.label_date.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label_date.Font = new System.Drawing.Font("Segoe UI", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label_date.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(15)))), ((int)(((byte)(23)))), ((int)(((byte)(42)))));
            this.label_date.Name = "label_date";
            this.label_date.TabIndex = 0;
            this.label_date.Text = "Rota  DD/MM/yyyy \u2013 DD/MM/yyyy";
            this.label_date.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // flow_stats
            // 
            this.flow_stats.AutoSize = true;
            this.flow_stats.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.flow_stats.Controls.Add(this.label_total);
            this.flow_stats.Controls.Add(this.label_holidays);
            this.flow_stats.Controls.Add(this.label_shift_total);
            this.flow_stats.Controls.Add(this.label_shift_assign);
            this.flow_stats.Controls.Add(this.progressBar1);
            this.flow_stats.Dock = System.Windows.Forms.DockStyle.Right;
            this.flow_stats.Name = "flow_stats";
            this.flow_stats.TabIndex = 1;
            this.flow_stats.WrapContents = false;
            // 
            // label_total
            // 
            this.label_total.AutoSize = true;
            this.label_total.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(238)))), ((int)(((byte)(242)))), ((int)(((byte)(255)))));
            this.label_total.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label_total.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(55)))), ((int)(((byte)(48)))), ((int)(((byte)(163)))));
            this.label_total.Margin = new System.Windows.Forms.Padding(8, 10, 0, 0);
            this.label_total.Name = "label_total";
            this.label_total.Padding = new System.Windows.Forms.Padding(8, 4, 8, 4);
            this.label_total.TabIndex = 0;
            this.label_total.Text = "People  0";
            // 
            // label_holidays
            // 
            this.label_holidays.AutoSize = true;
            this.label_holidays.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(238)))), ((int)(((byte)(242)))), ((int)(((byte)(255)))));
            this.label_holidays.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label_holidays.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(55)))), ((int)(((byte)(48)))), ((int)(((byte)(163)))));
            this.label_holidays.Margin = new System.Windows.Forms.Padding(8, 10, 0, 0);
            this.label_holidays.Name = "label_holidays";
            this.label_holidays.Padding = new System.Windows.Forms.Padding(8, 4, 8, 4);
            this.label_holidays.TabIndex = 1;
            this.label_holidays.Text = "Holidays  0";
            // 
            // label_shift_total
            // 
            this.label_shift_total.AutoSize = true;
            this.label_shift_total.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(238)))), ((int)(((byte)(242)))), ((int)(((byte)(255)))));
            this.label_shift_total.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label_shift_total.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(55)))), ((int)(((byte)(48)))), ((int)(((byte)(163)))));
            this.label_shift_total.Margin = new System.Windows.Forms.Padding(8, 10, 0, 0);
            this.label_shift_total.Name = "label_shift_total";
            this.label_shift_total.Padding = new System.Windows.Forms.Padding(8, 4, 8, 4);
            this.label_shift_total.TabIndex = 2;
            this.label_shift_total.Text = "Shifts  0";
            // 
            // label_shift_assign
            // 
            this.label_shift_assign.AutoSize = true;
            this.label_shift_assign.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(238)))), ((int)(((byte)(242)))), ((int)(((byte)(255)))));
            this.label_shift_assign.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label_shift_assign.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(55)))), ((int)(((byte)(48)))), ((int)(((byte)(163)))));
            this.label_shift_assign.Margin = new System.Windows.Forms.Padding(8, 10, 0, 0);
            this.label_shift_assign.Name = "label_shift_assign";
            this.label_shift_assign.Padding = new System.Windows.Forms.Padding(8, 4, 8, 4);
            this.label_shift_assign.TabIndex = 3;
            this.label_shift_assign.Text = "Assigned  0";
            // 
            // progressBar1
            // 
            this.progressBar1.Margin = new System.Windows.Forms.Padding(12, 13, 0, 0);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(180, 20);
            this.progressBar1.TabIndex = 9;
            this.progressBar1.Visible = false;
            // 
            // flow_toolbar
            // 
            this.flow_toolbar.Controls.Add(this.group_people);
            this.flow_toolbar.Controls.Add(this.separator_people);
            this.flow_toolbar.Controls.Add(this.group_data);
            this.flow_toolbar.Controls.Add(this.separator_data);
            this.flow_toolbar.Controls.Add(this.group_rota);
            this.flow_toolbar.Controls.Add(this.separator_rota);
            this.flow_toolbar.Controls.Add(this.group_export);
            this.flow_toolbar.Controls.Add(this.separator_export);
            this.flow_toolbar.Controls.Add(this.group_search);
            this.flow_toolbar.Dock = System.Windows.Forms.DockStyle.Top;
            this.flow_toolbar.Name = "flow_toolbar";
            this.flow_toolbar.Size = new System.Drawing.Size(1254, 64);
            this.flow_toolbar.Resize += new System.EventHandler(this.flow_toolbar_Resize);
            this.flow_toolbar.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.flow_toolbar.TabIndex = 1;
            // 
            // group_people
            // 
            this.group_people.AutoSize = true;
            this.group_people.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.group_people.Controls.Add(this.caption_people);
            this.group_people.Controls.Add(this.flow_people);
            this.group_people.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.group_people.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
            this.group_people.Name = "group_people";
            this.group_people.TabIndex = 0;
            this.group_people.WrapContents = false;
            // 
            // caption_people
            // 
            this.caption_people.AutoSize = true;
            this.caption_people.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.caption_people.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(116)))), ((int)(((byte)(139)))));
            this.caption_people.Margin = new System.Windows.Forms.Padding(1, 0, 0, 4);
            this.caption_people.Name = "caption_people";
            this.caption_people.Text = "PEOPLE";
            // 
            // flow_people
            // 
            this.flow_people.AutoSize = true;
            this.flow_people.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.flow_people.Controls.Add(this.btn_continue);
            this.flow_people.Controls.Add(this.btn_batch);
            this.flow_people.Controls.Add(this.rjButton1);
            this.flow_people.Margin = new System.Windows.Forms.Padding(0);
            this.flow_people.Name = "flow_people";
            this.flow_people.WrapContents = false;
            // 
            // btn_continue
            // 
            this.btn_continue.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.btn_continue.BackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.btn_continue.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(203)))), ((int)(((byte)(213)))), ((int)(((byte)(225)))));
            this.btn_continue.BorderRadius = 6;
            this.btn_continue.BorderSize = 1;
            this.btn_continue.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btn_continue.FlatAppearance.BorderSize = 0;
            this.btn_continue.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(226)))), ((int)(((byte)(232)))), ((int)(((byte)(240)))));
            this.btn_continue.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(241)))), ((int)(((byte)(245)))), ((int)(((byte)(249)))));
            this.btn_continue.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_continue.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_continue.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(15)))), ((int)(((byte)(23)))), ((int)(((byte)(42)))));
            this.btn_continue.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
            this.btn_continue.Name = "btn_continue";
            this.btn_continue.Size = new System.Drawing.Size(112, 34);
            this.btn_continue.TabIndex = 0;
            this.btn_continue.Text = "+ Add Person";
            this.btn_continue.TextColor = System.Drawing.Color.FromArgb(((int)(((byte)(15)))), ((int)(((byte)(23)))), ((int)(((byte)(42)))));
            this.toolTip1.SetToolTip(this.btn_continue, "Add one person with leave and preferred dates");
            this.btn_continue.UseVisualStyleBackColor = false;
            this.btn_continue.Click += new System.EventHandler(this.btn_continue_Click);
            // 
            // btn_batch
            // 
            this.btn_batch.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.btn_batch.BackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.btn_batch.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(203)))), ((int)(((byte)(213)))), ((int)(((byte)(225)))));
            this.btn_batch.BorderRadius = 6;
            this.btn_batch.BorderSize = 1;
            this.btn_batch.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btn_batch.FlatAppearance.BorderSize = 0;
            this.btn_batch.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(226)))), ((int)(((byte)(232)))), ((int)(((byte)(240)))));
            this.btn_batch.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(241)))), ((int)(((byte)(245)))), ((int)(((byte)(249)))));
            this.btn_batch.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_batch.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_batch.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(15)))), ((int)(((byte)(23)))), ((int)(((byte)(42)))));
            this.btn_batch.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
            this.btn_batch.Name = "btn_batch";
            this.btn_batch.Size = new System.Drawing.Size(92, 34);
            this.btn_batch.TabIndex = 1;
            this.btn_batch.Text = "Add Many";
            this.btn_batch.TextColor = System.Drawing.Color.FromArgb(((int)(((byte)(15)))), ((int)(((byte)(23)))), ((int)(((byte)(42)))));
            this.toolTip1.SetToolTip(this.btn_batch, "Paste a list of names, one per line");
            this.btn_batch.UseVisualStyleBackColor = false;
            this.btn_batch.Click += new System.EventHandler(this.btn_batch_Click);
            // 
            // rjButton1
            // 
            this.rjButton1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.rjButton1.BackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.rjButton1.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(252)))), ((int)(((byte)(165)))), ((int)(((byte)(165)))));
            this.rjButton1.BorderRadius = 6;
            this.rjButton1.BorderSize = 1;
            this.rjButton1.Cursor = System.Windows.Forms.Cursors.Hand;
            this.rjButton1.FlatAppearance.BorderSize = 0;
            this.rjButton1.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(254)))), ((int)(((byte)(226)))), ((int)(((byte)(226)))));
            this.rjButton1.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(254)))), ((int)(((byte)(242)))), ((int)(((byte)(242)))));
            this.rjButton1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rjButton1.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.rjButton1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(185)))), ((int)(((byte)(28)))), ((int)(((byte)(28)))));
            this.rjButton1.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
            this.rjButton1.Name = "rjButton1";
            this.rjButton1.Size = new System.Drawing.Size(76, 34);
            this.rjButton1.TabIndex = 2;
            this.rjButton1.Text = "Delete";
            this.rjButton1.TextColor = System.Drawing.Color.FromArgb(((int)(((byte)(185)))), ((int)(((byte)(28)))), ((int)(((byte)(28)))));
            this.toolTip1.SetToolTip(this.rjButton1, "Remove the selected people from the rota");
            this.rjButton1.UseVisualStyleBackColor = false;
            this.rjButton1.Click += new System.EventHandler(this.rjButton1_Click);
            // 
            // separator_people
            // 
            this.separator_people.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(226)))), ((int)(((byte)(232)))), ((int)(((byte)(240)))));
            this.separator_people.Margin = new System.Windows.Forms.Padding(8, 6, 14, 4);
            this.separator_people.Name = "separator_people";
            this.separator_people.Size = new System.Drawing.Size(1, 50);
            // 
            // group_data
            // 
            this.group_data.AutoSize = true;
            this.group_data.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.group_data.Controls.Add(this.caption_data);
            this.group_data.Controls.Add(this.flow_data);
            this.group_data.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.group_data.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
            this.group_data.Name = "group_data";
            this.group_data.TabIndex = 1;
            this.group_data.WrapContents = false;
            // 
            // caption_data
            // 
            this.caption_data.AutoSize = true;
            this.caption_data.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.caption_data.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(116)))), ((int)(((byte)(139)))));
            this.caption_data.Margin = new System.Windows.Forms.Padding(1, 0, 0, 4);
            this.caption_data.Name = "caption_data";
            this.caption_data.Text = "DATA";
            // 
            // flow_data
            // 
            this.flow_data.AutoSize = true;
            this.flow_data.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.flow_data.Controls.Add(this.btn_import_leave);
            this.flow_data.Controls.Add(this.btn_import_history);
            this.flow_data.Controls.Add(this.btn_holidays);
            this.flow_data.Controls.Add(this.btn_date);
            this.flow_data.Controls.Add(this.btn_clear);
            this.flow_data.Margin = new System.Windows.Forms.Padding(0);
            this.flow_data.Name = "flow_data";
            this.flow_data.WrapContents = false;
            // 
            // btn_import_leave
            // 
            this.btn_import_leave.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.btn_import_leave.BackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.btn_import_leave.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(203)))), ((int)(((byte)(213)))), ((int)(((byte)(225)))));
            this.btn_import_leave.BorderRadius = 6;
            this.btn_import_leave.BorderSize = 1;
            this.btn_import_leave.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btn_import_leave.FlatAppearance.BorderSize = 0;
            this.btn_import_leave.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(226)))), ((int)(((byte)(232)))), ((int)(((byte)(240)))));
            this.btn_import_leave.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(241)))), ((int)(((byte)(245)))), ((int)(((byte)(249)))));
            this.btn_import_leave.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_import_leave.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_import_leave.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(15)))), ((int)(((byte)(23)))), ((int)(((byte)(42)))));
            this.btn_import_leave.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
            this.btn_import_leave.Name = "btn_import_leave";
            this.btn_import_leave.Size = new System.Drawing.Size(112, 34);
            this.btn_import_leave.TabIndex = 0;
            this.btn_import_leave.Text = "Import Leave";
            this.btn_import_leave.TextColor = System.Drawing.Color.FromArgb(((int)(((byte)(15)))), ((int)(((byte)(23)))), ((int)(((byte)(42)))));
            this.toolTip1.SetToolTip(this.btn_import_leave, "Import the monthly leave Excel sheet (review before applying)");
            this.btn_import_leave.UseVisualStyleBackColor = false;
            this.btn_import_leave.Click += new System.EventHandler(this.btn_import_leave_Click);
            // 
            // btn_import_history
            // 
            this.btn_import_history.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.btn_import_history.BackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.btn_import_history.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(203)))), ((int)(((byte)(213)))), ((int)(((byte)(225)))));
            this.btn_import_history.BorderRadius = 6;
            this.btn_import_history.BorderSize = 1;
            this.btn_import_history.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btn_import_history.FlatAppearance.BorderSize = 0;
            this.btn_import_history.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(226)))), ((int)(((byte)(232)))), ((int)(((byte)(240)))));
            this.btn_import_history.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(241)))), ((int)(((byte)(245)))), ((int)(((byte)(249)))));
            this.btn_import_history.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_import_history.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_import_history.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(15)))), ((int)(((byte)(23)))), ((int)(((byte)(42)))));
            this.btn_import_history.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
            this.btn_import_history.Name = "btn_import_history";
            this.btn_import_history.Size = new System.Drawing.Size(160, 34);
            this.btn_import_history.TabIndex = 1;
            this.btn_import_history.Text = "Import Wkend History";
            this.btn_import_history.TextColor = System.Drawing.Color.FromArgb(((int)(((byte)(15)))), ((int)(((byte)(23)))), ((int)(((byte)(42)))));
            this.toolTip1.SetToolTip(this.btn_import_history, "Load past weekend/public holiday totals so weekends are balanced");
            this.btn_import_history.UseVisualStyleBackColor = false;
            this.btn_import_history.Click += new System.EventHandler(this.btn_import_history_Click);
            // 
            // btn_holidays
            // 
            this.btn_holidays.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.btn_holidays.BackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.btn_holidays.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(203)))), ((int)(((byte)(213)))), ((int)(((byte)(225)))));
            this.btn_holidays.BorderRadius = 6;
            this.btn_holidays.BorderSize = 1;
            this.btn_holidays.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btn_holidays.FlatAppearance.BorderSize = 0;
            this.btn_holidays.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(226)))), ((int)(((byte)(232)))), ((int)(((byte)(240)))));
            this.btn_holidays.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(241)))), ((int)(((byte)(245)))), ((int)(((byte)(249)))));
            this.btn_holidays.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_holidays.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_holidays.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(15)))), ((int)(((byte)(23)))), ((int)(((byte)(42)))));
            this.btn_holidays.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
            this.btn_holidays.Name = "btn_holidays";
            this.btn_holidays.Size = new System.Drawing.Size(124, 34);
            this.btn_holidays.TabIndex = 2;
            this.btn_holidays.Text = "Public Holidays";
            this.btn_holidays.TextColor = System.Drawing.Color.FromArgb(((int)(((byte)(15)))), ((int)(((byte)(23)))), ((int)(((byte)(42)))));
            this.toolTip1.SetToolTip(this.btn_holidays, "Add or remove public holidays in this rota");
            this.btn_holidays.UseVisualStyleBackColor = false;
            this.btn_holidays.Click += new System.EventHandler(this.btn_holidays_Click);
            // 
            // btn_date
            // 
            this.btn_date.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.btn_date.BackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.btn_date.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(203)))), ((int)(((byte)(213)))), ((int)(((byte)(225)))));
            this.btn_date.BorderRadius = 6;
            this.btn_date.BorderSize = 1;
            this.btn_date.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btn_date.FlatAppearance.BorderSize = 0;
            this.btn_date.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(226)))), ((int)(((byte)(232)))), ((int)(((byte)(240)))));
            this.btn_date.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(241)))), ((int)(((byte)(245)))), ((int)(((byte)(249)))));
            this.btn_date.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_date.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_date.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(15)))), ((int)(((byte)(23)))), ((int)(((byte)(42)))));
            this.btn_date.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
            this.btn_date.Name = "btn_date";
            this.btn_date.Size = new System.Drawing.Size(96, 34);
            this.btn_date.TabIndex = 3;
            this.btn_date.Text = "Rota Dates";
            this.btn_date.TextColor = System.Drawing.Color.FromArgb(((int)(((byte)(15)))), ((int)(((byte)(23)))), ((int)(((byte)(42)))));
            this.toolTip1.SetToolTip(this.btn_date, "Change the rota start and end dates");
            this.btn_date.UseVisualStyleBackColor = false;
            this.btn_date.Click += new System.EventHandler(this.btn_date_Click);
            // 
            // btn_clear
            // 
            this.btn_clear.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.btn_clear.BackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.btn_clear.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(252)))), ((int)(((byte)(165)))), ((int)(((byte)(165)))));
            this.btn_clear.BorderRadius = 6;
            this.btn_clear.BorderSize = 1;
            this.btn_clear.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btn_clear.FlatAppearance.BorderSize = 0;
            this.btn_clear.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(254)))), ((int)(((byte)(226)))), ((int)(((byte)(226)))));
            this.btn_clear.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(254)))), ((int)(((byte)(242)))), ((int)(((byte)(242)))));
            this.btn_clear.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_clear.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_clear.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(185)))), ((int)(((byte)(28)))), ((int)(((byte)(28)))));
            this.btn_clear.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
            this.btn_clear.Name = "btn_clear";
            this.btn_clear.Size = new System.Drawing.Size(100, 34);
            this.btn_clear.TabIndex = 4;
            this.btn_clear.Text = "Clear Dates";
            this.btn_clear.TextColor = System.Drawing.Color.FromArgb(((int)(((byte)(185)))), ((int)(((byte)(28)))), ((int)(((byte)(28)))));
            this.toolTip1.SetToolTip(this.btn_clear, "Clear everyone\u2019s leave and preferred dates, and all public holidays");
            this.btn_clear.UseVisualStyleBackColor = false;
            this.btn_clear.Click += new System.EventHandler(this.btn_clear_Click);
            // 
            // separator_data
            // 
            this.separator_data.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(226)))), ((int)(((byte)(232)))), ((int)(((byte)(240)))));
            this.separator_data.Margin = new System.Windows.Forms.Padding(8, 6, 14, 4);
            this.separator_data.Name = "separator_data";
            this.separator_data.Size = new System.Drawing.Size(1, 50);
            // 
            // group_rota
            // 
            this.group_rota.AutoSize = true;
            this.group_rota.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.group_rota.Controls.Add(this.caption_rota);
            this.group_rota.Controls.Add(this.flow_rota);
            this.group_rota.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.group_rota.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
            this.group_rota.Name = "group_rota";
            this.group_rota.TabIndex = 2;
            this.group_rota.WrapContents = false;
            // 
            // caption_rota
            // 
            this.caption_rota.AutoSize = true;
            this.caption_rota.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.caption_rota.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(116)))), ((int)(((byte)(139)))));
            this.caption_rota.Margin = new System.Windows.Forms.Padding(1, 0, 0, 4);
            this.caption_rota.Name = "caption_rota";
            this.caption_rota.Text = "ROTA";
            // 
            // flow_rota
            // 
            this.flow_rota.AutoSize = true;
            this.flow_rota.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.flow_rota.Controls.Add(this.btn_assign);
            this.flow_rota.Controls.Add(this.btn_reset);
            this.flow_rota.Margin = new System.Windows.Forms.Padding(0);
            this.flow_rota.Name = "flow_rota";
            this.flow_rota.WrapContents = false;
            // 
            // btn_assign
            // 
            this.btn_assign.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(37)))), ((int)(((byte)(99)))), ((int)(((byte)(235)))));
            this.btn_assign.BackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(37)))), ((int)(((byte)(99)))), ((int)(((byte)(235)))));
            this.btn_assign.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(37)))), ((int)(((byte)(99)))), ((int)(((byte)(235)))));
            this.btn_assign.BorderRadius = 6;
            this.btn_assign.BorderSize = 0;
            this.btn_assign.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btn_assign.FlatAppearance.BorderSize = 0;
            this.btn_assign.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(64)))), ((int)(((byte)(175)))));
            this.btn_assign.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(29)))), ((int)(((byte)(78)))), ((int)(((byte)(216)))));
            this.btn_assign.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_assign.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_assign.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.btn_assign.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
            this.btn_assign.Name = "btn_assign";
            this.btn_assign.Size = new System.Drawing.Size(128, 34);
            this.btn_assign.TabIndex = 0;
            this.btn_assign.Text = "Assign Shifts";
            this.btn_assign.TextColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.toolTip1.SetToolTip(this.btn_assign, "Generate the on-call rota");
            this.btn_assign.UseVisualStyleBackColor = false;
            this.btn_assign.Click += new System.EventHandler(this.rjButton2_Click);
            // 
            // btn_reset
            // 
            this.btn_reset.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.btn_reset.BackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.btn_reset.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(203)))), ((int)(((byte)(213)))), ((int)(((byte)(225)))));
            this.btn_reset.BorderRadius = 6;
            this.btn_reset.BorderSize = 1;
            this.btn_reset.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btn_reset.FlatAppearance.BorderSize = 0;
            this.btn_reset.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(226)))), ((int)(((byte)(232)))), ((int)(((byte)(240)))));
            this.btn_reset.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(241)))), ((int)(((byte)(245)))), ((int)(((byte)(249)))));
            this.btn_reset.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_reset.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_reset.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(15)))), ((int)(((byte)(23)))), ((int)(((byte)(42)))));
            this.btn_reset.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
            this.btn_reset.Name = "btn_reset";
            this.btn_reset.Size = new System.Drawing.Size(72, 34);
            this.btn_reset.TabIndex = 1;
            this.btn_reset.Text = "Reset";
            this.btn_reset.TextColor = System.Drawing.Color.FromArgb(((int)(((byte)(15)))), ((int)(((byte)(23)))), ((int)(((byte)(42)))));
            this.toolTip1.SetToolTip(this.btn_reset, "Remove all assigned shifts");
            this.btn_reset.UseVisualStyleBackColor = false;
            this.btn_reset.Click += new System.EventHandler(this.btn_reset_Click);
            // 
            // separator_rota
            // 
            this.separator_rota.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(226)))), ((int)(((byte)(232)))), ((int)(((byte)(240)))));
            this.separator_rota.Margin = new System.Windows.Forms.Padding(8, 6, 14, 4);
            this.separator_rota.Name = "separator_rota";
            this.separator_rota.Size = new System.Drawing.Size(1, 50);
            // 
            // group_export
            // 
            this.group_export.AutoSize = true;
            this.group_export.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.group_export.Controls.Add(this.caption_export);
            this.group_export.Controls.Add(this.flow_export);
            this.group_export.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.group_export.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
            this.group_export.Name = "group_export";
            this.group_export.TabIndex = 3;
            this.group_export.WrapContents = false;
            // 
            // caption_export
            // 
            this.caption_export.AutoSize = true;
            this.caption_export.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.caption_export.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(116)))), ((int)(((byte)(139)))));
            this.caption_export.Margin = new System.Windows.Forms.Padding(1, 0, 0, 4);
            this.caption_export.Name = "caption_export";
            this.caption_export.Text = "EXPORT";
            // 
            // flow_export
            // 
            this.flow_export.AutoSize = true;
            this.flow_export.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.flow_export.Controls.Add(this.btn_download);
            this.flow_export.Controls.Add(this.rjButton2);
            this.flow_export.Controls.Add(this.btn_export_history);
            this.flow_export.Margin = new System.Windows.Forms.Padding(0);
            this.flow_export.Name = "flow_export";
            this.flow_export.WrapContents = false;
            // 
            // btn_download
            // 
            this.btn_download.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(22)))), ((int)(((byte)(163)))), ((int)(((byte)(74)))));
            this.btn_download.BackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(22)))), ((int)(((byte)(163)))), ((int)(((byte)(74)))));
            this.btn_download.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(22)))), ((int)(((byte)(163)))), ((int)(((byte)(74)))));
            this.btn_download.BorderRadius = 6;
            this.btn_download.BorderSize = 0;
            this.btn_download.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btn_download.FlatAppearance.BorderSize = 0;
            this.btn_download.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(22)))), ((int)(((byte)(101)))), ((int)(((byte)(52)))));
            this.btn_download.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(21)))), ((int)(((byte)(128)))), ((int)(((byte)(61)))));
            this.btn_download.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_download.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_download.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.btn_download.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
            this.btn_download.Name = "btn_download";
            this.btn_download.Size = new System.Drawing.Size(142, 34);
            this.btn_download.TabIndex = 0;
            this.btn_download.Text = "Timetable (Excel)";
            this.btn_download.TextColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.toolTip1.SetToolTip(this.btn_download, "Save the rota as an Excel timetable");
            this.btn_download.UseVisualStyleBackColor = false;
            this.btn_download.Click += new System.EventHandler(this.btn_download_Click);
            // 
            // rjButton2
            // 
            this.rjButton2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.rjButton2.BackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.rjButton2.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(203)))), ((int)(((byte)(213)))), ((int)(((byte)(225)))));
            this.rjButton2.BorderRadius = 6;
            this.rjButton2.BorderSize = 1;
            this.rjButton2.Cursor = System.Windows.Forms.Cursors.Hand;
            this.rjButton2.FlatAppearance.BorderSize = 0;
            this.rjButton2.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(226)))), ((int)(((byte)(232)))), ((int)(((byte)(240)))));
            this.rjButton2.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(241)))), ((int)(((byte)(245)))), ((int)(((byte)(249)))));
            this.rjButton2.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rjButton2.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.rjButton2.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(15)))), ((int)(((byte)(23)))), ((int)(((byte)(42)))));
            this.rjButton2.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
            this.rjButton2.Name = "rjButton2";
            this.rjButton2.Size = new System.Drawing.Size(136, 34);
            this.rjButton2.TabIndex = 1;
            this.rjButton2.Text = "Save Data (JSON)";
            this.rjButton2.TextColor = System.Drawing.Color.FromArgb(((int)(((byte)(15)))), ((int)(((byte)(23)))), ((int)(((byte)(42)))));
            this.toolTip1.SetToolTip(this.rjButton2, "Save people, leave and holidays to a JSON file to load later");
            this.rjButton2.UseVisualStyleBackColor = false;
            this.rjButton2.Click += new System.EventHandler(this.rjButton2_Click_1);
            // 
            // btn_export_history
            // 
            this.btn_export_history.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.btn_export_history.BackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.btn_export_history.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(203)))), ((int)(((byte)(213)))), ((int)(((byte)(225)))));
            this.btn_export_history.BorderRadius = 6;
            this.btn_export_history.BorderSize = 1;
            this.btn_export_history.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btn_export_history.FlatAppearance.BorderSize = 0;
            this.btn_export_history.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(226)))), ((int)(((byte)(232)))), ((int)(((byte)(240)))));
            this.btn_export_history.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(241)))), ((int)(((byte)(245)))), ((int)(((byte)(249)))));
            this.btn_export_history.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_export_history.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btn_export_history.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(15)))), ((int)(((byte)(23)))), ((int)(((byte)(42)))));
            this.btn_export_history.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
            this.btn_export_history.Name = "btn_export_history";
            this.btn_export_history.Size = new System.Drawing.Size(118, 34);
            this.btn_export_history.TabIndex = 2;
            this.btn_export_history.Text = "Wkend History";
            this.btn_export_history.TextColor = System.Drawing.Color.FromArgb(((int)(((byte)(15)))), ((int)(((byte)(23)))), ((int)(((byte)(42)))));
            this.toolTip1.SetToolTip(this.btn_export_history, "Save the weekend history sheet updated with this rota");
            this.btn_export_history.UseVisualStyleBackColor = false;
            this.btn_export_history.Click += new System.EventHandler(this.btn_export_history_Click);
            // 
            // separator_export
            // 
            this.separator_export.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(226)))), ((int)(((byte)(232)))), ((int)(((byte)(240)))));
            this.separator_export.Margin = new System.Windows.Forms.Padding(8, 6, 14, 4);
            this.separator_export.Name = "separator_export";
            this.separator_export.Size = new System.Drawing.Size(1, 50);
            // 
            // group_search
            // 
            this.group_search.AutoSize = true;
            this.group_search.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.group_search.Controls.Add(this.caption_search);
            this.group_search.Controls.Add(this.textBox1);
            this.group_search.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.group_search.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
            this.group_search.Name = "group_search";
            this.group_search.TabIndex = 4;
            this.group_search.WrapContents = false;
            // 
            // caption_search
            // 
            this.caption_search.AutoSize = true;
            this.caption_search.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.caption_search.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(116)))), ((int)(((byte)(139)))));
            this.caption_search.Margin = new System.Windows.Forms.Padding(1, 0, 0, 4);
            this.caption_search.Name = "caption_search";
            this.caption_search.Text = "SEARCH";
            // 
            // textBox1
            // 
            this.textBox1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.textBox1.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBox1.Margin = new System.Windows.Forms.Padding(0);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(240, 29);
            this.textBox1.TabIndex = 0;
            this.toolTip1.SetToolTip(this.textBox1, "Type part of a name to highlight matching rows");
            this.textBox1.TextChanged += new System.EventHandler(this.textBox1_TextChanged);
            this.textBox1.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.textBox1_KeyPress);
            // 
            // panel_grid
            // 
            this.panel_grid.Controls.Add(this.dataGridView1);
            this.panel_grid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel_grid.Name = "panel_grid";
            this.panel_grid.Padding = new System.Windows.Forms.Padding(12, 10, 12, 12);
            this.panel_grid.TabIndex = 2;
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.AlternatingRowsDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(250)))), ((int)(((byte)(252)))));
            this.dataGridView1.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells;
            this.dataGridView1.BackgroundColor = System.Drawing.Color.White;
            this.dataGridView1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.dataGridView1.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.Single;
            this.dataGridView1.ColumnHeadersDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(241)))), ((int)(((byte)(245)))), ((int)(((byte)(249)))));
            this.dataGridView1.ColumnHeadersDefaultCellStyle.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.dataGridView1.ColumnHeadersDefaultCellStyle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(65)))), ((int)(((byte)(85)))));
            this.dataGridView1.ColumnHeadersDefaultCellStyle.Padding = new System.Windows.Forms.Padding(2, 4, 2, 4);
            this.dataGridView1.ColumnHeadersDefaultCellStyle.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.PersonName,
            this.weekend,
            this.ExtraShift,
            this.preferred_date,
            this.LeaveDatesString,
            this.TotalLeaveDays,
            this.AssignedShiftsString,
            this.LeaveDates,
            this.AssignedShifts,
            this.WeekdayShifts,
            this.WeekendShifts,
            this.PriorWeekendShifts,
            this.TotalShift});
            this.dataGridView1.DefaultCellStyle.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.dataGridView1.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(191)))), ((int)(((byte)(219)))), ((int)(((byte)(254)))));
            this.dataGridView1.DefaultCellStyle.SelectionForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(15)))), ((int)(((byte)(23)))), ((int)(((byte)(42)))));
            this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView1.EnableHeadersVisualStyles = false;
            this.dataGridView1.GridColor = System.Drawing.Color.FromArgb(((int)(((byte)(226)))), ((int)(((byte)(232)))), ((int)(((byte)(240)))));
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.RowHeadersWidth = 28;
            this.dataGridView1.TabIndex = 0;
            this.dataGridView1.CellMouseDoubleClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.dataGridView1_CellMouseDoubleClick);
            this.dataGridView1.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView1_CellValueChanged);
            this.dataGridView1.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.dataGridView1_CellFormatting);
            this.dataGridView1.CellParsing += new System.Windows.Forms.DataGridViewCellParsingEventHandler(this.dataGridView1_CellParsing);
            this.dataGridView1.CellValidating += new System.Windows.Forms.DataGridViewCellValidatingEventHandler(this.dataGridView1_CellValidating);
            this.dataGridView1.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView1_CellEndEdit);
            this.dataGridView1.CurrentCellDirtyStateChanged += new System.EventHandler(this.dataGridView1_CurrentCellDirtyStateChanged);
            this.dataGridView1.DataError += new System.Windows.Forms.DataGridViewDataErrorEventHandler(this.dataGridView1_DataError);
            this.dataGridView1.DataBindingComplete += new System.Windows.Forms.DataGridViewBindingCompleteEventHandler(this.dataGridView1_DataBindingComplete);
            this.dataGridView1.Paint += new System.Windows.Forms.PaintEventHandler(this.dataGridView1_Paint);
            // 
            // PersonName
            // 
            this.PersonName.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.PersonName.DefaultCellStyle.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.PersonName.FillWeight = 22F;
            this.PersonName.HeaderText = "Name";
            this.PersonName.MinimumWidth = 180;
            this.PersonName.Name = "PersonName";
            // 
            // weekend
            // 
            this.weekend.HeaderText = "Wkend Pref";
            this.weekend.Name = "weekend";
            this.weekend.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.ColumnHeader;
            // 
            // ExtraShift
            // 
            this.ExtraShift.HeaderText = "Extra";
            this.ExtraShift.Name = "ExtraShift";
            this.ExtraShift.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.ColumnHeader;
            // 
            // preferred_date
            // 
            this.preferred_date.HeaderText = "preferred_date";
            this.preferred_date.Name = "preferred_date";
            this.preferred_date.ReadOnly = true;
            this.preferred_date.Visible = false;
            // 
            // LeaveDatesString
            // 
            this.LeaveDatesString.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.LeaveDatesString.DefaultCellStyle.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.LeaveDatesString.FillWeight = 60F;
            this.LeaveDatesString.HeaderText = "Leave Dates (d/m)";
            this.LeaveDatesString.MinimumWidth = 200;
            this.LeaveDatesString.Name = "LeaveDatesString";
            // 
            // TotalLeaveDays
            // 
            this.TotalLeaveDays.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.ColumnHeader;
            this.TotalLeaveDays.HeaderText = "Leave Days";
            this.TotalLeaveDays.Name = "TotalLeaveDays";
            this.TotalLeaveDays.ReadOnly = true;
            // 
            // AssignedShiftsString
            // 
            this.AssignedShiftsString.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.AssignedShiftsString.DefaultCellStyle.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.AssignedShiftsString.FillWeight = 18F;
            this.AssignedShiftsString.HeaderText = "Assigned Shifts";
            this.AssignedShiftsString.MinimumWidth = 120;
            this.AssignedShiftsString.Name = "AssignedShiftsString";
            this.AssignedShiftsString.ReadOnly = true;
            // 
            // LeaveDates
            // 
            this.LeaveDates.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.ColumnHeader;
            this.LeaveDates.HeaderText = "LeaveDates";
            this.LeaveDates.Name = "LeaveDates";
            this.LeaveDates.ReadOnly = true;
            this.LeaveDates.Visible = false;
            // 
            // AssignedShifts
            // 
            this.AssignedShifts.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.ColumnHeader;
            this.AssignedShifts.HeaderText = "AssignedShifts";
            this.AssignedShifts.Name = "AssignedShifts";
            this.AssignedShifts.ReadOnly = true;
            this.AssignedShifts.Visible = false;
            // 
            // WeekdayShifts
            // 
            this.WeekdayShifts.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.ColumnHeader;
            this.WeekdayShifts.HeaderText = "Weekday";
            this.WeekdayShifts.Name = "WeekdayShifts";
            this.WeekdayShifts.ReadOnly = true;
            this.WeekdayShifts.Width = 78;
            // 
            // WeekendShifts
            // 
            this.WeekendShifts.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.ColumnHeader;
            this.WeekendShifts.HeaderText = "Weekend";
            this.WeekendShifts.Name = "WeekendShifts";
            this.WeekendShifts.ReadOnly = true;
            this.WeekendShifts.Width = 79;
            // 
            // PriorWeekendShifts
            // 
            this.PriorWeekendShifts.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.ColumnHeader;
            this.PriorWeekendShifts.HeaderText = "Prior Wkend";
            this.PriorWeekendShifts.Name = "PriorWeekendShifts";
            this.PriorWeekendShifts.Width = 90;
            // 
            // TotalShift
            // 
            this.TotalShift.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.ColumnHeader;
            this.TotalShift.HeaderText = "Shift Count";
            this.TotalShift.Name = "TotalShift";
            this.TotalShift.ReadOnly = true;
            this.TotalShift.Width = 78;
            // 
            // frmAddPerson
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1280, 800);
            this.Controls.Add(this.panel1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "frmAddPerson";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "frmAddPerson";
            this.panel_grid.ResumeLayout(false);
            this.panel_grid.PerformLayout();
            this.group_search.ResumeLayout(false);
            this.group_search.PerformLayout();
            this.flow_export.ResumeLayout(false);
            this.flow_export.PerformLayout();
            this.group_export.ResumeLayout(false);
            this.group_export.PerformLayout();
            this.flow_rota.ResumeLayout(false);
            this.flow_rota.PerformLayout();
            this.group_rota.ResumeLayout(false);
            this.group_rota.PerformLayout();
            this.flow_data.ResumeLayout(false);
            this.flow_data.PerformLayout();
            this.group_data.ResumeLayout(false);
            this.group_data.PerformLayout();
            this.flow_people.ResumeLayout(false);
            this.flow_people.PerformLayout();
            this.group_people.ResumeLayout(false);
            this.group_people.PerformLayout();
            this.flow_toolbar.ResumeLayout(false);
            this.flow_toolbar.PerformLayout();
            this.flow_stats.ResumeLayout(false);
            this.flow_stats.PerformLayout();
            this.panel_info.ResumeLayout(false);
            this.panel_info.PerformLayout();
            this.panel_header.ResumeLayout(false);
            this.panel_header.PerformLayout();
            this.panel_titlebar.ResumeLayout(false);
            this.panel_titlebar.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Panel panel_titlebar;
        private System.Windows.Forms.Label label4;
        private Button_Control.TitleBar titleBar1;
        private System.Windows.Forms.Panel panel_header;
        private System.Windows.Forms.Panel panel_info;
        private System.Windows.Forms.Label label_date;
        private System.Windows.Forms.FlowLayoutPanel flow_stats;
        private System.Windows.Forms.Label label_total;
        private System.Windows.Forms.Label label_holidays;
        private System.Windows.Forms.Label label_shift_total;
        private System.Windows.Forms.Label label_shift_assign;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.FlowLayoutPanel flow_toolbar;
        private System.Windows.Forms.FlowLayoutPanel group_people;
        private System.Windows.Forms.Label caption_people;
        private System.Windows.Forms.FlowLayoutPanel flow_people;
        private CustomControls.RJControls.RJButton btn_continue;
        private CustomControls.RJControls.RJButton btn_batch;
        private CustomControls.RJControls.RJButton rjButton1;
        private System.Windows.Forms.Panel separator_people;
        private System.Windows.Forms.FlowLayoutPanel group_data;
        private System.Windows.Forms.Label caption_data;
        private System.Windows.Forms.FlowLayoutPanel flow_data;
        private CustomControls.RJControls.RJButton btn_import_leave;
        private CustomControls.RJControls.RJButton btn_import_history;
        private CustomControls.RJControls.RJButton btn_holidays;
        private CustomControls.RJControls.RJButton btn_date;
        private CustomControls.RJControls.RJButton btn_clear;
        private System.Windows.Forms.Panel separator_data;
        private System.Windows.Forms.FlowLayoutPanel group_rota;
        private System.Windows.Forms.Label caption_rota;
        private System.Windows.Forms.FlowLayoutPanel flow_rota;
        private CustomControls.RJControls.RJButton btn_assign;
        private CustomControls.RJControls.RJButton btn_reset;
        private System.Windows.Forms.Panel separator_rota;
        private System.Windows.Forms.FlowLayoutPanel group_export;
        private System.Windows.Forms.Label caption_export;
        private System.Windows.Forms.FlowLayoutPanel flow_export;
        private CustomControls.RJControls.RJButton btn_download;
        private CustomControls.RJControls.RJButton rjButton2;
        private CustomControls.RJControls.RJButton btn_export_history;
        private System.Windows.Forms.Panel separator_export;
        private System.Windows.Forms.FlowLayoutPanel group_search;
        private System.Windows.Forms.Label caption_search;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Panel panel_grid;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.DataGridViewTextBoxColumn PersonName;
        private System.Windows.Forms.DataGridViewCheckBoxColumn weekend;
        private System.Windows.Forms.DataGridViewCheckBoxColumn ExtraShift;
        private System.Windows.Forms.DataGridViewCheckBoxColumn preferred_date;
        private System.Windows.Forms.DataGridViewTextBoxColumn LeaveDatesString;
        private System.Windows.Forms.DataGridViewTextBoxColumn TotalLeaveDays;
        private System.Windows.Forms.DataGridViewTextBoxColumn AssignedShiftsString;
        private System.Windows.Forms.DataGridViewTextBoxColumn LeaveDates;
        private System.Windows.Forms.DataGridViewTextBoxColumn AssignedShifts;
        private System.Windows.Forms.DataGridViewTextBoxColumn WeekdayShifts;
        private System.Windows.Forms.DataGridViewTextBoxColumn WeekendShifts;
        private System.Windows.Forms.DataGridViewTextBoxColumn PriorWeekendShifts;
        private System.Windows.Forms.DataGridViewTextBoxColumn TotalShift;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}
