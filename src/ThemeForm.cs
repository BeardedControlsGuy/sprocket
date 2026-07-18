using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Sprocket
{
    /// <summary>Workbench theme picker — WorkPlace Launcher parity: pick one of the platform's
    /// installed *theme*.jar modules and lock it as the Workbench default.</summary>
    internal sealed class ThemeForm : Form, IAuroraHost
    {
        private readonly Backdrop _backdrop = new Backdrop();
        private readonly NiagaraPlatform _platform;
        private readonly List<string> _themes;

        private PillSelect _themeSelect;
        private Label _emptyLabel;
        private HeroButton _applyButton;

        public ThemeForm(NiagaraPlatform platform)
        {
            HandleCreated += delegate { DwmUtil.RequestRoundedCorners(this); };
            _platform = platform;
            _themes = ThemeManager.FindThemes(platform);
            BuildUi();
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
            Text = "Workbench Theme";
            ClientSize = new Size(440, 280);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = SprocketTheme.WindowBg;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw, true);

            const int m = 26;
            int w = ClientSize.Width - m * 2;

            Label title = new Label();
            title.Text = "Workbench Theme";
            title.UseMnemonic = false;
            title.ForeColor = SprocketTheme.TextPrimary;
            title.BackColor = Color.Transparent;
            title.Font = new Font(SprocketTheme.HeadingFamily, 14F, FontStyle.Bold);
            title.SetBounds(m, 20, w, 26);
            Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = _platform.DisplayName;
            subtitle.ForeColor = SprocketTheme.TextSecondary;
            subtitle.BackColor = Color.Transparent;
            subtitle.Font = new Font(SprocketTheme.BodyFamily, 8.25F);
            subtitle.SetBounds(m, 46, w, 16);
            Controls.Add(subtitle);

            if (_themes.Count == 0)
            {
                _emptyLabel = new Label();
                _emptyLabel.Text = "No theme modules (*theme*.jar) were found in this platform's "
                    + "\\modules folder. Install a Workbench theme module first.";
                _emptyLabel.ForeColor = SprocketTheme.TextSecondary;
                _emptyLabel.BackColor = Color.Transparent;
                _emptyLabel.Font = new Font(SprocketTheme.BodyFamily, 9F);
                _emptyLabel.SetBounds(m, 90, w, 60);
                Controls.Add(_emptyLabel);
            }
            else
            {
                Label sectionLabel = new Label();
                sectionLabel.Text = "Default theme";
                sectionLabel.ForeColor = SprocketTheme.TextPrimary;
                sectionLabel.BackColor = Color.Transparent;
                sectionLabel.Font = new Font(SprocketTheme.BodyFamily, 9F, FontStyle.Bold);
                sectionLabel.SetBounds(m, 84, w, 16);
                Controls.Add(sectionLabel);

                _themeSelect = new PillSelect();
                _themeSelect.SetBounds(m, 104, w, 36);
                foreach (string theme in _themes) _themeSelect.Items.Add(theme);

                string current = ThemeManager.GetCurrentTheme(_platform);
                int idx = current == null ? -1 : _themes.FindIndex(delegate(string t)
                {
                    return string.Equals(t, current, StringComparison.OrdinalIgnoreCase);
                });
                _themeSelect.SelectedIndex = idx >= 0 ? idx : 0;
                Controls.Add(_themeSelect);

                Label note = new Label();
                note.Text = "Applies to this platform's Workbench and locks it — a user can't "
                    + "change it from inside Workbench itself. Restart Workbench to see it.";
                note.ForeColor = SprocketTheme.TextSecondary;
                note.BackColor = Color.Transparent;
                note.Font = new Font(SprocketTheme.BodyFamily, 8F);
                note.SetBounds(m, 150, w, 40);
                Controls.Add(note);
            }

            Panel hairline = new Panel();
            hairline.BackColor = SprocketTheme.Hairline;
            hairline.SetBounds(m, ClientSize.Height - 60, w, 1);
            Controls.Add(hairline);

            TextGhostButton cancel = new TextGhostButton();
            cancel.Text = "Cancel";
            cancel.SetBounds(m, ClientSize.Height - 46, 100, 32);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);

            _applyButton = new HeroButton();
            _applyButton.Text = "Apply";
            _applyButton.Enabled = _themes.Count > 0;
            _applyButton.Font = new Font(SprocketTheme.HeadingFamily, 10.5F, FontStyle.Bold);
            _applyButton.SetBounds(ClientSize.Width - m - 130, ClientSize.Height - 46, 130, 32);
            _applyButton.Click += ApplyClicked;
            Controls.Add(_applyButton);
        }

        private void ApplyClicked(object sender, EventArgs e)
        {
            if (_themeSelect == null || _themeSelect.SelectedItem == null) return;
            string theme = (string)_themeSelect.SelectedItem;

            if (!ThemeManager.SetTheme(_platform, theme))
            {
                MessageBox.Show("Couldn't update brand.properties for this platform.", "Sprocket",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            MessageBox.Show("Theme applied. Restart Workbench to see it.", "Sprocket",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
