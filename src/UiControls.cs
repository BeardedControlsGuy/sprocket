using System;
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

        public static Color White(int alpha)
        {
            return Color.FromArgb(alpha, 255, 255, 255);
        }

        /// <summary>Sprocket's "forge" signature gradient: flame → ember → sun → sun-light.</summary>
        public static LinearGradientBrush ForgeBrush(Rectangle bounds, int alpha)
        {
            LinearGradientBrush brush = new LinearGradientBrush(
                bounds, Color.Black, Color.Black, LinearGradientMode.Horizontal);
            ColorBlend blend = new ColorBlend(4);
            blend.Colors = new Color[]
            {
                Color.FromArgb(alpha, SprocketTheme.Flame),
                Color.FromArgb(alpha, SprocketTheme.Ember),
                Color.FromArgb(alpha, SprocketTheme.Sun),
                Color.FromArgb(alpha, SprocketTheme.SunLight)
            };
            blend.Positions = new float[] { 0f, 0.32f, 0.74f, 1f };
            brush.InterpolationColors = blend;
            return brush;
        }

        public static void FillBlob(Graphics g, float cx, float cy, float radius, Color color)
        {
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(cx - radius, cy - radius, radius * 2f, radius * 2f);
                using (PathGradientBrush brush = new PathGradientBrush(path))
                {
                    brush.CenterColor = color;
                    brush.CenterPoint = new PointF(cx, cy);
                    brush.SurroundColors = new Color[] { Color.FromArgb(0, color) };
                    g.FillPath(brush, path);
                }
            }
        }

        /// <summary>Deep-ink canvas with soft ember/rust/sun ambient glows (forge-lit shop floor).</summary>
        public static void PaintBackdrop(Graphics g, int w, int h)
        {
            using (SolidBrush b = new SolidBrush(SprocketTheme.Ink))
                g.FillRectangle(b, 0, 0, w, h);

            float m = Math.Max(w, h);
            FillBlob(g, w * 0.16f, -m * 0.08f, m * 0.72f, Color.FromArgb(36, SprocketTheme.Ember));
            FillBlob(g, w * 0.98f, h * 0.10f, m * 0.42f, Color.FromArgb(18, SprocketTheme.Rust));
            FillBlob(g, w * 1.02f, h * 1.06f, m * 0.66f, Color.FromArgb(26, SprocketTheme.Sun));
            FillBlob(g, -w * 0.06f, h * 0.98f, m * 0.42f, Color.FromArgb(15, SprocketTheme.Ember));
        }
    }

    /// <summary>Caches the aurora backdrop as a bitmap so children can repaint slices cheaply.</summary>
    internal sealed class Backdrop : IDisposable
    {
        private Bitmap _bmp;

        private void Ensure(Size size)
        {
            if (_bmp == null || _bmp.Width != size.Width || _bmp.Height != size.Height)
            {
                if (_bmp != null) _bmp.Dispose();
                _bmp = new Bitmap(size.Width, size.Height);
                using (Graphics bg = Graphics.FromImage(_bmp))
                    GdiUtil.PaintBackdrop(bg, size.Width, size.Height);
            }
        }

        public void Paint(Graphics g, Size size)
        {
            if (size.Width < 1 || size.Height < 1) return;
            Ensure(size);
            g.DrawImage(_bmp, 0, 0);
        }

        /// <summary>Paint the part of the backdrop lying under a child at the given host offset.</summary>
        public void PaintSlice(Graphics g, Size hostSize, Point offset)
        {
            if (hostSize.Width < 1 || hostSize.Height < 1) return;
            Ensure(hostSize);
            g.DrawImage(_bmp, -offset.X, -offset.Y);
        }

        public void Dispose()
        {
            if (_bmp != null) { _bmp.Dispose(); _bmp = null; }
        }
    }

    /// <summary>A form that owns an aurora backdrop children can sample from.</summary>
    internal interface IAuroraHost
    {
        void PaintAuroraSlice(Graphics g, Control child);
    }

    internal static class AuroraBg
    {
        /// <summary>Manual "transparency": paints the owning form's backdrop under a control.
        /// (WinForms' simulated transparency composites garbage under double-buffered controls.)</summary>
        public static void Paint(Control c, Graphics g)
        {
            Form form = c.FindForm();
            IAuroraHost host = form as IAuroraHost;
            if (host != null)
            {
                host.PaintAuroraSlice(g, c);
                return;
            }
            using (SolidBrush b = new SolidBrush(SprocketTheme.Ink))
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

    /// <summary>Eased hover progress (0..1) driving smooth color/glow transitions.</summary>
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
            _value += (_target - _value) * 0.3f;
            if (Math.Abs(_target - _value) < 0.02f)
            {
                _value = _target;
                _timer.Stop();
            }
            _owner.Invalidate();
        }
    }

    /// <summary>Primary CTA — thermal-gradient pill with hover glow and a slow shimmer sweep.</summary>
    internal sealed class HeroButton : AuroraButton
    {
        private readonly HoverFx _fx;
        private readonly Timer _shimmer;
        private float _phase;   // 0..2.6; the highlight band is visible over 0..1
        private bool _pressed;

        public HeroButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            ForeColor = Color.White;
            Cursor = Cursors.Hand;
            Font = new Font(SprocketTheme.HeadingFamily, 12F, FontStyle.Bold);
            _fx = new HoverFx(this);

            _shimmer = new Timer();
            _shimmer.Interval = 33;
            _shimmer.Tick += delegate
            {
                _phase += 0.013f;
                if (_phase > 2.6f) _phase = 0f;
                if (_phase < 1.1f) Invalidate();
            };
            _shimmer.Start();
            Disposed += delegate { _shimmer.Dispose(); };

            MouseDown += delegate { _pressed = true; Invalidate(); };
            MouseUp += delegate { _pressed = false; Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float hover = _fx.Value;
            Rectangle rect = new Rectangle(4, 4, Width - 9, Height - 9);
            using (GraphicsPath pill = GdiUtil.RoundedRect(rect, rect.Height / 2))
            {
                if (hover > 0.02f)
                {
                    using (Pen glowWide = new Pen(Color.FromArgb((int)(hover * 20f), SprocketTheme.EmberLight), 7f))
                        g.DrawPath(glowWide, pill);
                    using (Pen glowMid = new Pen(Color.FromArgb((int)(hover * 38f), SprocketTheme.EmberLight), 4f))
                        g.DrawPath(glowMid, pill);
                    using (Pen glowTight = new Pen(Color.FromArgb((int)(hover * 70f), Color.White), 1.6f))
                        g.DrawPath(glowTight, pill);
                }

                using (LinearGradientBrush brush = GdiUtil.ForgeBrush(rect, 255))
                    g.FillPath(brush, pill);

                if (hover > 0.02f)
                {
                    using (SolidBrush brighten = new SolidBrush(GdiUtil.White((int)(hover * 26f))))
                        g.FillPath(brighten, pill);
                }

                if (_phase < 1f)
                {
                    g.SetClip(pill);
                    float band = rect.Width * 0.30f;
                    float x = rect.X - band + _phase * (rect.Width + band * 2f);
                    RectangleF bandRect = new RectangleF(x, rect.Y, band, rect.Height);
                    using (LinearGradientBrush shine = new LinearGradientBrush(
                        new RectangleF(bandRect.X - 1f, bandRect.Y, bandRect.Width + 2f, bandRect.Height),
                        Color.Black, Color.Black, LinearGradientMode.Horizontal))
                    {
                        ColorBlend cb = new ColorBlend(3);
                        cb.Colors = new Color[] { GdiUtil.White(0), GdiUtil.White(64), GdiUtil.White(0) };
                        cb.Positions = new float[] { 0f, 0.5f, 1f };
                        shine.InterpolationColors = cb;
                        g.FillRectangle(shine, bandRect);
                    }
                    g.ResetClip();
                }

                if (_pressed)
                {
                    using (SolidBrush dim = new SolidBrush(Color.FromArgb(52, 0, 0, 0)))
                        g.FillPath(dim, pill);
                }
            }

            Color textColor = Enabled ? Color.White : GdiUtil.White(150);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, Height - 1), textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    /// <summary>Quick-action tile — glass card with a Fluent icon glyph over a label.</summary>
    internal sealed class IconTile : AuroraButton
    {
        public Color AccentColor = SprocketTheme.Ember;
        public string Glyph = "";
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
            using (GraphicsPath path = GdiUtil.RoundedRect(rect, 14))
            {
                int fillAlpha = Enabled ? (int)(11 + 13 * hover) : 6;
                using (SolidBrush fill = new SolidBrush(GdiUtil.White(fillAlpha)))
                    g.FillPath(fill, path);

                Color border = Enabled
                    ? GdiUtil.Lerp(GdiUtil.White(24), Color.FromArgb(200, AccentColor), hover)
                    : GdiUtil.White(12);
                using (Pen pen = new Pen(border, 1f))
                    g.DrawPath(pen, path);
            }

            Color iconColor = Enabled
                ? GdiUtil.Lerp(GdiUtil.Lerp(SprocketTheme.TextMuted, AccentColor, 0.62f),
                               GdiUtil.Lerp(AccentColor, Color.White, 0.18f), hover)
                : SprocketTheme.TextFaint;
            TextRenderer.DrawText(g, Glyph, _iconFont, new Rectangle(0, 11, Width, 22), iconColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            Color labelColor = Enabled
                ? GdiUtil.Lerp(SprocketTheme.TextMuted, SprocketTheme.TextPrimary, 0.35f + 0.65f * hover)
                : SprocketTheme.TextFaint;
            TextRenderer.DrawText(g, Text, Font, new Rectangle(4, Height - 26, Width - 8, 18), labelColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>Round frameless icon button (e.g. the rescan control in the header).</summary>
    internal sealed class GhostIconButton : AuroraButton
    {
        public string Glyph = "";
        private readonly HoverFx _fx;
        private readonly Font _iconFont;

        public GhostIconButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            _iconFont = new Font(SprocketTheme.IconFontName, 10.5F);
            _fx = new HoverFx(this);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float hover = _fx.Value;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (SolidBrush fill = new SolidBrush(GdiUtil.White((int)(7 + 11 * hover))))
                g.FillEllipse(fill, rect);
            using (Pen pen = new Pen(GdiUtil.Lerp(GdiUtil.White(20), Color.FromArgb(190, SprocketTheme.Ember), hover), 1f))
                g.DrawEllipse(pen, rect);

            Color iconColor = GdiUtil.Lerp(SprocketTheme.TextMuted, SprocketTheme.EmberLight, hover);
            TextRenderer.DrawText(g, Glyph, _iconFont, rect, iconColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>Pill-shaped ghost button with a text label (secondary actions / cancel).</summary>
    internal sealed class TextGhostButton : AuroraButton
    {
        private readonly HoverFx _fx;

        public TextGhostButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            Font = new Font(SprocketTheme.BodyFamily, 9.25F, FontStyle.Bold);
            _fx = new HoverFx(this);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float hover = _fx.Value;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath pill = GdiUtil.RoundedRect(rect, rect.Height / 2))
            {
                using (SolidBrush fill = new SolidBrush(GdiUtil.White((int)(7 + 11 * hover))))
                    g.FillPath(fill, pill);
                using (Pen pen = new Pen(GdiUtil.Lerp(GdiUtil.White(26), Color.FromArgb(190, SprocketTheme.Ember), hover), 1f))
                    g.DrawPath(pen, pill);
            }

            TextRenderer.DrawText(g, Text, Font, rect,
                GdiUtil.Lerp(SprocketTheme.TextMuted, SprocketTheme.TextPrimary, 0.4f + 0.6f * hover),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    /// <summary>Custom pill dropdown with a rounded floating popup (replaces ComboBox).</summary>
    internal sealed class PillSelect : AuroraControl
    {
        public event EventHandler SelectedIndexChanged;

        private readonly List<object> _items = new List<object>();
        private int _selectedIndex = -1;
        private readonly HoverFx _fx;
        private readonly Font _iconFont;
        private bool _open;
        private int _lastCloseTick = -10000;

        public PillSelect()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Cursor = Cursors.Hand;
            Height = 44;
            Font = new Font(SprocketTheme.BodyFamily, 10F);
            _iconFont = new Font(SprocketTheme.IconFontName, 8F);
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
            using (GraphicsPath pill = GdiUtil.RoundedRect(rect, rect.Height / 2))
            {
                using (SolidBrush fill = new SolidBrush(GdiUtil.White((int)(9 + 9 * hover))))
                    g.FillPath(fill, pill);
                using (Pen pen = new Pen(GdiUtil.Lerp(GdiUtil.White(26), Color.FromArgb(185, SprocketTheme.Ember), hover), 1f))
                    g.DrawPath(pen, pill);
            }

            object item = SelectedItem;
            string text = item != null ? item.ToString() : "Select a platform…";
            Color textColor = item != null ? SprocketTheme.TextPrimary : SprocketTheme.TextMuted;
            TextRenderer.DrawText(g, text, Font, new Rectangle(20, 0, Width - 58, Height), textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            TextRenderer.DrawText(g, _open ? "\uE70E" : "\uE70D", _iconFont,
                new Rectangle(Width - 38, 0, 24, Height),
                GdiUtil.Lerp(SprocketTheme.TextMuted, SprocketTheme.EmberLight, hover),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>Floating rounded list popup owned by a PillSelect.</summary>
    internal sealed class DropPopup : Form
    {
        private const int ItemHeight = 40;
        private const int Pad = 6;

        private readonly PillSelect _owner;
        private int _hot = -1;

        public DropPopup(PillSelect owner)
        {
            _owner = owner;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            KeyPreview = true;
            BackColor = SprocketTheme.InkRaised;
            Size = new Size(owner.Width, owner.Items.Count * ItemHeight + Pad * 2);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
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
            using (GraphicsPath path = GdiUtil.RoundedRect(new Rectangle(0, 0, Width, Height), 12))
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

        private int IndexAt(int y)
        {
            int i = (y - Pad) / ItemHeight;
            if (i < 0 || i >= _owner.Items.Count) return -1;
            return i;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int i = IndexAt(e.Y);
            if (i != _hot) { _hot = i; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hot != -1) { _hot = -1; Invalidate(); }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int i = IndexAt(e.Y);
            if (i >= 0)
            {
                _owner.SelectedIndex = i;
                Close();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (SolidBrush bg = new SolidBrush(SprocketTheme.InkRaised))
                g.FillRectangle(bg, 0, 0, Width, Height);
            using (GraphicsPath path = GdiUtil.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 12))
            using (Pen pen = new Pen(GdiUtil.White(30), 1f))
                g.DrawPath(pen, path);

            using (Font itemFont = new Font(SprocketTheme.BodyFamily, 9.75F))
            {
                for (int i = 0; i < _owner.Items.Count; i++)
                {
                    Rectangle row = new Rectangle(Pad, Pad + i * ItemHeight, Width - Pad * 2, ItemHeight);
                    if (i == _hot)
                    {
                        using (GraphicsPath hotPath = GdiUtil.RoundedRect(row, 8))
                        using (SolidBrush hotFill = new SolidBrush(Color.FromArgb(34, SprocketTheme.Ember)))
                            g.FillPath(hotFill, hotPath);
                    }

                    bool selected = i == _owner.SelectedIndex;
                    if (selected)
                    {
                        using (SolidBrush dot = new SolidBrush(SprocketTheme.EmberLight))
                            g.FillEllipse(dot, row.X + 12, row.Y + row.Height / 2 - 3, 6, 6);
                    }

                    Color textColor = selected ? SprocketTheme.TextPrimary
                        : (i == _hot ? SprocketTheme.TextPrimary : SprocketTheme.TextMuted);
                    TextRenderer.DrawText(g, _owner.Items[i].ToString(), itemFont,
                        new Rectangle(row.X + 28, row.Y, row.Width - 36, row.Height), textColor,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }
            }
        }
    }

    /// <summary>Glass status card fully custom-painted: platform name, chips, version/host id, path.</summary>
    internal sealed class StatusPanel : AuroraControl
    {
        public string PlatformName = "—";
        public string VersionText = "—";
        public string HostIdText = "—";
        public string PathText = "—";
        public string BitnessText = "";
        public string StateText = "UNKNOWN";
        public Color StateColor = SprocketTheme.TextFaint;

        private readonly Font _micro;
        private readonly Font _name;
        private readonly Font _value;
        private readonly Font _mono;
        private readonly Font _monoSmall;
        private readonly Font _chip;

        public StatusPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            _micro = new Font(SprocketTheme.BodyFamily, 7F, FontStyle.Bold);
            _name = new Font(SprocketTheme.HeadingFamily, 13.5F, FontStyle.Bold);
            _value = new Font(SprocketTheme.BodyFamily, 10F, FontStyle.Bold);
            _mono = new Font("Consolas", 9.5F, FontStyle.Bold);
            _monoSmall = new Font("Consolas", 8F);
            _chip = new Font(SprocketTheme.BodyFamily, 7.5F, FontStyle.Bold);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = GdiUtil.RoundedRect(rect, 18))
            {
                using (LinearGradientBrush fill = new LinearGradientBrush(
                    rect, GdiUtil.White(15), GdiUtil.White(6), LinearGradientMode.Vertical))
                    g.FillPath(fill, path);
                using (Pen pen = new Pen(GdiUtil.White(24), 1f))
                    g.DrawPath(pen, path);

                // thermal signature hairline along the card top
                g.SetClip(path);
                using (LinearGradientBrush thermal = GdiUtil.ForgeBrush(
                    new Rectangle(0, 0, Width, 3), 150))
                    g.FillRectangle(thermal, 0, 0, Width, 2);
                g.ResetClip();
            }

            const int pad = 20;
            int innerW = Width - pad * 2;

            TextRenderer.DrawText(g, SprocketTheme.Track("ACTIVE PLATFORM"), _micro,
                new Point(pad, 16), SprocketTheme.TextFaint, TextFormatFlags.NoPadding);

            // chips, right-aligned
            int chipRight = Width - pad;
            chipRight = DrawChip(g, chipRight, 12, StateText,
                GdiUtil.Lerp(StateColor, Color.White, 0.25f), StateColor, true);
            if (!string.IsNullOrEmpty(BitnessText))
                DrawChip(g, chipRight - 8, 12, BitnessText, SprocketTheme.TextMuted, Color.Empty, false);

            TextRenderer.DrawText(g, PlatformName, _name,
                new Rectangle(pad - 1, 32, innerW, 28), SprocketTheme.TextPrimary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            using (Pen hairline = new Pen(GdiUtil.White(16), 1f))
                g.DrawLine(hairline, pad, 68, Width - pad, 68);

            int col2 = pad + innerW / 2;
            TextRenderer.DrawText(g, SprocketTheme.Track("VERSION"), _micro,
                new Point(pad, 79), SprocketTheme.TextFaint, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, VersionText, _value,
                new Rectangle(pad, 92, innerW / 2 - 10, 18), SprocketTheme.TextPrimary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            TextRenderer.DrawText(g, SprocketTheme.Track("HOST ID"), _micro,
                new Point(col2, 79), SprocketTheme.TextFaint, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, HostIdText, _mono,
                new Rectangle(col2, 92, innerW / 2, 18), SprocketTheme.EmberLight,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            TextRenderer.DrawText(g, SprocketTheme.Track("INSTALL PATH"), _micro,
                new Point(pad, 118), SprocketTheme.TextFaint, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, PathText, _monoSmall,
                new Rectangle(pad, 131, innerW, 16), SprocketTheme.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }

        private int DrawChip(Graphics g, int right, int y, string text, Color textColor, Color dotColor, bool showDot)
        {
            Size textSize = TextRenderer.MeasureText(g, text, _chip, Size.Empty, TextFormatFlags.NoPadding);
            int chipW = textSize.Width + 22 + (showDot ? 12 : 0);
            Rectangle rect = new Rectangle(right - chipW, y, chipW, 22);

            using (GraphicsPath pill = GdiUtil.RoundedRect(rect, 11))
            {
                using (SolidBrush fill = new SolidBrush(GdiUtil.White(12)))
                    g.FillPath(fill, pill);
                using (Pen pen = new Pen(GdiUtil.White(20), 1f))
                    g.DrawPath(pen, pill);
            }

            int textX = rect.X + 11;
            if (showDot)
            {
                using (SolidBrush dot = new SolidBrush(dotColor))
                    g.FillEllipse(dot, rect.X + 10, rect.Y + rect.Height / 2 - 3, 6, 6);
                textX = rect.X + 22;
            }

            TextRenderer.DrawText(g, text, _chip,
                new Rectangle(textX, rect.Y, rect.Width - (textX - rect.X) - 8, rect.Height), textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            return rect.X;
        }
    }

    /// <summary>Glass card shown when no Niagara installs are detected.</summary>
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
            _iconFont = new Font(SprocketTheme.IconFontName, 21F);
            _headFont = new Font(SprocketTheme.HeadingFamily, 12.5F, FontStyle.Bold);
            _subFont = new Font(SprocketTheme.BodyFamily, 8.75F);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = GdiUtil.RoundedRect(rect, 18))
            {
                using (LinearGradientBrush fill = new LinearGradientBrush(
                    rect, GdiUtil.White(13), GdiUtil.White(5), LinearGradientMode.Vertical))
                    g.FillPath(fill, path);
                using (Pen pen = new Pen(GdiUtil.White(22), 1f))
                    g.DrawPath(pen, path);
            }

            TextRenderer.DrawText(g, "\uE7BA", _iconFont, new Rectangle(0, 26, Width, 34),
                SprocketTheme.TextFaint, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, Headline, _headFont, new Rectangle(0, 66, Width, 26),
                SprocketTheme.TextPrimary, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, Sub, _subFont, new Rectangle(24, 94, Width - 48, 40),
                SprocketTheme.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak);
        }
    }

    /// <summary>Heading text painted with the aurora gradient.</summary>
    internal sealed class GradientTitle : AuroraControl
    {
        public GradientTitle()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint, true);
            Font = new Font(SprocketTheme.HeadingFamily, 20F, FontStyle.Bold);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            SizeF measured = g.MeasureString(Text, Font);
            RectangleF textRect = new RectangleF(0, 0, Math.Max(measured.Width, 10f), Math.Max(measured.Height, 10f));
            using (LinearGradientBrush brush = new LinearGradientBrush(
                textRect, Color.Black, Color.Black, LinearGradientMode.Horizontal))
            {
                ColorBlend blend = new ColorBlend(3);
                blend.Colors = new Color[] { SprocketTheme.EmberLight, SprocketTheme.Ember, SprocketTheme.Flame };
                blend.Positions = new float[] { 0f, 0.45f, 1f };
                brush.InterpolationColors = blend;
                g.DrawString(Text, Font, brush, new PointF(0, 0));
            }
        }
    }

    /// <summary>Thin horizontal thermal-gradient accent bar.</summary>
    internal sealed class GradientBar : Control
    {
        public GradientBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = 2;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Rectangle bounds = new Rectangle(0, 0, Width, Height);
            using (LinearGradientBrush brush = GdiUtil.ForgeBrush(bounds, 200))
                e.Graphics.FillRectangle(brush, bounds);
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
            using (GraphicsPath path = GdiUtil.RoundedRect(rect, 12))
            {
                using (SolidBrush fill = new SolidBrush(GdiUtil.White(9)))
                    g.FillPath(fill, path);
                using (Pen pen = new Pen(GdiUtil.White(22), 1f))
                    g.DrawPath(pen, path);
            }

            if (_folders.Count == 0)
            {
                TextRenderer.DrawText(g, "No extra folders — standard locations are scanned.",
                    _emptyFont, rect, SprocketTheme.TextFaint,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            for (int i = 0; i < _folders.Count; i++)
            {
                Rectangle row = RowRect(i);
                if (row.Bottom > Height - Pad + RowHeight) break; // clip overflow

                if (i == _hotRow)
                {
                    using (GraphicsPath hotPath = GdiUtil.RoundedRect(row, 8))
                    using (SolidBrush hotFill = new SolidBrush(Color.FromArgb(26, SprocketTheme.Ember)))
                        g.FillPath(hotFill, hotPath);
                }

                TextRenderer.DrawText(g, _folders[i], _pathFont,
                    new Rectangle(row.X + 12, row.Y, row.Width - 52, row.Height),
                    i == _hotRow ? SprocketTheme.TextPrimary : SprocketTheme.TextMuted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.PathEllipsis);

                Rectangle rr = RemoveRect(i);
                bool hotX = i == _hotRow && _hotRemove;
                if (hotX)
                {
                    using (SolidBrush xFill = new SolidBrush(Color.FromArgb(46, SprocketTheme.Danger)))
                        g.FillEllipse(xFill, rr);
                }
                TextRenderer.DrawText(g, SprocketTheme.Glyph(0xE711), _iconFont, rr,
                    hotX ? SprocketTheme.Danger : SprocketTheme.TextFaint,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }
    }

    /// <summary>Rounded dark field hosting a borderless NumericUpDown.</summary>
    internal sealed class PillField : AuroraPanel
    {
        public readonly NumericUpDown Input;

        public PillField()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Height = 40;

            Input = new NumericUpDown();
            Input.BorderStyle = BorderStyle.None;
            Input.BackColor = SprocketTheme.InkRaised;
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
            Input.SetBounds(14, (Height - Input.PreferredHeight) / 2 + 1, Width - 26, Input.PreferredHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = GdiUtil.RoundedRect(rect, 10))
            {
                using (SolidBrush fill = new SolidBrush(SprocketTheme.InkRaised))
                    g.FillPath(fill, path);
                using (Pen pen = new Pen(GdiUtil.White(26), 1f))
                    g.DrawPath(pen, path);
            }
        }
    }
}
