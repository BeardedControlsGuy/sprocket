using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Sprocket
{
    /// <summary>Extra Niagara scan folders + Workbench language — WorkPlace Launcher's
    /// "+Folders" and language settings, Fluent light (mock 3a).</summary>
    internal sealed class LocationsForm : Form, IAuroraHost
    {
        private static readonly string[] LocaleCodes = new[] { "en", "fr", "de", "es" };
        private static readonly string[] LocaleNames = new[] { "English", "French", "German", "Spanish" };

        private readonly Backdrop _backdrop = new Backdrop();
        private readonly UserSettings _settings;
        private FolderList _folderList;
        private PillSelect _languageSelect;

        public LocationsForm()
        {
            _settings = UserSettings.Load();
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
            Text = "Locations & Language";
            ClientSize = new Size(470, 434);
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
            title.Text = "Locations & Language";
            title.UseMnemonic = false;
            title.ForeColor = SprocketTheme.TextPrimary;
            title.BackColor = Color.Transparent;
            title.Font = new Font(SprocketTheme.HeadingFamily, 14F, FontStyle.Bold);
            title.SetBounds(m, 20, w, 26);
            Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "Sprocket settings";
            subtitle.ForeColor = SprocketTheme.TextSecondary;
            subtitle.BackColor = Color.Transparent;
            subtitle.Font = new Font(SprocketTheme.BodyFamily, 8.25F);
            subtitle.SetBounds(m, 46, w, 16);
            Controls.Add(subtitle);

            AddSectionLabel("Extra Niagara locations", m, 76, w);

            Label note = new Label();
            note.Text = "C:\\, Program Files and Program Files (x86) are always scanned. "
                + "Add root folders here for installs that live anywhere else — they're remembered on this machine.";
            note.ForeColor = SprocketTheme.TextSecondary;
            note.BackColor = Color.Transparent;
            note.Font = new Font(SprocketTheme.BodyFamily, 8F);
            note.SetBounds(m, 94, w, 32);
            Controls.Add(note);

            _folderList = new FolderList();
            _folderList.SetBounds(m, 130, w, 110);
            _folderList.SetFolders(_settings.Folders);
            _folderList.RemoveClicked += RemoveFolder;
            Controls.Add(_folderList);

            TextGhostButton addButton = new TextGhostButton();
            addButton.Text = "Add folder…";
            addButton.Glyph = SprocketTheme.Glyph(0xE710);
            addButton.SetBounds(m, 248, 140, 32);
            addButton.Click += AddFolderClicked;
            Controls.Add(addButton);

            AddSectionLabel("Workbench language", m, 298, w);

            _languageSelect = new PillSelect();
            _languageSelect.SetBounds(m, 316, 200, 34);
            for (int i = 0; i < LocaleNames.Length; i++)
                _languageSelect.Items.Add(LocaleNames[i]);
            _languageSelect.SelectedIndex = Math.Max(0, Array.IndexOf(LocaleCodes, _settings.Locale));
            _languageSelect.SelectedIndexChanged += delegate
            {
                _settings.Locale = LocaleCodes[_languageSelect.SelectedIndex];
                _settings.Save();
            };
            Controls.Add(_languageSelect);

            Panel hairline = new Panel();
            hairline.BackColor = SprocketTheme.Hairline;
            hairline.SetBounds(m, 366, w, 1);
            Controls.Add(hairline);

            HeroButton done = new HeroButton();
            done.Text = "Done";
            done.Font = new Font(SprocketTheme.HeadingFamily, 10.5F, FontStyle.Bold);
            done.SetBounds(ClientSize.Width - m - 130, 380, 130, 34);
            done.Click += delegate { DialogResult = DialogResult.OK; Close(); };
            Controls.Add(done);
        }

        private void AddSectionLabel(string text, int x, int y, int w)
        {
            Label l = new Label();
            l.Text = text;
            l.ForeColor = SprocketTheme.TextPrimary;
            l.BackColor = Color.Transparent;
            l.Font = new Font(SprocketTheme.BodyFamily, 9F, FontStyle.Bold);
            l.SetBounds(x, y, w, 16);
            Controls.Add(l);
        }

        private void AddFolderClicked(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Pick a folder that contains Niagara installs "
                    + "(the folder itself, or <folder>\\<install>\\bin\\wb.exe).";
                dlg.ShowNewFolderButton = false;

                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                string folder = dlg.SelectedPath.TrimEnd(Path.DirectorySeparatorChar);
                if (UserSettings.ContainsIgnoreCase(_settings.Folders, folder))
                {
                    MessageBox.Show("That folder is already in the list.", "Sprocket",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                int hits = PlatformScanner.ScanRoot(folder).Count;
                if (hits == 0)
                {
                    DialogResult keep = MessageBox.Show(
                        "No Niagara installs were found under that folder yet.\r\n\r\nAdd it anyway?",
                        "Sprocket", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (keep != DialogResult.Yes) return;
                }

                _settings.Folders.Add(folder);
                _settings.Save();
                _folderList.SetFolders(_settings.Folders);
            }
        }

        private void RemoveFolder(string folder)
        {
            _settings.Folders.Remove(folder);
            _settings.Save();
            _folderList.SetFolders(_settings.Folders);
        }
    }
}
