using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Sprocket
{
    /// <summary>Extra Niagara scan folders + Workbench language — WorkPlace Launcher's
    /// "+Folders" and language settings, restyled.</summary>
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
            BackColor = SprocketTheme.Ink;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw, true);

            const int m = 28;
            int w = ClientSize.Width - m * 2;

            AddMicro("SPROCKET SETTINGS", m, 22, w);

            Label title = new Label();
            title.Text = "Locations & Language";
            title.UseMnemonic = false;
            title.ForeColor = SprocketTheme.TextPrimary;
            title.BackColor = Color.Transparent;
            title.Font = new Font(SprocketTheme.HeadingFamily, 13F, FontStyle.Bold);
            title.SetBounds(m, 38, w, 26);
            Controls.Add(title);

            AddMicro("EXTRA NIAGARA LOCATIONS", m, 78, w);

            Label note = new Label();
            note.Text = "C:\\, Program Files and Program Files (x86) are always scanned. "
                + "Add folders here for installs that live anywhere else.";
            note.ForeColor = SprocketTheme.TextMuted;
            note.BackColor = Color.Transparent;
            note.Font = new Font(SprocketTheme.BodyFamily, 8.25F);
            note.SetBounds(m, 92, w, 30);
            Controls.Add(note);

            _folderList = new FolderList();
            _folderList.SetBounds(m, 126, w, 130);
            _folderList.SetFolders(_settings.Folders);
            _folderList.RemoveClicked += RemoveFolder;
            Controls.Add(_folderList);

            TextGhostButton addButton = new TextGhostButton();
            addButton.Text = "Add Folder…";
            addButton.SetBounds(m, 264, 150, 38);
            addButton.Click += AddFolderClicked;
            Controls.Add(addButton);

            AddMicro("WORKBENCH LANGUAGE", m, 318, w);

            _languageSelect = new PillSelect();
            _languageSelect.SetBounds(m, 334, 200, 40);
            for (int i = 0; i < LocaleNames.Length; i++)
                _languageSelect.Items.Add(LocaleNames[i]);
            _languageSelect.SelectedIndex = Math.Max(0, Array.IndexOf(LocaleCodes, _settings.Locale));
            _languageSelect.SelectedIndexChanged += delegate
            {
                _settings.Locale = LocaleCodes[_languageSelect.SelectedIndex];
                _settings.Save();
            };
            Controls.Add(_languageSelect);

            HeroButton done = new HeroButton();
            done.Text = "DONE";
            done.Font = new Font(SprocketTheme.HeadingFamily, 10F, FontStyle.Bold);
            done.SetBounds(ClientSize.Width - m - 150, 384, 150, 42);
            done.Click += delegate { DialogResult = DialogResult.OK; Close(); };
            Controls.Add(done);
        }

        private void AddMicro(string text, int x, int y, int w)
        {
            Label l = new Label();
            l.Text = SprocketTheme.Track(text);
            l.ForeColor = SprocketTheme.TextFaint;
            l.BackColor = Color.Transparent;
            l.Font = new Font(SprocketTheme.BodyFamily, 6.75F, FontStyle.Bold);
            l.SetBounds(x, y, w, 12);
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
