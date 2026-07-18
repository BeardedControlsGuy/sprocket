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
        private Label _title;
        private Label _subtitle;
        private GhostIconButton _refreshButton;
        private GhostIconButton _foldersButton;
        private ToolTip _tips;

        // body
        private PillSelect _platformSelect;
        private InfoBanner _banner;
        private StatusPanel _statusPanel;
        private EmptyCard _emptyCard;
        private HeroButton _launchButton;
        private HeroButton _daemonButton;
        private Label _quickLabel;

        private IconTile _alarmTile;
        private IconTile _consoleTile;
        private IconTile _openFolderTile;
        private IconTile _memoryTile;
        private IconTile _modulesTile;
        private IconTile _installDaemonTile;
        private IconTile _importNavTile;
        private IconTile _exportNavTile;
        private IconTile _themeTile;
        private IconTile[] _tiles;

        // footer
        private Panel _footerHairline;
        private Label _versionLabel;
        private LinkLabel _updateLink;

        // tray
        private NotifyIcon _trayIcon;

        // Global daemon state. Windows hosts exactly one Niagara daemon service, so these are
        // machine-wide, not per-platform: _registeredPlatform owns that service (running or not),
        // and _runningPlatform is non-null only when it is actually up.
        private NiagaraPlatform _registeredPlatform;
        private NiagaraPlatform _runningPlatform;
        private string _opPhase;              // null / "stopping" / "starting" / "registering"
        private NiagaraPlatform _opFrom;
        private NiagaraPlatform _opTo;
        private bool _opTwoStep;

        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Sprocket", "lastplatform.txt");

        public MainForm()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw, true);
            HandleCreated += delegate { DwmUtil.RequestRoundedCorners(this); };
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
            if (disposing)
            {
                _backdrop.Dispose();
                if (_trayIcon != null) _trayIcon.Dispose();
            }
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
            if (_trayIcon != null) _trayIcon.Icon = this.Icon ?? System.Drawing.SystemIcons.Application;
        }

        // ---------------------------------------------------------------- UI

        private void BuildUi()
        {
            Text = "Sprocket";
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = SprocketTheme.WindowBg;
            ClientSize = new Size(600, 674);
            MinimumSize = new Size(560, 654);

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

            _title = new Label();
            _title.Text = "Sprocket";
            _title.ForeColor = SprocketTheme.TextPrimary;
            _title.BackColor = Color.Transparent;
            _title.Font = new Font(SprocketTheme.HeadingFamily, 16F, FontStyle.Bold);
            Controls.Add(_title);

            _subtitle = new Label();
            _subtitle.Text = "Niagara platform launcher";
            _subtitle.ForeColor = SprocketTheme.TextSecondary;
            _subtitle.BackColor = Color.Transparent;
            _subtitle.Font = new Font(SprocketTheme.BodyFamily, 8.25F);
            Controls.Add(_subtitle);

            _refreshButton = new GhostIconButton();
            _refreshButton.Glyph = SprocketTheme.Glyph(0xE72C); // Refresh
            _refreshButton.Click += delegate { Rescan(); };
            Controls.Add(_refreshButton);

            _foldersButton = new GhostIconButton();
            _foldersButton.Glyph = SprocketTheme.Glyph(0xE710); // Add ("+Folders")
            _foldersButton.Click += FoldersClicked;
            Controls.Add(_foldersButton);

            _tips = new ToolTip();
            _tips.SetToolTip(_refreshButton, "Rescan for Niagara installs");
            _tips.SetToolTip(_foldersButton, "Locations && language");

            // body
            _platformSelect = new PillSelect();
            _platformSelect.ShowScanRootsFooter = true;
            _platformSelect.SelectedIndexChanged += delegate { UpdateForSelection(); };
            _platformSelect.AddRootRequested += AddRootClicked;
            _platformSelect.RemoveRootRequested += RemoveRootClicked;
            Controls.Add(_platformSelect);

            _banner = new InfoBanner();
            _banner.Visible = false;
            Controls.Add(_banner);

            _statusPanel = new StatusPanel();
            Controls.Add(_statusPanel);

            _emptyCard = new EmptyCard();
            _emptyCard.Sub = "Sprocket scans C:\\, Program Files and Program Files (x86) for "
                + "<vendor>\\<install>\\bin\\wb.exe. Install Niagara, then rescan with the button above.";
            _emptyCard.Visible = false;
            Controls.Add(_emptyCard);

            _launchButton = new HeroButton();
            _launchButton.Text = "Launch Workbench";
            _launchButton.Click += delegate { LaunchSelected(ProcessLauncher.LaunchWorkbench); };
            ContextMenuStrip launchMenu = new ContextMenuStrip();
            launchMenu.Items.Add("Launch Workbench (with console)", null,
                delegate { LaunchSelected(ProcessLauncher.LaunchWorkbenchWithConsole); });
            _launchButton.ContextMenuStrip = launchMenu;
            Controls.Add(_launchButton);
            _tips.SetToolTip(_launchButton, "Right-click for Workbench with console");

            _daemonButton = new HeroButton();
            _daemonButton.Text = "Daemon";
            _daemonButton.Click += DaemonButtonClicked;
            Controls.Add(_daemonButton);

            _quickLabel = new Label();
            _quickLabel.Text = "Quick actions";
            _quickLabel.ForeColor = SprocketTheme.TextSecondary;
            _quickLabel.BackColor = Color.Transparent;
            _quickLabel.Font = new Font(SprocketTheme.BodyFamily, 8F);
            _quickLabel.AutoSize = true;
            Controls.Add(_quickLabel);

            _alarmTile = MakeTile("Alarm Portal", 0xEA8F);          // Ringer
            _alarmTile.Click += delegate { LaunchSelected(ProcessLauncher.LaunchAlarmPortal); };

            _consoleTile = MakeTile("Console", 0xE756);             // CommandPrompt
            _consoleTile.Click += delegate { LaunchSelected(ProcessLauncher.LaunchConsole); };

            _openFolderTile = MakeTile("Open Folder", 0xE838);      // OpenFolderHorizontal
            _openFolderTile.Click += delegate { LaunchSelected(ProcessLauncher.OpenInstallFolder); };

            _memoryTile = MakeTile("Memory", 0xE713);                // Settings
            _memoryTile.Click += MemoryTileClicked;

            _modulesTile = MakeTile("Modules", "");
            _modulesTile.Highlighted = true;
            _modulesTile.CustomIconPaint = PaintModulesGlyph;
            _modulesTile.Click += ModulesTileClicked;

            _installDaemonTile = MakeTile("Install Daemon", 0xE7EF); // Admin
            _installDaemonTile.Click += delegate
            {
                NiagaraPlatform p = SelectedPlatform;
                if (p == null || _opPhase != null) return;
                SaveLastUsed(p);
                StartRegisterDaemon(p);
            };

            _importNavTile = MakeTile("Import Nav", 0xE896);         // Download
            _importNavTile.Click += ImportNavTreeClicked;

            _exportNavTile = MakeTile("Export Nav", 0xE898);         // Upload
            _exportNavTile.Click += ExportNavTreeClicked;

            _themeTile = MakeTile("Theme", 0xE771);                  // Color
            _themeTile.Click += ThemeTileClicked;

            _tiles = new IconTile[]
            {
                _alarmTile, _consoleTile, _openFolderTile, _memoryTile,
                _modulesTile, _installDaemonTile, _importNavTile, _exportNavTile,
                _themeTile
            };

            // footer
            _footerHairline = new Panel();
            _footerHairline.BackColor = SprocketTheme.Hairline;
            Controls.Add(_footerHairline);

            _versionLabel = new Label();
            _versionLabel.Text = "v" + AppVersion.Display;
            _versionLabel.ForeColor = SprocketTheme.TextTertiary;
            _versionLabel.BackColor = Color.Transparent;
            _versionLabel.Font = new Font(SprocketTheme.BodyFamily, 7.5F);
            _versionLabel.AutoSize = true;
            Controls.Add(_versionLabel);

            _updateLink = new LinkLabel();
            _updateLink.Text = "";
            _updateLink.Visible = false;
            _updateLink.AutoSize = true;
            _updateLink.BackColor = Color.Transparent;
            _updateLink.LinkColor = SprocketTheme.AccentDeep;
            _updateLink.ActiveLinkColor = SprocketTheme.Accent;
            _updateLink.VisitedLinkColor = SprocketTheme.AccentDeep;
            _updateLink.LinkBehavior = LinkBehavior.HoverUnderline;
            _updateLink.Font = new Font(SprocketTheme.BodyFamily, 7.5F);
            _updateLink.Click += delegate
            {
                string url = _updateLink.Tag as string;
                if (!string.IsNullOrEmpty(url)) ProcessLauncher.OpenUrl(url);
            };
            Controls.Add(_updateLink);

            _trayIcon = new NotifyIcon();
            _trayIcon.Text = "Sprocket";
            ContextMenuStrip trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Show Sprocket", null, delegate { RestoreFromTray(); });
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("Exit", null, delegate { _trayIcon.Visible = false; Close(); });
            _trayIcon.ContextMenuStrip = trayMenu;
            _trayIcon.DoubleClick += delegate { RestoreFromTray(); };

            Resize += delegate { DoLayout(); };
            DoLayout();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized && _trayIcon != null)
            {
                ShowInTaskbar = false;
                _trayIcon.Visible = true;
            }
        }

        private void RestoreFromTray()
        {
            ShowInTaskbar = true;
            WindowState = FormWindowState.Normal;
            Show();
            Activate();
            if (_trayIcon != null) _trayIcon.Visible = false;
        }

        /// <summary>Keeps the tray icon's hover tooltip glanceable while minimized — mirrors the
        /// status card. NotifyIcon.Text throws past 63 chars on some Framework builds, so truncate.</summary>
        private void UpdateTrayTooltip()
        {
            if (_trayIcon == null) return;
            NiagaraPlatform p = SelectedPlatform;
            string text = p == null ? "Sprocket" : "Sprocket — " + p.DisplayName + " (" + _statusPanel.StateText + ")";
            if (text.Length > 63) text = text.Substring(0, 60) + "...";
            _trayIcon.Text = text;
        }

        private IconTile MakeTile(string text, int glyphCodepoint)
        {
            return MakeTile(text, SprocketTheme.Glyph(glyphCodepoint));
        }

        private IconTile MakeTile(string text, string glyph)
        {
            IconTile t = new IconTile();
            t.Text = text;
            t.Glyph = glyph;
            Controls.Add(t);
            return t;
        }

        private static void PaintModulesGlyph(Graphics g, Rectangle rect, Color color)
        {
            const int cell = 6;
            const int gap = 3;
            int totalW = cell * 2 + gap;
            int startX = rect.X + (rect.Width - totalW) / 2;
            int startY = rect.Y + (rect.Height - totalW) / 2;
            using (SolidBrush b = new SolidBrush(color))
            {
                g.FillRectangle(b, startX, startY, cell, cell);
                g.FillRectangle(b, startX + cell + gap, startY, cell, cell);
                g.FillRectangle(b, startX, startY + cell + gap, cell, cell);
                g.FillRectangle(b, startX + cell + gap, startY + cell + gap, cell, cell);
            }
        }

        private void DoLayout()
        {
            int w = ClientSize.Width - Gutter * 2;
            if (w < 100) return;

            // header
            _gear.SetBounds(Gutter, 24, 38, 38);
            _title.SetBounds(Gutter + 50, 18, w - 50 - 150, 26);
            _subtitle.SetBounds(Gutter + 51, 46, w - 51 - 150, 16);
            _refreshButton.SetBounds(ClientSize.Width - Gutter - 34, 24, 34, 34);
            _foldersButton.SetBounds(ClientSize.Width - Gutter - 34 * 2 - 8, 24, 34, 34);

            int y = 76;
            _platformSelect.SetBounds(Gutter, y, w, 36);
            _emptyCard.SetBounds(Gutter, y, w, 150);
            y += 36 + 10;

            if (_banner.Visible)
            {
                _banner.SetBounds(Gutter, y, w, 42);
                y += 42 + 10;
            }

            _statusPanel.SetBounds(Gutter, y, w, 150);
            y += 150 + 14;

            int rowW = w - 8;
            int launchW = (int)(rowW * 1.6 / 2.6);
            int daemonW = rowW - launchW;
            _launchButton.SetBounds(Gutter, y, launchW, 38);
            _daemonButton.SetBounds(Gutter + launchW + 8, y, daemonW, 38);
            y += 38 + 20;

            _quickLabel.Location = new Point(Gutter + 2, y);
            y += 20;

            const int cols = 4;
            const int gap = 8;
            const int tileH = 56;
            int tileW = (w - (cols - 1) * gap) / cols;
            for (int i = 0; i < _tiles.Length; i++)
            {
                int col = i % cols;
                int row = i / cols;
                _tiles[i].SetBounds(Gutter + col * (tileW + gap), y + row * (tileH + gap), tileW, tileH);
            }
            int rows = (_tiles.Length + cols - 1) / cols;
            y += rows * (tileH + gap) - gap;

            int footerY = Math.Max(y + 16, ClientSize.Height - 46);
            _footerHairline.SetBounds(Gutter, footerY, w, 1);
            _versionLabel.Location = new Point(
                ClientSize.Width - Gutter - _versionLabel.PreferredWidth, footerY + 11);
            _updateLink.Location = new Point(Gutter, footerY + 11);
        }

        // ---------------------------------------------------------- scanning

        private void Rescan()
        {
            _platforms = PlatformScanner.Scan();
            _platformSelect.ScanRoots = new List<string>(UserSettings.Load().Folders);

            _platformSelect.Items.Clear();
            foreach (NiagaraPlatform p in _platforms)
                _platformSelect.Items.Add(p);

            bool any = _platforms.Count > 0;
            _platformSelect.Visible = any;
            _statusPanel.Visible = any;
            _launchButton.Visible = any;
            _daemonButton.Visible = any;
            _quickLabel.Visible = any;
            for (int i = 0; i < _tiles.Length; i++)
                _tiles[i].Visible = any;
            _emptyCard.Visible = !any;
            if (!any) _banner.Visible = false;

            RefreshGlobalDaemonState();

            if (any)
            {
                int idx = FindLastUsedIndex();
                _platformSelect.SelectedIndex = idx >= 0 ? idx : 0;
                UpdateForSelection();
            }
            else
            {
                DoLayout();
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

        private static bool SamePlatform(NiagaraPlatform a, NiagaraPlatform b)
        {
            if (a == null || b == null) return false;
            return string.Equals(a.InstallDir, b.InstallDir, StringComparison.OrdinalIgnoreCase);
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

            UpdateDaemonUi();

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

        // ------------------------------------------------------ daemon (Feature 2)

        private void RefreshGlobalDaemonState()
        {
            _registeredPlatform = null;
            _runningPlatform = null;

            // One service, so resolve it once and match it to a platform, rather than querying
            // sc.exe per install (12 installs on a well-used engineering box = 12 processes).
            string registeredDir = DaemonStatus.RegisteredInstallDir();
            if (registeredDir == null) return;

            foreach (NiagaraPlatform p in _platforms)
            {
                if (string.Equals(p.InstallDir.TrimEnd('\\'), registeredDir.TrimEnd('\\'),
                        StringComparison.OrdinalIgnoreCase))
                {
                    _registeredPlatform = p;
                    break;
                }
            }
            if (_registeredPlatform == null) return;

            DaemonState st = DaemonStatus.Query(_registeredPlatform);
            if (st == DaemonState.Running || st == DaemonState.Starting || st == DaemonState.Stopping)
                _runningPlatform = _registeredPlatform;
        }

        private void UpdateDaemonUi()
        {
            NiagaraPlatform p = SelectedPlatform;
            if (p == null) return;

            bool selIsRunning = SamePlatform(p, _runningPlatform);
            bool somethingElseRunning = _runningPlatform != null && !selIsRunning;
            bool involvedInOp = _opPhase != null && (SamePlatform(p, _opFrom) || SamePlatform(p, _opTo));

            // status chip
            if (involvedInOp)
            {
                bool stoppingHere = _opPhase == "stopping" && SamePlatform(p, _opFrom);
                _statusPanel.StateText = stoppingHere ? "STOPPING…" : "STARTING…";
                _statusPanel.StateColor = SprocketTheme.Pending;
                _statusPanel.StateBg = SprocketTheme.PendingTintBg;
                _statusPanel.StateBorder = SprocketTheme.PendingTintBorder;
            }
            else if (selIsRunning)
            {
                _statusPanel.StateText = "RUNNING";
                _statusPanel.StateColor = SprocketTheme.Success;
                _statusPanel.StateBg = SprocketTheme.SuccessTintBg;
                _statusPanel.StateBorder = SprocketTheme.SuccessTintBorder;
            }
            else
            {
                _statusPanel.StateText = "STOPPED";
                _statusPanel.StateColor = SprocketTheme.Danger;
                _statusPanel.StateBg = SprocketTheme.DangerTintBg;
                _statusPanel.StateBorder = SprocketTheme.DangerTintBorder;
            }
            _statusPanel.Invalidate();

            // dropdown pill: status dot + "DAEMON ON x.y.z" hint
            _platformSelect.ShowStatusDot = true;
            _platformSelect.StatusDotColor = (selIsRunning && _opPhase == null)
                ? SprocketTheme.Success : SprocketTheme.TextTertiary;
            _platformSelect.Hint = (somethingElseRunning && _opPhase == null)
                ? "DAEMON ON " + _runningPlatform.DisplayName : "";
            _platformSelect.RunningItem = _runningPlatform;
            _platformSelect.Invalidate();

            // banner
            bool bannerWasVisible = _banner.Visible;
            if (_opPhase != null)
            {
                if (_opPhase == "registering")
                    _banner.Message = "Re-registering the Niagara service on " + _opTo.DisplayName
                        + "… approve the administrator prompt if it appears.";
                else if (_opPhase == "stopping")
                    _banner.Message = "Stopping daemon on " + _opFrom.DisplayName + "…";
                else
                    _banner.Message = "Starting daemon on " + _opTo.DisplayName + "…";
                _banner.StepText = _opTwoStep ? (_opPhase == "stopping" ? "STEP 1 OF 2" : "STEP 2 OF 2") : "";
                _banner.Visible = true;
            }
            else if (_registeredPlatform != null && !SamePlatform(p, _registeredPlatform))
            {
                // Windows allows only one registered Niagara daemon, so say so plainly rather than
                // implying this platform has its own service sitting there stopped.
                _banner.Message = "The Niagara service belongs to " + _registeredPlatform.DisplayName
                    + (_runningPlatform != null ? " (running)" : " (stopped)")
                    + ". Moving it here replaces that registration — one click does both.";
                _banner.StepText = "";
                _banner.Visible = true;
            }
            else
            {
                _banner.Visible = false;
            }
            _banner.Invalidate();

            // Daemon button.
            //
            // Only the platform that owns the single Niagara service can be started/stopped
            // directly. For any other platform there is exactly one meaningful action — re-register
            // the service against it — and that same action covers both "no daemon installed at
            // all" and "the daemon currently belongs to a different install". The old code split
            // those into an "Install daemon" path and a "Switch daemon here" path, and the latter
            // could never work: it stopped the running service and then tried to start a service
            // for the target that had never existed.
            bool ownsService = SamePlatform(p, _registeredPlatform);
            if (_opPhase != null)
            {
                _daemonButton.Enabled = false;
                _daemonButton.Text = involvedInOp
                    ? (_opPhase == "registering" ? "Switching…" : (_opPhase == "stopping" ? "Stopping…" : "Starting…"))
                    : "Daemon";
                _daemonButton.FillColor = SprocketTheme.PendingTintBg;
                _daemonButton.FillHoverColor = SprocketTheme.PendingTintBg;
                _daemonButton.TextColor = SprocketTheme.Pending;
                _daemonButton.BorderColor = SprocketTheme.PendingTintBorder;
            }
            else if (!ownsService)
            {
                bool canInstall = p.HasPlatDaemonInstaller;
                _daemonButton.Enabled = canInstall;
                _daemonButton.Text = !canInstall
                    ? "Daemon"
                    : (_registeredPlatform == null ? "Install daemon here" : "⇄ Move daemon here");
                _daemonButton.FillColor = canInstall ? SprocketTheme.PendingTintBg : SprocketTheme.CardBg;
                _daemonButton.FillHoverColor = canInstall ? SprocketTheme.PendingTintBg : SprocketTheme.CardBg;
                _daemonButton.TextColor = canInstall ? SprocketTheme.Pending : SprocketTheme.TextTertiary;
                _daemonButton.BorderColor = canInstall ? SprocketTheme.PendingTintBorder : SprocketTheme.FieldBorder;
            }
            else if (selIsRunning)
            {
                _daemonButton.Enabled = true;
                _daemonButton.Text = "Stop daemon";
                _daemonButton.FillColor = SprocketTheme.CardBg;
                _daemonButton.FillHoverColor = SprocketTheme.FieldHoverBg;
                _daemonButton.TextColor = SprocketTheme.Danger;
                _daemonButton.BorderColor = SprocketTheme.FieldBorder;
            }
            else
            {
                _daemonButton.Enabled = true;
                _daemonButton.Text = "Start daemon";
                _daemonButton.FillColor = SprocketTheme.CardBg;
                _daemonButton.FillHoverColor = SprocketTheme.FieldHoverBg;
                _daemonButton.TextColor = SprocketTheme.Success;
                _daemonButton.BorderColor = SprocketTheme.FieldBorder;
            }
            _daemonButton.Invalidate();
            UpdateTrayTooltip();

            if (bannerWasVisible != _banner.Visible) DoLayout();
        }

        private void DaemonButtonClicked(object sender, EventArgs e)
        {
            NiagaraPlatform p = SelectedPlatform;
            if (p == null || _opPhase != null) return;

            if (!SamePlatform(p, _registeredPlatform))
            {
                StartRegisterDaemon(p);
                return;
            }

            StartSimpleToggle(p, !SamePlatform(p, _runningPlatform));
        }

        /// <summary>
        /// Makes <paramref name="p"/> the owner of the machine's single Niagara daemon service.
        ///
        /// plat.exe installdaemon does the whole job in one elevated step — it stops and deletes an
        /// existing daemon service belonging to another install, then creates and starts this one —
        /// so there is no stop-then-start dance to sequence here, and no window where the machine is
        /// left with no daemon because step 2 failed.
        /// </summary>
        private void StartRegisterDaemon(NiagaraPlatform p)
        {
            if (!p.HasPlatDaemonInstaller) return;

            string question = (_registeredPlatform == null)
                ? "Register the Niagara platform daemon for:\r\n\r\n    " + p.DisplayName
                    + "\r\n\r\nThis installs the Windows service and starts it."
                : "Move the Niagara platform daemon from:\r\n\r\n    " + _registeredPlatform.DisplayName
                    + "\r\n\r\nto:\r\n\r\n    " + p.DisplayName
                    + "\r\n\r\nWindows allows only one registered Niagara daemon, so the existing one "
                    + "is stopped and unregistered first. Any station it is running will go down.";

            if (MessageBox.Show(question + "\r\n\r\nWindows will ask for administrator approval.",
                    "Sprocket", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
                return;

            _opPhase = "registering";
            _opFrom = _registeredPlatform;
            _opTo = p;
            _opTwoStep = false;
            UpdateDaemonUi();

            Thread t = new Thread(delegate()
            {
                DaemonOpResult result = DaemonStatus.InstallDaemon(p);

                // installdaemon starts the service itself, but it returns as soon as the start is
                // initiated — wait for it to settle so the UI shows the real end state.
                if (result.Ok)
                {
                    string svc = DaemonStatus.ServiceNameFor(p);
                    if (svc != null) DaemonStatus.WaitForSettled(svc, 60000);
                }

                if (IsDisposed) return;
                BeginInvoke((MethodInvoker)delegate
                {
                    if (IsDisposed) return;
                    _opPhase = null; _opFrom = null; _opTo = null; _opTwoStep = false;

                    if (!result.Ok && !result.Cancelled)
                    {
                        MessageBox.Show(
                            "Couldn't register the daemon on " + p.DisplayName + ".\r\n\r\n"
                            + result.FriendlyError,
                            "Sprocket", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }

                    RefreshGlobalDaemonState();
                    UpdateDaemonUi();
                });
            });
            t.IsBackground = true;
            t.Start();
        }

        private void StartSimpleToggle(NiagaraPlatform p, bool turnOn)
        {
            string serviceName = DaemonStatus.ServiceNameFor(p);

            _opPhase = turnOn ? "starting" : "stopping";
            _opFrom = turnOn ? null : p;
            _opTo = turnOn ? p : null;
            _opTwoStep = false;
            UpdateDaemonUi();

            Thread t = new Thread(delegate()
            {
                bool ok = DaemonStatus.SetRunning(serviceName, turnOn);
                if (ok) DaemonStatus.WaitForSettled(serviceName, 45000);

                if (IsDisposed) return;
                BeginInvoke((MethodInvoker)delegate
                {
                    if (IsDisposed) return;
                    _opPhase = null; _opFrom = null; _opTo = null; _opTwoStep = false;
                    if (!ok)
                    {
                        MessageBox.Show(
                            "Couldn't " + (turnOn ? "start" : "stop") + " the daemon service.",
                            "Sprocket", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    RefreshGlobalDaemonState();
                    UpdateDaemonUi();
                });
            });
            t.IsBackground = true;
            t.Start();
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

        private void AddRootClicked()
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Pick a folder that contains Niagara installs "
                    + "(the folder itself, or <folder>\\<install>\\bin\\wb.exe).";
                dlg.ShowNewFolderButton = false;

                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                string folder = dlg.SelectedPath.TrimEnd(Path.DirectorySeparatorChar);
                UserSettings settings = UserSettings.Load();
                if (UserSettings.ContainsIgnoreCase(settings.Folders, folder)) return;

                settings.Folders.Add(folder);
                settings.Save();
                Rescan();
            }
        }

        private void RemoveRootClicked(string folder)
        {
            UserSettings settings = UserSettings.Load();
            settings.Folders.Remove(folder);
            settings.Save();
            Rescan();
        }

        private void MemoryTileClicked(object sender, EventArgs e)
        {
            NiagaraPlatform p = SelectedPlatform;
            if (p == null) return;
            using (MemorySettingsForm dlg = new MemorySettingsForm(p))
                dlg.ShowDialog(this);
        }

        private void ModulesTileClicked(object sender, EventArgs e)
        {
            NiagaraPlatform p = SelectedPlatform;
            if (p == null) return;
            using (ModulesForm dlg = new ModulesForm(_platforms, p))
                dlg.ShowDialog(this);
        }

        private void ThemeTileClicked(object sender, EventArgs e)
        {
            NiagaraPlatform p = SelectedPlatform;
            if (p == null) return;
            using (ThemeForm dlg = new ThemeForm(p))
                dlg.ShowDialog(this);
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
    }
}
