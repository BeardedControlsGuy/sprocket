﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Sprocket
{
    internal static class GdiUtil
    {
        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.StartFigure();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static Color Lerp(Color a, Color b, float t)
        {
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        /// <summary>Right-aligned pill chip (status/version badges). Returns the chip's left edge,
        /// so callers can chain another chip immediately to its left.</summary>
        public static int DrawChipRight(Graphics g, int right, int y, int height, string text, Font font,
            Color textColor, Color bgColor, Color borderColor, bool showDot, Color dotColor)
        {
            Size textSize = TextRenderer.MeasureText(g, text, font, Size.Empty, TextFormatFlags.NoPadding);
            int chipW = textSize.Width + 22 + (showDot ? 12 : 0);
            Rectangle rect = new Rectangle(right - chipW, y, chipW, height);

            using (GraphicsPath pill = RoundedRect(rect, height / 2))
            {
                using (SolidBrush fill = new SolidBrush(bgColor))
                    g.FillPath(fill, pill);
                using (Pen pen = new Pen(borderColor, 1f))
                    g.DrawPath(pen, pill);
            }

            int textX = rect.X + 11;
            if (showDot)
            {
                using (SolidBrush dot = new SolidBrush(dotColor))
                    g.FillEllipse(dot, rect.X + 10, rect.Y + rect.Height / 2 - 3, 6, 6);
                textX = rect.X + 22;
            }

            TextRenderer.DrawText(g, text, font,
                new Rectangle(textX, rect.Y, rect.Width - (textX - rect.X) - 8, rect.Height), textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            return rect.X;
        }
    }

    /// <summary>Caches nothing — the window background is now a flat Fluent-light fill — but keeps the
    /// same slice-painting shape so children don't need per-control transparency handling.</summary>
    internal sealed class Backdrop : IDisposable
    {
        public void Paint(Graphics g, Size size)
        {
            if (size.Width < 1 || size.Height < 1) return;
            using (SolidBrush b = new SolidBrush(SprocketTheme.WindowBg))
                g.FillRectangle(b, 0, 0, size.Width, size.Height);
        }

        /// <summary>Paint the flat window background under a child (offset is irrelevant for a flat fill,
        /// but kept in the signature so call sites don't need to change).</summary>
        public void PaintSlice(Graphics g, Size hostSize, Point offset)
        {
            if (hostSize.Width < 1 || hostSize.Height < 1) return;
            using (SolidBrush b = new SolidBrush(SprocketTheme.WindowBg))
                g.FillRectangle(b, 0, 0, hostSize.Width, hostSize.Height);
        }

        public void Dispose() { }
    }

    /// <summary>A form that owns a flat backdrop children can sample from.</summary>
    internal interface IAuroraHost
    {
        void PaintAuroraSlice(Graphics g, Control child);
    }

    internal static class AuroraBg
    {
        /// <summary>Manual "transparency": paints the owning form's flat background under a control.
        /// (WinForms' simulated transparency composites garbage under double-buffered controls, and this
        /// also keeps rounded-corner anti-aliasing correct against the window background.)</summary>
        public static void Paint(Control c, Graphics g)
        {
            Form form = c.FindForm();
            IAuroraHost host = form as IAuroraHost;
            if (host != null)
            {
                host.PaintAuroraSlice(g, c);
                return;
            }
            using (SolidBrush b = new SolidBrush(SprocketTheme.WindowBg))
                g.FillRectangle(b, 0, 0, c.Width, c.Height);
        }
    }

    internal abstract class AuroraControl : Control
    {
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            AuroraBg.Paint(this, e.Graphics);
        }
    }

    internal abstract class AuroraButton : Button
    {
        protected AuroraButton()
        {
            // ButtonBase sets ControlStyles.Opaque, which suppresses OnPaintBackground and
            // leaves stale garbage in the double buffer — clear it so the backdrop paints.
            SetStyle(ControlStyles.Opaque, false);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            AuroraBg.Paint(this, e.Graphics);
        }
    }

    internal abstract class AuroraPanel : Panel
    {
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            AuroraBg.Paint(this, e.Graphics);
        }
    }

    /// <summary>Eased hover progress (0..1) driving smooth color transitions.</summary>
    internal sealed class HoverFx
    {
        private readonly Control _owner;
        private readonly Timer _timer;
        private float _value;
        private float _target;

        public HoverFx(Control owner)
        {
            _owner = owner;
            _timer = new Timer();
            _timer.Interval = 25;
            _timer.Tick += Tick;
            owner.MouseEnter += delegate { Animate(1f); };
            owner.MouseLeave += delegate { Animate(0f); };
            owner.Disposed += delegate { _timer.Dispose(); };
        }

        public float Value { get { return _value; } }

        public void Animate(float target)
        {
            _target = target;
            _timer.Start();
        }

        private void Tick(object sender, EventArgs e)
        {
            _value += (_target - _value) * 0.35f;
            if (Math.Abs(_target - _value) < 0.02f)
            {
                _value = _target;
                _timer.Stop();
            }
            _owner.Invalidate();
        }
    }

    /// <summary>Primary CTA / secondary action button — flat pill, radius 5, hover lightens
    /// (no gradient/shimmer). Colors are configurable so it also covers the daemon action button's
    /// outline (Stop/Start daemon) and tinted (Switch daemon here) states.</summary>
    internal sealed class HeroButton : AuroraButton
    {
        public Color FillColor = SprocketTheme.Accent;
        public Color FillHoverColor = SprocketTheme.AccentHover;
        public Color TextColor = Color.White;
        /// <summary>Color.Empty (the default) means no border — used for the solid-accent style.</summary>
        public Color BorderColor = Color.Empty;

        private readonly HoverFx _fx;
        private bool _pressed;

        public HeroButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            Font = new Font(SprocketTheme.HeadingFamily, 11F, FontStyle.Bold);
            _fx = new HoverFx(this);

            MouseDown += delegate { _pressed = true; Invalidate(); };
            MouseUp += delegate { _pressed = false; Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = GdiUtil.RoundedRect(rect, 5))
            {
                // Disabled elements recede toward the window background, not toward white — on a
                // dark theme, fading toward white would make disabled buttons brighter than enabled
                // ones.
                Color fill = !Enabled
                    ? GdiUtil.Lerp(FillColor, SprocketTheme.WindowBg, 0.55f)
                    : GdiUtil.Lerp(FillColor, FillHoverColor, _fx.Value);
                using (SolidBrush b = new SolidBrush(fill))
                    g.FillPath(b, path);

                if (BorderColor != Color.Empty)
                {
                    Color border = Enabled ? BorderColor : GdiUtil.Lerp(BorderColor, SprocketTheme.WindowBg, 0.4f);
                    using (Pen pen = new Pen(border, 1f))
                        g.DrawPath(pen, path);
                }

                if (_pressed && Enabled)
                    using (SolidBrush dim = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
                        g.FillPath(dim, path);
            }

            Color textColor = Enabled ? TextColor : GdiUtil.Lerp(TextColor, SprocketTheme.WindowBg, 0.45f);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(6, 0, Width - 12, Height), textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>Quick-action tile — white card with a Fluent icon glyph over a label.</summary>
    internal sealed class IconTile : AuroraButton
    {
        /// <summary>Accent-tinted "featured" style (used for the Modules tile).</summary>
        public bool Highlighted;
        public string Glyph = "";
        /// <summary>Optional hand-drawn icon used instead of an MDL2 glyph (e.g. the module grid mark).</summary>
        public Action<Graphics, Rectangle, Color> CustomIconPaint;

        private readonly HoverFx _fx;
        private readonly Font _iconFont;

        public IconTile()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            ForeColor = SprocketTheme.TextPrimary;
            Cursor = Cursors.Hand;
            Font = new Font(SprocketTheme.BodyFamily, 8.25F, FontStyle.Bold);
            _iconFont = new Font(SprocketTheme.IconFontName, 12.5F);
            _fx = new HoverFx(this);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float hover = Enabled ? _fx.Value : 0f;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = GdiUtil.RoundedRect(rect, 6))
            {
                Color fillColor = Highlighted ? SprocketTheme.AccentTintBg : SprocketTheme.CardBg;
                using (SolidBrush fill = new SolidBrush(Enabled ? fillColor : SprocketTheme.WindowBg))
                    g.FillPath(fill, path);

                Color baseBorder = Highlighted ? SprocketTheme.AccentTintBorder : SprocketTheme.TileBorder;
                Color border = Enabled ? GdiUtil.Lerp(baseBorder, SprocketTheme.Accent, hover) : SprocketTheme.TileBorder;
                using (Pen pen = new Pen(border, 1f))
                    g.DrawPath(pen, path);
            }

            Color iconColor = Enabled ? SprocketTheme.AccentDeep : SprocketTheme.TextTertiary;
            Rectangle iconRect = new Rectangle(0, 10, Width, 22);
            if (CustomIconPaint != null)
                CustomIconPaint(g, iconRect, iconColor);
            else
                TextRenderer.DrawText(g, Glyph, _iconFont, iconRect, iconColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            Color labelColor = Enabled
                ? (Highlighted ? SprocketTheme.AccentDeep : SprocketTheme.TileLabelText)
                : SprocketTheme.TextTertiary;
            TextRenderer.DrawText(g, Text, Font, new Rectangle(4, Height - 24, Width - 8, 18), labelColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>Rounded ghost icon button (header actions like rescan / locations).</summary>
    internal sealed class GhostIconButton : AuroraButton
    {
        public string Glyph = "";
        private readonly HoverFx _fx;
        private readonly Font _iconFont;

        public GhostIconButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            _iconFont = new Font(SprocketTheme.IconFontName, 11F);
            _fx = new HoverFx(this);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float hover = _fx.Value;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = GdiUtil.RoundedRect(rect, 5))
            {
                using (SolidBrush fill = new SolidBrush(GdiUtil.Lerp(Color.FromArgb(0, SprocketTheme.GhostHoverBg), SprocketTheme.GhostHoverBg, hover)))
                    g.FillPath(fill, path);
            }

            TextRenderer.DrawText(g, Glyph, _iconFont, rect, SprocketTheme.TextSecondary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>Outline button with a text label (secondary actions / cancel / "Add folder…").</summary>
    internal sealed class TextGhostButton : AuroraButton
    {
        public string Glyph = "";
        private readonly HoverFx _fx;
        private readonly Font _iconFont;

        public TextGhostButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            Font = new Font(SprocketTheme.BodyFamily, 9.25F, FontStyle.Bold);
            _iconFont = new Font(SprocketTheme.IconFontName, 9F);
            _fx = new HoverFx(this);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float hover = _fx.Value;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = GdiUtil.RoundedRect(rect, 5))
            {
                using (SolidBrush fill = new SolidBrush(GdiUtil.Lerp(SprocketTheme.CardBg, SprocketTheme.FieldHoverBg, hover)))
                    g.FillPath(fill, path);
                using (Pen pen = new Pen(SprocketTheme.FieldBorder, 1f))
                    g.DrawPath(pen, path);
            }

            if (!string.IsNullOrEmpty(Glyph))
            {
                Size textSize = TextRenderer.MeasureText(g, Text, Font, Size.Empty, TextFormatFlags.NoPadding);
                int totalW = textSize.Width + 22;
                int startX = (Width - totalW) / 2;
                TextRenderer.DrawText(g, Glyph, _iconFont, new Rectangle(startX, 0, 18, Height),
                    SprocketTheme.TextPrimary, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, Text, Font, new Rectangle(startX + 18, 0, textSize.Width + 6, Height),
                    SprocketTheme.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
            else
            {
                TextRenderer.DrawText(g, Text, Font, rect, SprocketTheme.TextPrimary,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }

    /// <summary>Custom pill dropdown with a rounded floating popup (replaces ComboBox).</summary>
    internal sealed class PillSelect : AuroraControl
    {
        public event EventHandler SelectedIndexChanged;
        public event Action AddRootRequested;
        public event Action<string> RemoveRootRequested;

        private readonly List<object> _items = new List<object>();
        private int _selectedIndex = -1;
        private readonly HoverFx _fx;
        private readonly Font _iconFont;
        private readonly Font _hintFont;
        private bool _open;
        private int _lastCloseTick = -10000;

        /// <summary>Leading status dot (e.g. "is the selected platform the one whose daemon is running").</summary>
        public bool ShowStatusDot;
        public Color StatusDotColor = SprocketTheme.TextTertiary;

        /// <summary>Right-aligned hint text shown before the chevron (e.g. "DAEMON ON 4.13.2.18").</summary>
        public string Hint = "";
        public Color HintColor = SprocketTheme.Pending;

        /// <summary>The item (by reference) whose daemon is currently running, if any — drives the
        /// dropdown row's green dot + RUNNING chip.</summary>
        public object RunningItem;

        /// <summary>When true, the popup grows a "scan roots" footer (main platform selector only).</summary>
        public bool ShowScanRootsFooter;
        public List<string> ScanRoots = new List<string>();

        public PillSelect()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Cursor = Cursors.Hand;
            Height = 36;
            Font = new Font(SprocketTheme.BodyFamily, 10F);
            _iconFont = new Font(SprocketTheme.IconFontName, 8F);
            _hintFont = new Font(SprocketTheme.BodyFamily, 7.5F, FontStyle.Bold);
            _fx = new HoverFx(this);
        }

        public List<object> Items { get { return _items; } }

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                int v = value;
                if (v >= _items.Count) v = _items.Count - 1;
                if (v < -1) v = -1;
                bool changed = v != _selectedIndex;
                _selectedIndex = v;
                Invalidate();
                if (changed && SelectedIndexChanged != null)
                    SelectedIndexChanged(this, EventArgs.Empty);
            }
        }

        public object SelectedItem
        {
            get
            {
                if (_selectedIndex < 0 || _selectedIndex >= _items.Count) return null;
                return _items[_selectedIndex];
            }
        }

        internal void RaiseAddRoot()
        {
            if (AddRootRequested != null) AddRootRequested();
        }

        internal void RaiseRemoveRoot(string root)
        {
            if (RemoveRootRequested != null) RemoveRootRequested(root);
        }

        internal void PopupClosed()
        {
            _open = false;
            _lastCloseTick = Environment.TickCount;
            if (!IsDisposed) Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || _items.Count == 0 || _open) return;
            // Clicking the pill while the popup is open first deactivates (closes) the popup —
            // swallow that click instead of instantly reopening.
            if (Environment.TickCount - _lastCloseTick < 250) return;

            _open = true;
            Invalidate();
            DropPopup popup = new DropPopup(this);
            popup.FormClosed += delegate { PopupClosed(); };
            popup.Location = PointToScreen(new Point(0, Height + 6));
            popup.Show(FindForm());
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float hover = Math.Max(_fx.Value, _open ? 1f : 0f);
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath pill = GdiUtil.RoundedRect(rect, 5))
            {
                using (SolidBrush fill = new SolidBrush(GdiUtil.Lerp(SprocketTheme.CardBg, SprocketTheme.FieldHoverBg, hover)))
                    g.FillPath(fill, pill);
                using (Pen pen = new Pen(SprocketTheme.FieldBorder, 1f))
                    g.DrawPath(pen, pill);
            }
            using (Pen bottomPen = new Pen(SprocketTheme.FieldBorderBottom, 1f))
                g.DrawLine(bottomPen, rect.X + 5, rect.Bottom, rect.Right - 5, rect.Bottom);

            int textLeft = 14;
            if (ShowStatusDot)
            {
                using (SolidBrush dot = new SolidBrush(StatusDotColor))
                    g.FillEllipse(dot, 14, Height / 2 - 4, 7, 7);
                textLeft = 30;
            }

            int rightReserved = 32;
            if (!string.IsNullOrEmpty(Hint))
            {
                Size hintSize = TextRenderer.MeasureText(g, Hint, _hintFont, Size.Empty, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, Hint, _hintFont,
                    new Rectangle(Width - 32 - hintSize.Width, 0, hintSize.Width, Height),
                    HintColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                rightReserved += hintSize.Width + 10;
            }

            object item = SelectedItem;
            string text = item != null ? item.ToString() : "Select a platform…";
            Color textColor = item != null ? SprocketTheme.TextPrimary : SprocketTheme.TextTertiary;
            TextRenderer.DrawText(g, text, Font, new Rectangle(textLeft, 0, Width - textLeft - rightReserved, Height), textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            TextRenderer.DrawText(g, _open ? "" : "", _iconFont,
                new Rectangle(Width - 30, 0, 20, Height), SprocketTheme.TextSecondary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>Floating rounded list popup owned by a PillSelect — optionally grows a "scan roots" footer.</summary>
    internal sealed class DropPopup : Form
    {
        private const int ItemHeight = 40;
        private const int Pad = 6;
        private const int RootRowH = 24;
        private const int AddRootH = 30;

        private readonly PillSelect _owner;
        private int _hotItem = -1;
        private int _hotRemoveRoot = -1;
        private bool _hotAddRoot;

        public DropPopup(PillSelect owner)
        {
            _owner = owner;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            KeyPreview = true;
            BackColor = SprocketTheme.CardBg;
            Size = new Size(Math.Max(owner.Width, 260), ComputeHeight());
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        private int ItemsBottom { get { return Pad + _owner.Items.Count * ItemHeight; } }
        private int RootCount { get { return 1 + _owner.ScanRoots.Count; } } // built-in + custom
        private int MicroY { get { return ItemsBottom + 16; } }
        private int RootsTop { get { return MicroY + 18; } }
        private int RootRowY(int i) { return RootsTop + i * RootRowH; }
        private int AddRootY { get { return RootsTop + RootCount * RootRowH + 6; } }

        private int ComputeHeight()
        {
            return _owner.ShowScanRootsFooter ? AddRootY + AddRootH + 10 : ItemsBottom + Pad;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x20000; // CS_DROPSHADOW
                return cp;
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            using (GraphicsPath path = GdiUtil.RoundedRect(new Rectangle(0, 0, Width, Height), 10))
                Region = new Region(path);
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) Close();
        }

        private int ItemIndexAt(Point pt)
        {
            if (pt.Y < 0 || pt.Y >= ItemsBottom) return -1;
            int i = (pt.Y - Pad) / ItemHeight;
            if (i < 0 || i >= _owner.Items.Count) return -1;
            return i;
        }

        private Rectangle RemoveRootRect(int rowIndex)
        {
            return new Rectangle(Width - Pad - 26, RootRowY(rowIndex), 20, 20);
        }

        private Rectangle AddRootRect()
        {
            return new Rectangle(Pad, AddRootY, Width - Pad * 2, AddRootH);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int item = ItemIndexAt(e.Location);
            int removeRoot = -1;
            bool addRoot = false;
            if (_owner.ShowScanRootsFooter)
            {
                for (int i = 0; i < _owner.ScanRoots.Count; i++)
                {
                    if (RemoveRootRect(i + 1).Contains(e.Location)) { removeRoot = i; break; }
                }
                addRoot = AddRootRect().Contains(e.Location);
            }
            if (item != _hotItem || removeRoot != _hotRemoveRoot || addRoot != _hotAddRoot)
            {
                _hotItem = item; _hotRemoveRoot = removeRoot; _hotAddRoot = addRoot;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hotItem != -1 || _hotRemoveRoot != -1 || _hotAddRoot)
            {
                _hotItem = -1; _hotRemoveRoot = -1; _hotAddRoot = false;
                Invalidate();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int item = ItemIndexAt(e.Location);
            if (item >= 0)
            {
                _owner.SelectedIndex = item;
                Close();
                return;
            }

            if (!_owner.ShowScanRootsFooter) return;

            for (int i = 0; i < _owner.ScanRoots.Count; i++)
            {
                if (RemoveRootRect(i + 1).Contains(e.Location))
                {
                    string root = _owner.ScanRoots[i];
                    Close();
                    _owner.RaiseRemoveRoot(root);
                    return;
                }
            }

            if (AddRootRect().Contains(e.Location))
            {
                Close();
                _owner.RaiseAddRoot();
                return;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (SolidBrush bg = new SolidBrush(SprocketTheme.CardBg))
                g.FillRectangle(bg, 0, 0, Width, Height);
            using (GraphicsPath path = GdiUtil.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 10))
            using (Pen pen = new Pen(SprocketTheme.CardBorder, 1f))
                g.DrawPath(pen, path);

            using (Font itemFont = new Font(SprocketTheme.BodyFamily, 9.75F))
            using (Font chipFont = new Font(SprocketTheme.BodyFamily, 7.5F, FontStyle.Bold))
            {
                for (int i = 0; i < _owner.Items.Count; i++)
                {
                    Rectangle row = new Rectangle(Pad, Pad + i * ItemHeight, Width - Pad * 2, ItemHeight);
                    bool isRunning = _owner.RunningItem != null && ReferenceEquals(_owner.Items[i], _owner.RunningItem);
                    bool isSelected = i == _owner.SelectedIndex;

                    if (i == _hotItem)
                    {
                        using (GraphicsPath hotPath = GdiUtil.RoundedRect(row, 8))
                        using (SolidBrush hotFill = new SolidBrush(SprocketTheme.RowHoverBg))
                            g.FillPath(hotFill, hotPath);
                    }

                    int textLeft = row.X + 12;
                    if (isRunning)
                    {
                        using (SolidBrush dot = new SolidBrush(SprocketTheme.Success))
                            g.FillEllipse(dot, row.X + 12, row.Y + row.Height / 2 - 3, 6, 6);
                        textLeft = row.X + 26;
                    }

                    int textRight = row.Right - 10;
                    if (isRunning)
                    {
                        textRight = GdiUtil.DrawChipRight(g, row.Right - 4, row.Y + (row.Height - 20) / 2, 20,
                            "RUNNING", chipFont, SprocketTheme.Success, SprocketTheme.SuccessTintBg,
                            SprocketTheme.SuccessTintBorder, false, Color.Empty) - 8;
                    }

                    Color textColor = isSelected || i == _hotItem ? SprocketTheme.TextPrimary : SprocketTheme.TextSecondary;
                    TextRenderer.DrawText(g, _owner.Items[i].ToString(), itemFont,
                        new Rectangle(textLeft, row.Y, Math.Max(0, textRight - textLeft), row.Height), textColor,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }

                if (!_owner.ShowScanRootsFooter) return;

                using (Pen sep = new Pen(SprocketTheme.Hairline, 1f))
                    g.DrawLine(sep, Pad, ItemsBottom, Width - Pad, ItemsBottom);

                using (Font microFont = new Font(SprocketTheme.BodyFamily, 7.5F))
                    TextRenderer.DrawText(g, "Scan roots · remembered on this machine", microFont,
                        new Point(Pad + 6, MicroY - 4), SprocketTheme.TextTertiary, TextFormatFlags.NoPadding);

                using (Font rootFont = new Font("Consolas", 8F))
                using (Font tagFont = new Font(SprocketTheme.BodyFamily, 7F, FontStyle.Bold))
                {
                    Rectangle builtInRow = new Rectangle(Pad, RootRowY(0), Width - Pad * 2, RootRowH);
                    TextRenderer.DrawText(g, "C:\\ · Program Files · Program Files (x86)", rootFont,
                        new Rectangle(builtInRow.X + 6, builtInRow.Y, builtInRow.Width - 80, builtInRow.Height),
                        SprocketTheme.TextSecondary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    TextRenderer.DrawText(g, "BUILT-IN", tagFont,
                        new Rectangle(builtInRow.Right - 66, builtInRow.Y, 66, builtInRow.Height),
                        SprocketTheme.TextTertiary, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

                    for (int i = 0; i < _owner.ScanRoots.Count; i++)
                    {
                        Rectangle row = new Rectangle(Pad, RootRowY(i + 1), Width - Pad * 2, RootRowH);
                        TextRenderer.DrawText(g, _owner.ScanRoots[i], rootFont,
                            new Rectangle(row.X + 6, row.Y, row.Width - 40, row.Height),
                            SprocketTheme.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.PathEllipsis);

                        Rectangle rr = RemoveRootRect(i + 1);
                        bool hotX = _hotRemoveRoot == i;
                        if (hotX)
                        {
                            using (SolidBrush xFill = new SolidBrush(SprocketTheme.DangerTintBg))
                                g.FillEllipse(xFill, rr);
                        }
                        TextRenderer.DrawText(g, "✕", tagFont, rr, hotX ? SprocketTheme.Danger : SprocketTheme.TextTertiary,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    }
                }

                Rectangle addRow = AddRootRect();
                using (GraphicsPath addPath = GdiUtil.RoundedRect(addRow, 8))
                using (Pen dash = new Pen(_hotAddRoot ? SprocketTheme.Accent : SprocketTheme.FieldBorder, 1f))
                {
                    dash.DashStyle = DashStyle.Dash;
                    g.DrawPath(dash, addPath);
                }
                using (Font addFont = new Font(SprocketTheme.BodyFamily, 8.25F, FontStyle.Bold))
                    TextRenderer.DrawText(g, "+ Add root folder…", addFont, addRow,
                        _hotAddRoot ? SprocketTheme.AccentDeep : SprocketTheme.TextSecondary,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }

    /// <summary>White status card: platform name, chips, version/host id, path.</summary>
    internal sealed class StatusPanel : AuroraControl
    {
        public string PlatformName = "—";
        public string VersionText = "—";
        public string HostIdText = "—";
        public string PathText = "—";
        public string BitnessText = "";
        public string StateText = "UNKNOWN";
        public Color StateColor = SprocketTheme.TextTertiary;
        public Color StateBg = SprocketTheme.ChipBg;
        public Color StateBorder = SprocketTheme.CardBorder;

        private readonly Font _label;
        private readonly Font _name;
        private readonly Font _value;
        private readonly Font _mono;
        private readonly Font _monoSmall;
        private readonly Font _chip;

        public StatusPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            _label = new Font(SprocketTheme.BodyFamily, 8F);
            _name = new Font(SprocketTheme.HeadingFamily, 13.5F, FontStyle.Bold);
            _value = new Font(SprocketTheme.BodyFamily, 10F, FontStyle.Bold);
            _mono = new Font("Consolas", 9.5F);
            _monoSmall = new Font("Consolas", 8.25F);
            _chip = new Font(SprocketTheme.BodyFamily, 7.5F, FontStyle.Bold);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = GdiUtil.RoundedRect(rect, 8))
            {
                using (SolidBrush fill = new SolidBrush(SprocketTheme.CardBg))
                    g.FillPath(fill, path);
                using (Pen pen = new Pen(SprocketTheme.CardBorder, 1f))
                    g.DrawPath(pen, path);
            }

            const int pad = 18;
            int innerW = Width - pad * 2;

            int chipRight = Width - pad;
            chipRight = GdiUtil.DrawChipRight(g, chipRight, 12, 22, StateText, _chip,
                StateColor, StateBg, StateBorder, true, StateColor);
            if (!string.IsNullOrEmpty(BitnessText))
                GdiUtil.DrawChipRight(g, chipRight - 8, 12, 22, BitnessText, _chip,
                    SprocketTheme.TextSecondary, SprocketTheme.ChipBg, SprocketTheme.CardBorder, false, Color.Empty);

            TextRenderer.DrawText(g, "Active platform", _label,
                new Point(pad, 15), SprocketTheme.TextSecondary, TextFormatFlags.NoPadding);

            TextRenderer.DrawText(g, PlatformName, _name,
                new Rectangle(pad - 1, 30, innerW, 26), SprocketTheme.TextPrimary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            using (Pen hairline = new Pen(SprocketTheme.Hairline, 1f))
                g.DrawLine(hairline, pad, 66, Width - pad, 66);

            int col2 = pad + innerW / 2 + 4;
            TextRenderer.DrawText(g, "Version", _label,
                new Point(pad, 76), SprocketTheme.TextTertiary, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, VersionText, _value,
                new Rectangle(pad, 89, innerW / 2 - 10, 18), SprocketTheme.TextPrimary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            TextRenderer.DrawText(g, "Host ID", _label,
                new Point(col2, 76), SprocketTheme.TextTertiary, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, HostIdText, _mono,
                new Rectangle(col2, 89, Width - pad - col2, 18), SprocketTheme.AccentDeep,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            TextRenderer.DrawText(g, "Install path", _label,
                new Point(pad, 114), SprocketTheme.TextTertiary, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, PathText, _monoSmall,
                new Rectangle(pad, 127, innerW, 16), SprocketTheme.TextSecondary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>White card shown when no Niagara installs are detected.</summary>
    internal sealed class EmptyCard : AuroraControl
    {
        public string Headline = "No Niagara installs found";
        public string Sub = "";

        private readonly Font _iconFont;
        private readonly Font _headFont;
        private readonly Font _subFont;

        public EmptyCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            _iconFont = new Font(SprocketTheme.IconFontName, 20F);
            _headFont = new Font(SprocketTheme.HeadingFamily, 12.5F, FontStyle.Bold);
            _subFont = new Font(SprocketTheme.BodyFamily, 8.75F);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = GdiUtil.RoundedRect(rect, 8))
            {
                using (SolidBrush fill = new SolidBrush(SprocketTheme.CardBg))
                    g.FillPath(fill, path);
                using (Pen pen = new Pen(SprocketTheme.CardBorder, 1f))
                    g.DrawPath(pen, path);
            }

            TextRenderer.DrawText(g, "", _iconFont, new Rectangle(0, 26, Width, 34),
                SprocketTheme.TextTertiary, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, Headline, _headFont, new Rectangle(0, 66, Width, 26),
                SprocketTheme.TextPrimary, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, Sub, _subFont, new Rectangle(24, 94, Width - 48, 40),
                SprocketTheme.TextSecondary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak);
        }
    }

    /// <summary>Glass card listing custom scan folders, each with a remove (✕) affordance.</summary>
    internal sealed class FolderList : AuroraControl
    {
        public event Action<string> RemoveClicked;

        private const int RowHeight = 36;
        private const int Pad = 8;

        private readonly List<string> _folders = new List<string>();
        private readonly Font _pathFont;
        private readonly Font _emptyFont;
        private readonly Font _iconFont;
        private int _hotRow = -1;
        private bool _hotRemove;

        public FolderList()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw
                | ControlStyles.OptimizedDoubleBuffer, true);
            _pathFont = new Font("Consolas", 8.5F);
            _emptyFont = new Font(SprocketTheme.BodyFamily, 8.75F);
            _iconFont = new Font(SprocketTheme.IconFontName, 7.5F);
        }

        public List<string> Folders { get { return _folders; } }

        public void SetFolders(IEnumerable<string> folders)
        {
            _folders.Clear();
            _folders.AddRange(folders);
            _hotRow = -1;
            Invalidate();
        }

        private Rectangle RowRect(int i)
        {
            return new Rectangle(Pad, Pad + i * RowHeight, Width - Pad * 2, RowHeight);
        }

        private Rectangle RemoveRect(int i)
        {
            Rectangle row = RowRect(i);
            return new Rectangle(row.Right - 34, row.Y + (RowHeight - 24) / 2, 24, 24);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int row = -1;
            bool remove = false;
            for (int i = 0; i < _folders.Count; i++)
            {
                if (RowRect(i).Contains(e.Location))
                {
                    row = i;
                    remove = RemoveRect(i).Contains(e.Location);
                    break;
                }
            }
            if (row != _hotRow || remove != _hotRemove)
            {
                _hotRow = row;
                _hotRemove = remove;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hotRow != -1) { _hotRow = -1; _hotRemove = false; Invalidate(); }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;
            for (int i = 0; i < _folders.Count; i++)
            {
                if (RemoveRect(i).Contains(e.Location))
                {
                    if (RemoveClicked != null) RemoveClicked(_folders[i]);
                    return;
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = GdiUtil.RoundedRect(rect, 8))
            {
                using (SolidBrush fill = new SolidBrush(SprocketTheme.CardBg))
                    g.FillPath(fill, path);
                using (Pen pen = new Pen(SprocketTheme.CardBorder, 1f))
                    g.DrawPath(pen, path);
            }

            if (_folders.Count == 0)
            {
                TextRenderer.DrawText(g, "No extra folders — standard locations are scanned.",
                    _emptyFont, rect, SprocketTheme.TextTertiary,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            for (int i = 0; i < _folders.Count; i++)
            {
                Rectangle row = RowRect(i);
                if (row.Bottom > Height - Pad + RowHeight) break; // clip overflow

                if (i == _hotRow)
                {
                    using (GraphicsPath hotPath = GdiUtil.RoundedRect(row, 6))
                    using (SolidBrush hotFill = new SolidBrush(SprocketTheme.RowHoverBg))
                        g.FillPath(hotFill, hotPath);
                }

                if (i > 0)
                    using (Pen sep = new Pen(SprocketTheme.RowDivider, 1f))
                        g.DrawLine(sep, row.X, row.Y, row.Right, row.Y);

                TextRenderer.DrawText(g, _folders[i], _pathFont,
                    new Rectangle(row.X + 12, row.Y, row.Width - 52, row.Height),
                    SprocketTheme.TextPrimary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.PathEllipsis);

                Rectangle rr = RemoveRect(i);
                bool hotX = i == _hotRow && _hotRemove;
                if (hotX)
                {
                    using (SolidBrush xFill = new SolidBrush(SprocketTheme.DangerTintBg))
                        g.FillEllipse(xFill, rr);
                }
                TextRenderer.DrawText(g, SprocketTheme.Glyph(0xE711), _iconFont, rr,
                    hotX ? SprocketTheme.Danger : SprocketTheme.TextTertiary,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }
    }

    /// <summary>Rounded white field hosting a borderless NumericUpDown (Fluent field cue on the bottom edge).</summary>
    internal sealed class PillField : AuroraPanel
    {
        public readonly NumericUpDown Input;

        public PillField()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Height = 36;

            Input = new NumericUpDown();
            Input.BorderStyle = BorderStyle.None;
            Input.BackColor = SprocketTheme.CardBg;
            Input.ForeColor = SprocketTheme.TextPrimary;
            Input.Font = new Font(SprocketTheme.BodyFamily, 10F);
            Controls.Add(Input);
            LayoutInput();
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            LayoutInput();
        }

        private void LayoutInput()
        {
            if (Input == null) return; // resize fires from the ctor before Input exists
            Input.SetBounds(13, (Height - Input.PreferredHeight) / 2, Width - 24, Input.PreferredHeight);
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

    /// <summary>Accent-tinted info/progress banner (daemon switchover notices).</summary>
    internal sealed class InfoBanner : AuroraControl
    {
        public string Message = "";
        /// <summary>Right-aligned step indicator, e.g. "STEP 1 OF 2". Empty hides it.</summary>
        public string StepText = "";
        public bool ShowDot = true;

        private readonly Font _font;
        private readonly Font _stepFont;

        public InfoBanner()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            _font = new Font(SprocketTheme.BodyFamily, 8.5F);
            _stepFont = new Font(SprocketTheme.BodyFamily, 7F, FontStyle.Bold);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Always the pending/amber treatment (not the ember brand accent) — this banner only
            // ever shows daemon-switchover notices, matching the reviewed mock's amber styling.
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = GdiUtil.RoundedRect(rect, 8))
            {
                using (SolidBrush fill = new SolidBrush(SprocketTheme.PendingTintBg))
                    g.FillPath(fill, path);
                using (Pen pen = new Pen(SprocketTheme.PendingTintBorder, 1f))
                    g.DrawPath(pen, path);
            }

            int left = 14;
            if (ShowDot)
            {
                using (SolidBrush dot = new SolidBrush(SprocketTheme.Pending))
                    g.FillEllipse(dot, left, Height / 2 - 3, 7, 7);
                left += 16;
            }

            int right = Width - 14;
            if (!string.IsNullOrEmpty(StepText))
            {
                Size stepSize = TextRenderer.MeasureText(g, StepText, _stepFont, Size.Empty, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, StepText, _stepFont,
                    new Rectangle(Width - 14 - stepSize.Width, 0, stepSize.Width, Height),
                    SprocketTheme.Pending, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                right = Width - 14 - stepSize.Width - 10;
            }

            TextRenderer.DrawText(g, Message, _font, new Rectangle(left, 0, Math.Max(0, right - left), Height),
                SprocketTheme.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordEllipsis);
        }
    }
}
