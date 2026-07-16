using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Sprocket
{
    internal sealed class MainForm : Form, IAuroraHost
    {
        private const int Gutter = 30;

        private List<NiagaraPlatform> _platforms;
        private readonly Backdrop _backdrop = new Backdrop();

        // header
        private PictureBox _gear;
        private GradientTitle _title;
        private Label _subtitle;
        private GhostIconButton _refreshButton;
        private GhostIconButton _foldersButton;

        // body
        private PillSelect _platformSelect;
        private StatusPanel _statusPanel;
        private EmptyCard _emptyCard;
        private HeroButton _launchButton;
        private Label _quickLabel;

        private IconTile _alarmTile;
        private IconTile _consoleTile;
        private IconTile _openFolderTile;
        private IconTile _memoryTile;
        private IconTile _addModulesTile;
        private IconTile _installDaemonTile;
        private IconTile _importNavTile;
        private IconTile _exportNavTile;
        private IconTile _daemonToggleTile;
        private IconTile[] _tiles;

        // footer
        private GradientBar _footerBar;
        private Label _versionLabel;
        private LinkLabel _updateLink;

        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Sprocket", "lastplatform.txt");

        public MainForm()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw, true);
            BuildUi();
            LoadIcon();
            Rescan();
            CheckForUpdate();
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

        private void LoadIcon()
        {
            string icoPath = Path.Combine(Application.StartupPath, "assets", "sprocket.ico");
            if (File.Exists(icoPath))
            {
                try { this.Icon = new Icon(icoPath); }
                catch { }
            }
        }

        // ---------------------------------------------------------------- UI

        private void BuildUi()
        {
            Text = "Sprocket";
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = SprocketTheme.Ink;
            ClientSize = new Size(600, 724);
            MinimumSize = new Size(560, 744);

            // header
            _gear = new PictureBox();
            _gear.SizeMode = PictureBoxSizeMode.Zoom;
            _gear.BackColor = Color.Transparent;
            string gearPath = Path.Combine(Application.StartupPath, "assets", "sprocket_gear.png");
            if (File.Exists(gearPath))
            {
                try { _gear.Image = Image.FromFile(gearPath); }
                catch { }
            }
            Controls.Add(_gear);

            _title = new GradientTitle();
            _title.Text = "Sprocket";
            Controls.Add(_title);

            _subtitle = new Label();
            _subtitle.Text = SprocketTheme.Track("NIAGARA PLATFORM LAUNCHER");
            _subtitle.ForeColor = SprocketTheme.TextMuted;
            _subtitle.BackColor = Color.Transparent;
            _subtitle.Font = new Font(SprocketTheme.BodyFamily, 7.25F, FontStyle.Bold);
            Controls.Add(_subtitle);

            _refreshButton = new GhostIconButton();
            _refreshButton.Glyph = SprocketTheme.Glyph(0xE72C); // Refresh
            _refreshButton.Click += delegate { Rescan(); };
            Controls.Add(_refreshButton);

            _foldersButton = new GhostIconButton();
            _foldersButton.Glyph = SprocketTheme.Glyph(0xE710); // Add ("+Folders")
            _foldersButton.Click += FoldersClicked;
            Controls.Add(_foldersButton);

            ToolTip tips = new ToolTip();
            tips.SetToolTip(_refreshButton, "Rescan for Niagara installs");
            tips.SetToolTip(_foldersButton, "Locations && language");

            // body
            _platformSelect = new PillSelect();
            _platformSelect.SelectedIndexChanged += delegate { UpdateForSelection(); };
            Controls.Add(_platformSelect);

            _statusPanel = new StatusPanel();
            Controls.Add(_statusPanel);

            _emptyCard = new EmptyCard();
            _emptyCard.Sub = "Sprocket scans C:\\, Program Files and Program Files (x86) for "
                + "<vendor>\\<install>\\bin\\wb.exe. Install Niagara, then rescan with the button above.";
            _emptyCard.Visible = false;
            Controls.Add(_emptyCard);

            _launchButton = new HeroButton();
            _launchButton.Text = "LAUNCH WORKBENCH";
            _launchButton.Click += delegate { LaunchSelected(ProcessLauncher.LaunchWorkbench); };
            Controls.Add(_launchButton);

            _quickLabel = new Label();
            _quickLabel.Text = SprocketTheme.Track("QUICK ACTIONS");
            _quickLabel.ForeColor = SprocketTheme.TextFaint;
            _quickLabel.BackColor = Color.Transparent;
            _quickLabel.Font = new Font(SprocketTheme.BodyFamily, 7.25F, FontStyle.Bold);
            _quickLabel.AutoSize = true;
            Controls.Add(_quickLabel);

            _alarmTile = MakeTile("Alarm Portal", 0xEA8F);          // Ringer
            _alarmTile.Click += delegate { LaunchSelected(ProcessLauncher.LaunchAlarmPortal); };

            _consoleTile = MakeTile("Console", 0xE756);             // CommandPrompt
            _consoleTile.Click += delegate { LaunchSelected(ProcessLauncher.LaunchConsole); };

            _openFolderTile = MakeTile("Open Folder", 0xE838);      // OpenFolderHorizontal
            _openFolderTile.Click += delegate { LaunchSelected(ProcessLauncher.OpenInstallFolder); };

            _memoryTile = MakeTile("Memory Settings", 0xE713);      // Settings
            _memoryTile.Click += MemoryTileClicked;

            _addModulesTile = MakeTile("Add Modules", 0xE710);      // Add
            _addModulesTile.Click += AddModulesClicked;

            _installDaemonTile = MakeTile("Install Daemon", 0xE7EF); // Admin
            _installDaemonTile.Click += delegate { LaunchSelected(ProcessLauncher.LaunchPlatformDaemonInstaller); };

            _importNavTile = MakeTile("Import Nav Tree", 0xE896);   // Download
            _importNavTile.Click += ImportNavTreeClicked;

            _exportNavTile = MakeTile("Export Nav Tree", 0xE898);   // Upload
            _exportNavTile.Click += ExportNavTreeClicked;

            _daemonToggleTile = MakeTile("Stop Daemon", 0xE71A);    // Stop
            _daemonToggleTile.AccentColor = SprocketTheme.Danger;
            _daemonToggleTile.Click += DaemonToggleClicked;

            _tiles = new IconTile[]
            {
                _alarmTile, _consoleTile, _openFolderTile,
                _memoryTile, _addModulesTile, _installDaemonTile,
                _importNavTile, _exportNavTile, _daemonToggleTile
            };

            // footer
            _footerBar = new GradientBar();
            Controls.Add(_footerBar);

            _versionLabel = new Label();
            _versionLabel.Text = "V" + AppVersion.Display;
            _versionLabel.ForeColor = SprocketTheme.TextFaint;
            _versionLabel.BackColor = Color.Transparent;
            _versionLabel.Font = new Font(SprocketTheme.BodyFamily, 6.75F, FontStyle.Bold);
            _versionLabel.AutoSize = true;
            Controls.Add(_versionLabel);

            _updateLink = new LinkLabel();
            _updateLink.Text = "";
            _updateLink.Visible = false;
            _updateLink.AutoSize = true;
            _updateLink.BackColor = Color.Transparent;
            _updateLink.LinkColor = SprocketTheme.EmberLight;
            _updateLink.ActiveLinkColor = SprocketTheme.Sun;
            _updateLink.VisitedLinkColor = SprocketTheme.EmberLight;
            _updateLink.LinkBehavior = LinkBehavior.HoverUnderline;
            _updateLink.Font = new Font(SprocketTheme.BodyFamily, 6.75F, FontStyle.Bold);
            _updateLink.Click += delegate
            {
                string url = _updateLink.Tag as string;
                if (!string.IsNullOrEmpty(url)) ProcessLauncher.OpenUrl(url);
            };
            Controls.Add(_updateLink);

            Resize += delegate { DoLayout(); };
            DoLayout();
        }

        private IconTile MakeTile(string text, int glyphCodepoint)
        {
            IconTile t = new IconTile();
            t.Text = text;
            t.Glyph = SprocketTheme.Glyph(glyphCodepoint);
            Controls.Add(t);
            return t;
        }

        private void DoLayout()
        {
            int w = ClientSize.Width - Gutter * 2;
            if (w < 100) return;

            // header
            _gear.SetBounds(Gutter, 26, 46, 46);
            _title.SetBounds(Gutter + 58, 22, w - 58 - 150, 36);
            _subtitle.SetBounds(Gutter + 61, 57, w - 61 - 150, 14);
            _refreshButton.SetBounds(ClientSize.Width - Gutter - 42, 28, 42, 42);
            _foldersButton.SetBounds(ClientSize.Width - Gutter - 42 * 2 - 8, 28, 42, 42);

            int y = 96;
            _platformSelect.SetBounds(Gutter, y, w, 44);
            _emptyCard.SetBounds(Gutter, y, w, 150);
            y += 58;

            _statusPanel.SetBounds(Gutter, y, w, 158);
            y += 172;

            _launchButton.SetBounds(Gutter, y, w, 56);
            y += 72;

            _quickLabel.Location = new Point(Gutter + 2, y);
            y += 22;

            int cols = w >= 450 ? 3 : 2;
            int gap = 10;
            int tileW = (w - (cols - 1) * gap) / cols;
            int tileH = 66;
            for (int i = 0; i < _tiles.Length; i++)
            {
                int col = i % cols;
                int row = i / cols;
                _tiles[i].SetBounds(Gutter + col * (tileW + gap), y + row * (tileH + gap), tileW, tileH);
            }
            int rows = (_tiles.Length + cols - 1) / cols;
            y += rows * (tileH + gap) - gap;

            int footerY = Math.Max(y + 16, ClientSize.Height - 50);
            _footerBar.SetBounds(Gutter, footerY, w, 2);
            _versionLabel.Location = new Point(
                ClientSize.Width - Gutter - _versionLabel.PreferredWidth, footerY + 12);
            _updateLink.Location = new Point(Gutter, footerY + 12);
        }

        // ---------------------------------------------------------- scanning

        private void Rescan()
        {
            _platforms = PlatformScanner.Scan();

            _platformSelect.Items.Clear();
            foreach (NiagaraPlatform p in _platforms)
                _platformSelect.Items.Add(p);

            bool any = _platforms.Count > 0;
            _platformSelect.Visible = any;
            _statusPanel.Visible = any;
            _launchButton.Visible = any;
            _quickLabel.Visible = any;
            for (int i = 0; i < _tiles.Length; i++)
                _tiles[i].Visible = any;
            _emptyCard.Visible = !any;

            if (any)
            {
                int idx = FindLastUsedIndex();
                _platformSelect.SelectedIndex = idx >= 0 ? idx : 0;
                UpdateForSelection();
            }
        }

        private int FindLastUsedIndex()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return -1;
                string last = File.ReadAllText(SettingsPath).Trim();
                for (int i = 0; i < _platforms.Count; i++)
                {
                    if (string.Equals(_platforms[i].InstallDir, last, StringComparison.OrdinalIgnoreCase))
                        return i;
                }
            }
            catch { }
            return -1;
        }

        private void SaveLastUsed(NiagaraPlatform platform)
        {
            try
            {
                string dir = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(SettingsPath, platform.InstallDir);
            }
            catch { }
        }

        private NiagaraPlatform SelectedPlatform
        {
            get { return (NiagaraPlatform)_platformSelect.SelectedItem; }
        }

        private void UpdateForSelection()
        {
            NiagaraPlatform p = SelectedPlatform;
            if (p == null) return;

            _statusPanel.PlatformName = Path.GetFileName(p.InstallDir);
            _statusPanel.BitnessText = p.Bitness;
            _statusPanel.VersionText = string.IsNullOrEmpty(p.BrandVersion) ? "—" : p.BrandVersion;
            _statusPanel.PathText = p.InstallDir;
            _statusPanel.HostIdText = "…";
            _statusPanel.Invalidate();

            _consoleTile.Enabled = p.HasConsole;
            _installDaemonTile.Enabled = p.HasPlatDaemonInstaller;

            RefreshDaemonState(p);

            HostIdResolver.ResolveAsync(p, delegate(string hostId)
            {
                if (IsDisposed) return;
                if (InvokeRequired)
                {
                    BeginInvoke((MethodInvoker)delegate { ApplyHostId(p, hostId); });
                }
                else
                {
                    ApplyHostId(p, hostId);
                }
            });
        }

        private void ApplyHostId(NiagaraPlatform forPlatform, string hostId)
        {
            if (SelectedPlatform != forPlatform) return; // user moved on; discard stale result
            _statusPanel.HostIdText = hostId;
            _statusPanel.Invalidate();
        }

        private void CheckForUpdate()
        {
            UpdateChecker.CheckAsync(delegate(UpdateInfo info)
            {
                if (IsDisposed) return;
                if (InvokeRequired)
                {
                    BeginInvoke((MethodInvoker)delegate { ApplyUpdateNotice(info); });
                }
                else
                {
                    ApplyUpdateNotice(info);
                }
            });
        }

        private void ApplyUpdateNotice(UpdateInfo info)
        {
            if (IsDisposed) return;
            _updateLink.Text = "Update available: v" + info.Version.ToString(3) + " — click to download";
            _updateLink.Tag = info.HtmlUrl;
            _updateLink.Visible = true;
        }

        private void RefreshDaemonState(NiagaraPlatform p)
        {
            string serviceName = DaemonStatus.ServiceNameFor(p);
            _daemonToggleTile.Enabled = serviceName != null;

            DaemonState state = serviceName != null ? DaemonStatus.Query(p) : DaemonState.Unknown;
            switch (state)
            {
                case DaemonState.Running:
                    _statusPanel.StateColor = SprocketTheme.Success;
                    _statusPanel.StateText = "RUNNING";
                    _daemonToggleTile.Text = "Stop Daemon";
                    _daemonToggleTile.Glyph = SprocketTheme.Glyph(0xE71A); // Stop
                    _daemonToggleTile.AccentColor = SprocketTheme.Danger;
                    break;
                case DaemonState.Stopped:
                    _statusPanel.StateColor = SprocketTheme.Danger;
                    _statusPanel.StateText = "STOPPED";
                    _daemonToggleTile.Text = "Start Daemon";
                    _daemonToggleTile.Glyph = SprocketTheme.Glyph(0xE768); // Play
                    _daemonToggleTile.AccentColor = SprocketTheme.Success;
                    break;
                case DaemonState.Starting:
                case DaemonState.Stopping:
                    _statusPanel.StateColor = SprocketTheme.Sun;
                    _statusPanel.StateText = state == DaemonState.Starting ? "STARTING…" : "STOPPING…";
                    _daemonToggleTile.Text = state == DaemonState.Starting ? "Starting…" : "Stopping…";
                    _daemonToggleTile.Glyph = SprocketTheme.Glyph(0xE72C); // Refresh (in-progress)
                    _daemonToggleTile.AccentColor = SprocketTheme.Sun;
                    _daemonToggleTile.Enabled = false; // block re-click until it settles
                    break;
                default:
                    _statusPanel.StateColor = SprocketTheme.TextFaint;
                    _statusPanel.StateText = "UNKNOWN";
                    _daemonToggleTile.Text = "Daemon";
                    _daemonToggleTile.Glyph = SprocketTheme.Glyph(0xE946); // Info
                    _daemonToggleTile.AccentColor = SprocketTheme.Ember;
                    break;
            }
            _statusPanel.Invalidate();
            _daemonToggleTile.Invalidate();
        }

        // ----------------------------------------------------------- actions

        private void LaunchSelected(Action<NiagaraPlatform> action)
        {
            NiagaraPlatform p = SelectedPlatform;
            if (p == null) return;
            SaveLastUsed(p);
            action(p);
        }

        private void FoldersClicked(object sender, EventArgs e)
        {
            using (LocationsForm dlg = new LocationsForm())
                dlg.ShowDialog(this);
            Rescan();
        }

        private void MemoryTileClicked(object sender, EventArgs e)
        {
            NiagaraPlatform p = SelectedPlatform;
            if (p == null) return;
            using (MemorySettingsForm dlg = new MemorySettingsForm(p))
                dlg.ShowDialog(this);
        }

        private void AddModulesClicked(object sender, EventArgs e)
        {
            NiagaraPlatform p = SelectedPlatform;
            if (p == null) return;

            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "Add Niagara Modules";
                dlg.Filter = "Niagara Modules (*.jar)|*.jar";
                dlg.Multiselect = true;

                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    if (!Directory.Exists(p.ModulesDir)) Directory.CreateDirectory(p.ModulesDir);
                    foreach (string src in dlg.FileNames)
                        File.Copy(src, Path.Combine(p.ModulesDir, Path.GetFileName(src)), true);

                    MessageBox.Show(dlg.FileNames.Length + " module(s) copied to " + p.ModulesDir +
                        ".\r\n\r\nRun Software Manager in Workbench to install them into a station.",
                        "Sprocket", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Couldn't copy modules:\r\n" + ex.Message, "Sprocket",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ImportNavTreeClicked(object sender, EventArgs e)
        {
            NiagaraPlatform p = SelectedPlatform;
            if (p == null) return;

            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "Import Nav Tree";
                dlg.Filter = "Nav Tree (*.xml)|*.xml";

                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    string destDir = Path.GetDirectoryName(p.NavTreeXmlPath);
                    if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                    File.Copy(dlg.FileName, p.NavTreeXmlPath, true);
                    MessageBox.Show("Nav tree imported. Restart Workbench to see it.", "Sprocket",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Couldn't import nav tree:\r\n" + ex.Message, "Sprocket",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ExportNavTreeClicked(object sender, EventArgs e)
        {
            NiagaraPlatform p = SelectedPlatform;
            if (p == null) return;

            if (!File.Exists(p.NavTreeXmlPath))
            {
                MessageBox.Show("No saved nav tree found for this platform yet.", "Sprocket",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Title = "Export Nav Tree";
                dlg.Filter = "Nav Tree (*.xml)|*.xml";
                dlg.FileName = "navTree.xml";

                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    File.Copy(p.NavTreeXmlPath, dlg.FileName, true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Couldn't export nav tree:\r\n" + ex.Message, "Sprocket",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void DaemonToggleClicked(object sender, EventArgs e)
        {
            NiagaraPlatform p = SelectedPlatform;
            if (p == null) return;

            string serviceName = DaemonStatus.ServiceNameFor(p);
            if (serviceName == null) return;

            bool currentlyRunning = DaemonStatus.Query(p) == DaemonState.Running;

            // Optimistic transitional UI: a Niagara daemon can take well over a minute to actually
            // reach RUNNING, and starting it may pop a UAC prompt the user has to respond to. Doing
            // this synchronously (old behavior) froze the UI, and refreshing immediately after just
            // read the still-PENDING state as Unknown and silently reverted the button — which is
            // what made toggling look broken, especially on a platform whose daemon takes longer to
            // settle. Show progress now, do the real work on a background thread, and only settle
            // the tile once the service actually reaches a terminal state.
            _daemonToggleTile.Enabled = false;
            _statusPanel.StateColor = SprocketTheme.Sun;
            _statusPanel.StateText = currentlyRunning ? "STOPPING…" : "STARTING…";
            _daemonToggleTile.Text = currentlyRunning ? "Stopping…" : "Starting…";
            _daemonToggleTile.Invalidate();
            _statusPanel.Invalidate();

            Thread t = new Thread(delegate()
            {
                bool ok = DaemonStatus.SetRunning(serviceName, !currentlyRunning);
                if (ok) DaemonStatus.WaitForSettled(serviceName, 45000);

                if (IsDisposed) return;
                BeginInvoke((MethodInvoker)delegate
                {
                    if (IsDisposed) return;
                    if (SelectedPlatform != p) return; // user moved to another platform; its own
                                                        // RefreshDaemonState already took over

                    if (!ok)
                    {
                        MessageBox.Show(
                            "Couldn't " + (currentlyRunning ? "stop" : "start") + " the daemon service.",
                            "Sprocket", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    RefreshDaemonState(p);
                });
            });
            t.IsBackground = true;
            t.Start();
        }
    }
}

