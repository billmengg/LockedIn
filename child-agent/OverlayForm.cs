using System;
using System.Drawing;
using System.Windows.Forms;

namespace AccountabilityAgent
{
    public partial class OverlayForm : Form
    {
        public OverlayForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.Size = new Size(300, 60);
            this.Location = new Point(
                Screen.PrimaryScreen.WorkingArea.Right - 320,
                Screen.PrimaryScreen.WorkingArea.Top + 10
            );
            
            // Use solid color instead of transparent to avoid error
            this.BackColor = Color.Orange;
            this.Opacity = 0.85;

            Label label = new Label
            {
                Text = "Parent check-in active",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.Black,
                BackColor = Color.Orange, // Solid background
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };

            this.Controls.Add(label);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.FillRectangle(new SolidBrush(BackColor), ClientRectangle);
        }
    }
}

