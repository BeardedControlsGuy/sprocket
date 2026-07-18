using System;
using System.Drawing;
using System.Windows.Forms;

namespace Sprocket
{
    internal sealed class MemorySettingsForm : Form, IAuroraHost
    {
        private readonly NiagaraPlatform _platform;
        private readonly Backdrop _backdrop = new Backdrop();
        private PillField _stationField;
        private PillField _wbField;

        public MemorySettingsForm(NiagaraPlatform platform)
        {
            _platform = platform;
            BuildUi();
            LoadValues();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            _backdrop.Paint(e.Graphics, ClientSize);
        }

        public void PaintAuroraSlice(Graphics g, Control child)
        {
            Point offset = PointToClient(child.PointToScreen(Point.Empty));
            _backdrop.PaintSlice(g, ClientSize, offset);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _backdrop.Dispose();
            base.Dispose(disposing);
        }

        private void BuildUi()
        {
            Text = "Memory Settings — " + _platform.DisplayName;
            ClientSize = new Size(400, 230);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = SprocketTheme.WindowBg;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw, true);

            const int m = 26;
            int w = ClientSize.Width - m * 2;

            Label name = new Label();
            name.Text = _platform.DisplayName;
            name.ForeColor = SprocketTheme.TextPrimary;
            name.BackColor = Color.Transparent;
            name.Font = new Font(SprocketTheme.HeadingFamily, 14F, FontStyle.Bold);
            name.AutoEllipsis = true;
            name.SetBounds(m, 20, w, 26);
            Controls.Add(name);

            Label intro = new Label();
            intro.Text = "JVM max heap (-Xmx) written to nre.properties. Applies on next launch.";
            intro.ForeColor = SprocketTheme.TextSecondary;
            intro.BackColor = Color.Transparent;
            intro.Font = new Font(SprocketTheme.BodyFamily, 8F);
            intro.SetBounds(m, 48, w, 30);
            Controls.Add(intro);

            int colW = (w - 12) / 2;
            int col2 = m + colW + 12;

            AddFieldLabel("Station (MB)", m, 86, colW);
            _stationField = MakeField(m, 104, colW);

            AddFieldLabel("Workbench (MB)", col2, 86, colW);
            _wbField = MakeField(col2, 104, colW);

            Panel hairline = new Panel();
            hairline.BackColor = SprocketTheme.Hairline;
            hairline.SetBounds(m, 160, w, 1);
            Controls.Add(hairline);

            TextGhostButton cancel = new TextGhostButton();
            cancel.Text = "Cancel";
            cancel.SetBounds(m, 176, colW, 34);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);

            HeroButton save = new HeroButton();
            save.Text = "Save";
            save.Font = new Font(SprocketTheme.HeadingFamily, 10.5F, FontStyle.Bold);
            save.SetBounds(col2, 176, colW, 34);
            save.Click += SaveClicked;
            Controls.Add(save);
        }

        private void AddFieldLabel(string text, int x, int y, int w)
        {
            Label l = new Label();
            l.Text = text;
            l.ForeColor = SprocketTheme.TextPrimary;
            l.BackColor = Color.Transparent;
            l.Font = new Font(SprocketTheme.BodyFamily, 9F, FontStyle.Bold);
            l.SetBounds(x, y, w, 16);
            Controls.Add(l);
        }

        private PillField MakeField(int x, int y, int w)
        {
            PillField f = new PillField();
            f.SetBounds(x, y, w, 36);
            f.Input.Minimum = 256;
            f.Input.Maximum = 32768;
            f.Input.Increment = 256;
            Controls.Add(f);
            return f;
        }

        private void LoadValues()
        {
            HeapSizes heap = MemorySettings.Read(_platform);
            _stationField.Input.Value = heap.StationMb > 0 ? heap.StationMb : 1024;
            _wbField.Input.Value = heap.WorkbenchMb > 0 ? heap.WorkbenchMb : 1024;
        }

        private void SaveClicked(object sender, EventArgs e)
        {
            try
            {
                MemorySettings.Write(_platform, (int)_stationField.Input.Value, (int)_wbField.Input.Value);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Couldn't save memory settings:\r\n" + ex.Message, "Sprocket",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
