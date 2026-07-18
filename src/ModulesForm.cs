using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Sprocket
{
    /// <summary>Feature 3 — module manager: diff a source platform's \modules against a target and
    /// copy the missing jars across.</summary>
    internal sealed class ModulesForm : Form, IAuroraHost
    {
        private const int Pad = 24;

        private sealed class ModuleRow
        {
            public string FileName;
            public string FullPath;
            public string Version;
            public string Size;
            public bool ExistsOnTarget;
            public bool Copied;
        }

        private readonly Backdrop _backdrop = new Backdrop();
        private readonly List<NiagaraPlatform> _platforms;
        private readonly List<ModuleRow> _rows = new List<ModuleRow>();
        private readonly HashSet<string> _checked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _copiedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private Label _titleLabel;
        private Label _sourceLabel;
        private Label _targetLabel;
        private PillSelect _source;
        private PillSelect _target;
        private Label _arrowLabel;
        private PillTextBox _filterBox;

        private Label _colModule;
        private Label _colVersion;
        private Label _colSize;
        private Label _colTarget;

        private Panel _tableBorder;
        private Panel _scrollPanel;
        private ModuleRowsCanvas _canvas;

        private TextGhostButton _addJarButton;
        private Label _summaryLabel;
        private Label _copyNoteLabel;
        private HeroButton _copyButton;
        private Label _footnoteLabel;

        public ModulesForm(List<NiagaraPlatform> platforms, NiagaraPlatform initialSource)
        {
            HandleCreated += delegate { DwmUtil.RequestRoundedCorners(this); };
            _platforms = platforms;
            BuildUi();

            if (initialSource != null) SelectByPlatform(_source, initialSource);
            else if (_source.Items.Count > 0) _source.SelectedIndex = 0;

            PopulateTarget();
            RebuildRows();
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

        // ---------------------------------------------------------------- UI

        private void BuildUi()
        {
            Text = "Sprocket — Modules";
            ClientSize = new Size(720, 560);
            MinimumSize = new Size(640, 420);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = SprocketTheme.WindowBg;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw, true);

            _titleLabel = new Label();
            _titleLabel.Text = "Modules";
            _titleLabel.ForeColor = SprocketTheme.TextPrimary;
            _titleLabel.BackColor = Color.Transparent;
            _titleLabel.Font = new Font(SprocketTheme.HeadingFamily, 15F, FontStyle.Bold);
            Controls.Add(_titleLabel);

            _sourceLabel = new Label();
            _sourceLabel.Text = "Modules on";
            _sourceLabel.ForeColor = SprocketTheme.TextTertiary;
            _sourceLabel.BackColor = Color.Transparent;
            _sourceLabel.Font = new Font(SprocketTheme.BodyFamily, 7.5F);
            Controls.Add(_sourceLabel);

            _targetLabel = new Label();
            _targetLabel.Text = "Compare / copy to";
            _targetLabel.ForeColor = SprocketTheme.TextTertiary;
            _targetLabel.BackColor = Color.Transparent;
            _targetLabel.Font = new Font(SprocketTheme.BodyFamily, 7.5F);
            Controls.Add(_targetLabel);

            _source = new PillSelect();
            foreach (NiagaraPlatform p in _platforms) _source.Items.Add(p);
            _source.SelectedIndexChanged += delegate { PopulateTarget(); RebuildRows(); };
            Controls.Add(_source);

            _arrowLabel = new Label();
            _arrowLabel.Text = "→";
            _arrowLabel.ForeColor = SprocketTheme.TextTertiary;
            _arrowLabel.BackColor = Color.Transparent;
            _arrowLabel.Font = new Font(SprocketTheme.BodyFamily, 12F);
            _arrowLabel.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(_arrowLabel);

            _target = new PillSelect();
            _target.SelectedIndexChanged += delegate { _copiedFiles.Clear(); RecomputeDiff(); };
            Controls.Add(_target);

            _filterBox = new PillTextBox();
            _filterBox.PlaceholderText = "Filter modules…";
            _filterBox.Input.TextChanged += delegate { RefreshTable(); };
            Controls.Add(_filterBox);

            _colModule = MakeColHeader("Module", HorizontalAlignment.Left);
            _colVersion = MakeColHeader("Version", HorizontalAlignment.Left);
            _colSize = MakeColHeader("Size", HorizontalAlignment.Left);
            _colTarget = MakeColHeader("On target", HorizontalAlignment.Right);

            _tableBorder = new Panel();
            _tableBorder.BackColor = SprocketTheme.CardBorder;
            Controls.Add(_tableBorder);

            _scrollPanel = new Panel();
            _scrollPanel.AutoScroll = true;
            _scrollPanel.BackColor = SprocketTheme.CardBg;
            _scrollPanel.Resize += delegate
            {
                _canvas.Width = _scrollPanel.ClientSize.Width;
                _canvas.Invalidate();
            };
            Controls.Add(_scrollPanel);
            _scrollPanel.BringToFront();

            _canvas = new ModuleRowsCanvas();
            _canvas.RowToggled += OnRowToggled;
            _scrollPanel.Controls.Add(_canvas);
            _canvas.BringToFront();

            _addJarButton = new TextGhostButton();
            _addJarButton.Text = "Add jar…";
            _addJarButton.Glyph = SprocketTheme.Glyph(0xE710);
            _addJarButton.Click += AddJarClicked;
            Controls.Add(_addJarButton);

            _summaryLabel = new Label();
            _summaryLabel.ForeColor = SprocketTheme.TextTertiary;
            _summaryLabel.BackColor = Color.Transparent;
            _summaryLabel.Font = new Font(SprocketTheme.BodyFamily, 8.25F);
            _summaryLabel.AutoSize = false;
            _summaryLabel.AutoEllipsis = true;
            Controls.Add(_summaryLabel);

            _copyNoteLabel = new Label();
            _copyNoteLabel.Text = "";
            _copyNoteLabel.ForeColor = SprocketTheme.Success;
            _copyNoteLabel.BackColor = Color.Transparent;
            _copyNoteLabel.Font = new Font(SprocketTheme.BodyFamily, 8.25F, FontStyle.Bold);
            _copyNoteLabel.AutoSize = true;
            Controls.Add(_copyNoteLabel);

            _copyButton = new HeroButton();
            _copyButton.Click += CopyClicked;
            Controls.Add(_copyButton);

            _footnoteLabel = new Label();
            _footnoteLabel.Text = "Copies the jar into the target's \\modules folder… "
                + "Run Software Manager in Workbench afterwards to install into a station.";
            _footnoteLabel.ForeColor = SprocketTheme.TextTertiary;
            _footnoteLabel.BackColor = Color.Transparent;
            _footnoteLabel.Font = new Font(SprocketTheme.BodyFamily, 7.75F);
            Controls.Add(_footnoteLabel);

            Resize += delegate { DoLayout(); };
            Load += delegate { DoLayout(); RefreshTable(); };
            // ClientSize can still settle after Load (window chrome/DPI finalize once actually on
            // screen) — Shown fires later and catches the real final size.
            Shown += delegate { DoLayout(); RefreshTable(); };
            DoLayout();
        }

        private Label MakeColHeader(string text, HorizontalAlignment align)
        {
            Label l = new Label();
            l.AutoSize = false; // fixed box needed for TextAlign to mean anything
            l.Text = text;
            l.ForeColor = SprocketTheme.TextTertiary;
            l.BackColor = Color.Transparent;
            l.Font = new Font(SprocketTheme.BodyFamily, 7.5F, FontStyle.Bold);
            l.TextAlign = align == HorizontalAlignment.Right ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft;
            Controls.Add(l);
            return l;
        }

        private void DoLayout()
        {
            int w = ClientSize.Width - Pad * 2;
            if (w < 200) return;

            int y = Pad;
            _titleLabel.SetBounds(Pad, y, w, 24);
            y += 34;

            int filterW = Math.Min(190, (int)(w * 0.26));
            int arrowW = 20;
            int gap = 14;
            int comboW = (w - filterW - arrowW - gap * 3) / 2;
            if (comboW < 80) comboW = 80;

            int targetX = Pad + comboW + gap + arrowW + gap;
            _sourceLabel.SetBounds(Pad, y, comboW, 14);
            _targetLabel.SetBounds(targetX, y, comboW, 14);
            y += 16;

            _source.SetBounds(Pad, y, comboW, 38);
            _arrowLabel.SetBounds(Pad + comboW + gap, y, arrowW, 38);
            _target.SetBounds(targetX, y, comboW, 38);
            _filterBox.SetBounds(targetX + comboW + gap, y, filterW, 36);
            y += 38 + 18;

            int checkboxColW = 40;
            int nameColW = (int)(w * 0.42);
            int verColW = (int)(w * 0.18);
            int sizeColW = (int)(w * 0.12);
            _colModule.SetBounds(Pad + checkboxColW, y, nameColW, 16);
            _colVersion.SetBounds(Pad + checkboxColW + nameColW, y, verColW, 16);
            _colSize.SetBounds(Pad + checkboxColW + nameColW + verColW, y, sizeColW, 16);
            _colTarget.SetBounds(Pad, y, w - 12, 16);
            y += 22;

            int tableTop = y;
            const int footerH = 122;
            int tableH = ClientSize.Height - tableTop - footerH;
            if (tableH < 80) tableH = 80;

            _tableBorder.SetBounds(Pad, tableTop, w, tableH);
            _scrollPanel.SetBounds(Pad + 1, tableTop + 1, w - 2, tableH - 2);
            _canvas.Width = _scrollPanel.ClientSize.Width;

            // Two footer rows: "Add jar…" + summary on one line, copy note + Copy button on the
            // next — the summary and copy-button labels can both run long (platform names), and a
            // single shared row doesn't reliably have room for all of it.
            y = tableTop + tableH + 12;
            _addJarButton.SetBounds(Pad, y, 96, 32);
            _summaryLabel.SetBounds(Pad + 106, y + 7, w - 106, 18);
            y += 32 + 10;

            int copyBtnW = Math.Min(260, w);
            _copyButton.SetBounds(ClientSize.Width - Pad - copyBtnW, y, copyBtnW, 34);
            _copyNoteLabel.Location = new Point(Pad, y + 9);

            y += 34 + 8;
            _footnoteLabel.SetBounds(Pad, y, w, 32);
        }

        // ------------------------------------------------------------- data

        private static bool SamePlatform(NiagaraPlatform a, NiagaraPlatform b)
        {
            if (a == null || b == null) return false;
            return string.Equals(a.InstallDir, b.InstallDir, StringComparison.OrdinalIgnoreCase);
        }

        private static void SelectByPlatform(PillSelect select, NiagaraPlatform p)
        {
            for (int i = 0; i < select.Items.Count; i++)
            {
                if (SamePlatform((NiagaraPlatform)select.Items[i], p)) { select.SelectedIndex = i; return; }
            }
        }

        private NiagaraPlatform SelectedSource
        {
            get { return _source.SelectedItem as NiagaraPlatform; }
        }

        private NiagaraPlatform SelectedTarget
        {
            get { return _target.SelectedItem as NiagaraPlatform; }
        }

        private void PopulateTarget()
        {
            NiagaraPlatform src = SelectedSource;
            NiagaraPlatform prevTarget = SelectedTarget;

            _target.Items.Clear();
            foreach (NiagaraPlatform p in _platforms)
            {
                if (SamePlatform(p, src)) continue;
                _target.Items.Add(p);
            }

            int idx = -1;
            if (prevTarget != null)
            {
                for (int i = 0; i < _target.Items.Count; i++)
                    if (SamePlatform((NiagaraPlatform)_target.Items[i], prevTarget)) { idx = i; break; }
            }
            _target.SelectedIndex = idx >= 0 ? idx : (_target.Items.Count > 0 ? 0 : -1);
            _target.Invalidate();
        }

        private void RebuildRows()
        {
            _rows.Clear();
            _checked.Clear();
            _copiedFiles.Clear();

            NiagaraPlatform src = SelectedSource;
            if (src != null && Directory.Exists(src.ModulesDir))
            {
                string[] jars;
                try { jars = Directory.GetFiles(src.ModulesDir, "*.jar"); }
                catch { jars = new string[0]; }
                Array.Sort(jars, StringComparer.OrdinalIgnoreCase);

                foreach (string jar in jars)
                {
                    ModuleRow row = new ModuleRow();
                    row.FileName = Path.GetFileName(jar);
                    row.FullPath = jar;
                    row.Version = ParseJarVersion(jar);
                    long size = 0;
                    try { size = new FileInfo(jar).Length; } catch { }
                    row.Size = FormatSize(size);
                    _rows.Add(row);
                }
            }

            RecomputeDiff();
        }

        private void RecomputeDiff()
        {
            NiagaraPlatform target = SelectedTarget;
            HashSet<string> targetFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (target != null && Directory.Exists(target.ModulesDir))
            {
                try
                {
                    foreach (string f in Directory.GetFiles(target.ModulesDir, "*.jar"))
                        targetFiles.Add(Path.GetFileName(f));
                }
                catch { }
            }

            foreach (ModuleRow row in _rows)
            {
                row.ExistsOnTarget = targetFiles.Contains(row.FileName);
                row.Copied = _copiedFiles.Contains(row.FileName);
            }

            _checked.Clear();
            RefreshTable();
        }

        private void RefreshTable()
        {
            string q = _filterBox.Input.Text == null ? "" : _filterBox.Input.Text.Trim().ToLowerInvariant();
            List<ModuleRow> visible = new List<ModuleRow>();
            foreach (ModuleRow r in _rows)
            {
                if (q.Length == 0 || r.FileName.ToLowerInvariant().IndexOf(q) >= 0)
                    visible.Add(r);
            }

            _canvas.Rows = visible;
            _canvas.Checked = _checked;
            _canvas.Width = _scrollPanel.ClientSize.Width;
            _canvas.RecalcHeight();
            // AutoScroll panels don't reliably pick up a child's Size before the form's window
            // handle exists (this can run during the constructor, pre-Show) — set the scrollable
            // region explicitly instead of relying on child-size auto-detection.
            _scrollPanel.AutoScrollMinSize = new Size(0, _canvas.Height);
            _canvas.Invalidate();

            UpdateFooter();
        }

        private void UpdateFooter()
        {
            NiagaraPlatform src = SelectedSource;
            NiagaraPlatform target = SelectedTarget;

            int missing = 0;
            foreach (ModuleRow r in _rows)
                if (!r.ExistsOnTarget && !r.Copied) missing++;

            string srcName = src != null ? src.DisplayName : "—";
            string targetName = target != null ? target.DisplayName : "—";
            _summaryLabel.Text = _rows.Count + " modules on " + srcName + " · " + missing + " missing on " + targetName;

            int n = _checked.Count;
            _copyButton.Enabled = n > 0 && target != null;
            _copyButton.Text = n > 0
                ? "Copy " + n + " module" + (n > 1 ? "s" : "") + " to " + targetName + " →"
                : "Copy to " + targetName + " →";

            if (n > 0)
            {
                _copyButton.FillColor = SprocketTheme.Accent;
                _copyButton.FillHoverColor = SprocketTheme.AccentHover;
                _copyButton.TextColor = Color.White;
                _copyButton.BorderColor = Color.Empty;
            }
            else
            {
                _copyButton.FillColor = SprocketTheme.CardBg;
                _copyButton.FillHoverColor = SprocketTheme.CardBg;
                _copyButton.TextColor = SprocketTheme.TextTertiary;
                _copyButton.BorderColor = SprocketTheme.FieldBorder;
            }
            _copyButton.Invalidate();
        }

        private void OnRowToggled(ModuleRow row)
        {
            if (_checked.Contains(row.FileName)) _checked.Remove(row.FileName);
            else _checked.Add(row.FileName);
            _copyNoteLabel.Text = "";
            _canvas.Invalidate();
            UpdateFooter();
        }

        private void AddJarClicked(object sender, EventArgs e)
        {
            NiagaraPlatform src = SelectedSource;
            if (src == null) return;

            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "Add Modules";
                dlg.Filter = "Niagara Modules (*.jar)|*.jar";
                dlg.Multiselect = true;

                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    if (!Directory.Exists(src.ModulesDir)) Directory.CreateDirectory(src.ModulesDir);
                    foreach (string f in dlg.FileNames)
                        File.Copy(f, Path.Combine(src.ModulesDir, Path.GetFileName(f)), true);
                    RebuildRows();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Couldn't copy modules:\r\n" + ex.Message, "Sprocket",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void CopyClicked(object sender, EventArgs e)
        {
            NiagaraPlatform target = SelectedTarget;
            if (target == null || _checked.Count == 0) return;

            HashSet<string> names = new HashSet<string>(_checked, StringComparer.OrdinalIgnoreCase);
            int copiedCount = 0;
            try
            {
                if (!Directory.Exists(target.ModulesDir)) Directory.CreateDirectory(target.ModulesDir);
                foreach (ModuleRow row in _rows)
                {
                    if (!names.Contains(row.FileName)) continue;
                    File.Copy(row.FullPath, Path.Combine(target.ModulesDir, row.FileName), true);
                    _copiedFiles.Add(row.FileName);
                    copiedCount++;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Couldn't copy modules:\r\n" + ex.Message, "Sprocket",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            _checked.Clear();
            RecomputeDiff();
            _copyNoteLabel.Text = copiedCount + " module" + (copiedCount != 1 ? "s" : "") + " copied to \\modules ✓";
            UpdateFooter();
        }

        // -------------------------------------------------------- jar parsing

        private static readonly Regex VersionInFileName = new Regex(@"\d+(\.\d+){1,3}");

        private static string ParseJarVersion(string jarPath)
        {
            string fromManifest = TryReadManifestVersion(jarPath);
            if (!string.IsNullOrEmpty(fromManifest)) return fromManifest;

            Match m = VersionInFileName.Match(Path.GetFileNameWithoutExtension(jarPath));
            if (m.Success) return m.Value;

            return "—";
        }

        private static string TryReadManifestVersion(string jarPath)
        {
            try
            {
                using (ZipArchive zip = ZipFile.OpenRead(jarPath))
                {
                    ZipArchiveEntry entry = zip.GetEntry("META-INF/MANIFEST.MF");
                    if (entry == null) return null;

                    using (Stream s = entry.Open())
                    using (StreamReader reader = new StreamReader(s))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            line = line.Trim();
                            if (line.StartsWith("Implementation-Version:", StringComparison.OrdinalIgnoreCase))
                                return line.Substring("Implementation-Version:".Length).Trim();
                            if (line.StartsWith("Bundle-Version:", StringComparison.OrdinalIgnoreCase))
                                return line.Substring("Bundle-Version:".Length).Trim();
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1024 * 1024) return (bytes / (1024.0 * 1024.0)).ToString("0.0") + " MB";
            if (bytes >= 1024) return (bytes / 1024.0).ToString("0") + " KB";
            return bytes + " B";
        }

        // ------------------------------------------------------------ canvas

        // Plain Control, not AuroraControl: this is a simple rectangular scroll surface (no rounded
        // corners), so it just paints a flat white fill rather than sampling the window backdrop.
        private sealed class ModuleRowsCanvas : Control
        {
            public List<ModuleRow> Rows = new List<ModuleRow>();
            public HashSet<string> Checked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public event Action<ModuleRow> RowToggled;

            private const int RowHeight = 30;
            private int _hotRow = -1;
            private readonly Font _nameFont;
            private readonly Font _metaFont;
            private readonly Font _badgeFont;

            public ModuleRowsCanvas()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw
                    | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
                BackColor = SprocketTheme.CardBg;
                _nameFont = new Font("Consolas", 9F);
                _metaFont = new Font(SprocketTheme.BodyFamily, 8.25F);
                _badgeFont = new Font(SprocketTheme.BodyFamily, 7F, FontStyle.Bold);
            }

            public void RecalcHeight()
            {
                Height = Math.Max(1, Rows.Count * RowHeight);
            }

            private Rectangle CheckboxRect(int i)
            {
                return new Rectangle(14, i * RowHeight + (RowHeight - 16) / 2, 16, 16);
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                int row = e.Y / RowHeight;
                if (row < 0 || row >= Rows.Count) row = -1;
                if (row != _hotRow) { _hotRow = row; Invalidate(); }
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                if (_hotRow != -1) { _hotRow = -1; Invalidate(); }
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                base.OnMouseClick(e);
                int row = e.Y / RowHeight;
                if (row < 0 || row >= Rows.Count) return;
                ModuleRow m = Rows[row];
                bool missing = !m.ExistsOnTarget && !m.Copied;
                if (!missing) return;
                if (RowToggled != null) RowToggled(m);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                for (int i = 0; i < Rows.Count; i++)
                {
                    ModuleRow m = Rows[i];
                    Rectangle row = new Rectangle(0, i * RowHeight, Width, RowHeight);
                    bool missing = !m.ExistsOnTarget && !m.Copied;
                    bool checkedOn = missing && Checked.Contains(m.FileName);

                    if (checkedOn)
                    {
                        using (SolidBrush fill = new SolidBrush(SprocketTheme.AccentTintBg))
                            g.FillRectangle(fill, row);
                    }
                    else if (i == _hotRow && missing)
                    {
                        using (SolidBrush fill = new SolidBrush(SprocketTheme.RowHoverBg))
                            g.FillRectangle(fill, row);
                    }

                    if (i > 0)
                        using (Pen sep = new Pen(SprocketTheme.Hairline, 1f))
                            g.DrawLine(sep, 0, row.Y, Width, row.Y);

                    Rectangle cb = CheckboxRect(i);
                    using (GraphicsPath cbPath = GdiUtil.RoundedRect(cb, 4))
                    {
                        Color boxBorder = missing
                            ? (checkedOn ? SprocketTheme.Accent : SprocketTheme.FieldBorder)
                            : SprocketTheme.TileBorder;
                        Color boxFill = checkedOn ? SprocketTheme.Accent : SprocketTheme.CardBg;
                        using (SolidBrush b = new SolidBrush(boxFill)) g.FillPath(b, cbPath);
                        using (Pen p = new Pen(boxBorder, 1f)) g.DrawPath(p, cbPath);
                    }
                    if (checkedOn)
                        TextRenderer.DrawText(g, "✓", _badgeFont, cb, Color.White,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

                    Color nameColor = missing ? SprocketTheme.TextPrimary : SprocketTheme.TextTertiary;
                    int nameColW = (int)(Width * 0.42);
                    int verColW = (int)(Width * 0.18);
                    int sizeColW = (int)(Width * 0.12);

                    TextRenderer.DrawText(g, m.FileName, _nameFont,
                        new Rectangle(40, row.Y, nameColW, RowHeight), nameColor,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                    TextRenderer.DrawText(g, m.Version, _metaFont,
                        new Rectangle(40 + nameColW, row.Y, verColW, RowHeight), SprocketTheme.TextSecondary,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                    TextRenderer.DrawText(g, m.Size, _metaFont,
                        new Rectangle(40 + nameColW + verColW, row.Y, sizeColW, RowHeight), SprocketTheme.TextSecondary,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                    string badgeText = m.Copied ? "COPIED ✓" : (m.ExistsOnTarget ? "INSTALLED" : "MISSING");
                    Color badgeColor = m.Copied ? SprocketTheme.Success
                        : (m.ExistsOnTarget ? SprocketTheme.TextTertiary : SprocketTheme.Pending);
                    Color badgeBg = m.Copied ? SprocketTheme.SuccessTintBg
                        : (m.ExistsOnTarget ? SprocketTheme.ChipBg : SprocketTheme.PendingTintBg);
                    Color badgeBorder = m.Copied ? SprocketTheme.SuccessTintBorder
                        : (m.ExistsOnTarget ? SprocketTheme.CardBorder : SprocketTheme.PendingTintBorder);
                    GdiUtil.DrawChipRight(g, Width - 12, row.Y + (RowHeight - 20) / 2, 20, badgeText, _badgeFont,
                        badgeColor, badgeBg, badgeBorder, false, Color.Empty);
                }
            }
        }
    }

    /// <summary>Rounded white field hosting a borderless TextBox (Fluent field cue), used for the
    /// module filter box.</summary>
    internal sealed class PillTextBox : AuroraPanel
    {
        public readonly TextBox Input;

        // Classic WinForms (not WinForms-on-.NET5+) has no TextBox.PlaceholderText, so this is
        // hand-rolled: a label drawn over the field, hidden once there's real text.
        private readonly Label _placeholder;

        public string PlaceholderText
        {
            get { return _placeholder.Text; }
            set { _placeholder.Text = value; }
        }

        public PillTextBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Height = 36;

            Input = new TextBox();
            Input.BorderStyle = BorderStyle.None;
            Input.BackColor = SprocketTheme.CardBg;
            Input.ForeColor = SprocketTheme.TextPrimary;
            Input.Font = new Font(SprocketTheme.BodyFamily, 9.5F);
            Input.TextChanged += delegate { UpdatePlaceholderVisibility(); };
            Controls.Add(Input);

            _placeholder = new Label();
            _placeholder.BackColor = Color.Transparent;
            _placeholder.ForeColor = SprocketTheme.TextTertiary;
            _placeholder.Font = Input.Font;
            _placeholder.TextAlign = ContentAlignment.MiddleLeft;
            _placeholder.Enabled = false;
            Controls.Add(_placeholder);
            _placeholder.BringToFront();

            LayoutInput();
            UpdatePlaceholderVisibility();
        }

        private void UpdatePlaceholderVisibility()
        {
            _placeholder.Visible = Input.Text.Length == 0;
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            LayoutInput();
        }

        private void LayoutInput()
        {
            if (Input == null) return;
            Input.SetBounds(13, (Height - Input.PreferredHeight) / 2, Width - 24, Input.PreferredHeight);
            if (_placeholder != null) _placeholder.SetBounds(14, 0, Width - 26, Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = GdiUtil.RoundedRect(rect, 5))
            {
                using (SolidBrush fill = new SolidBrush(SprocketTheme.CardBg))
                    g.FillPath(fill, path);
                using (Pen pen = new Pen(SprocketTheme.FieldBorder, 1f))
                    g.DrawPath(pen, path);
            }
            using (Pen bottomPen = new Pen(SprocketTheme.FieldBorderBottom, 1f))
                g.DrawLine(bottomPen, rect.X + 5, rect.Bottom, rect.Right - 5, rect.Bottom);
        }
    }
}
