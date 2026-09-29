using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace IMS_Project.Class.Styles
{
    public class TitleBarPanelStyler
    {
        public static void ApplyTitleBarPanelStyle(Panel panel, Form form)
        {
            // Add event handlers to the panel itself
            panel.MouseDown += (sender, e) => Panel_MouseDown(sender, e);
            panel.MouseMove += (sender, e) => Panel_MouseMove(sender, e, form);
            panel.MouseUp += (sender, e) => Panel_MouseUp(sender, e);

            panel.BorderStyle = BorderStyle.FixedSingle;
            // Add event handlers to all child controls within the panel
            foreach (Control control in panel.Controls)
            {
                control.MouseDown += (sender, e) => Panel_MouseDown(sender, e);
                control.MouseMove += (sender, e) => Panel_MouseMove(sender, e, form);
                control.MouseUp += (sender, e) => Panel_MouseUp(sender, e);
            }
        }

        /// <summary>
        /// Opens the borderless form maximised to the working area (taskbar stays visible) and lets
        /// the user double-click the title bar to switch between maximised and a normal size.
        /// </summary>
        public static void EnableMaximize(Panel titleBar, Form form, Size normalSize)
        {
            // Borderless forms cover the taskbar with WindowState.Maximized, so size to the
            // screen's working area directly instead.
            Rectangle normalBounds = Rectangle.Empty;

            Action maximize = () =>
            {
                normalBounds = form.Bounds;
                form.Bounds = Screen.FromControl(form).WorkingArea;
                maximizedForms.Add(form);
            };

            form.Load += (s, e) =>
            {
                var area = Screen.FromControl(form).WorkingArea;
                form.Size = new Size(Math.Min(normalSize.Width, area.Width), Math.Min(normalSize.Height, area.Height));
                form.Location = new Point(area.Left + (area.Width - form.Width) / 2, area.Top + (area.Height - form.Height) / 2);
                maximize();
            };
            form.FormClosed += (s, e) => maximizedForms.Remove(form);

            EventHandler toggle = (s, e) =>
            {
                isDragging = false;
                if (maximizedForms.Remove(form))
                    form.Bounds = normalBounds;
                else
                    maximize();
            };

            titleBar.DoubleClick += toggle;
            foreach (Control control in titleBar.Controls)
                if (control is Label) control.DoubleClick += toggle;
        }

        private static readonly HashSet<Form> maximizedForms = new HashSet<Form>();
        private static bool isDragging = false;
        private static Point startPoint = new Point(0, 0);

        private static void Panel_MouseDown(object sender, MouseEventArgs e)
        {
            isDragging = true;
            startPoint = new Point(e.X, e.Y);
        }

        private static void Panel_MouseMove(object sender, MouseEventArgs e, Form form)
        {
            if (isDragging && !maximizedForms.Contains(form))
            {
                // Calculate the new position of the form
                Point newPosition = new Point(e.X - startPoint.X, e.Y - startPoint.Y);
                form.Location = new Point(form.Location.X + newPosition.X, form.Location.Y + newPosition.Y);
            }
        }

        private static void Panel_MouseUp(object sender, MouseEventArgs e)
        {
            // Stop dragging
            isDragging = false;
        }
    }
}