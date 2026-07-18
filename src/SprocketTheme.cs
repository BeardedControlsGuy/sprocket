using System.Drawing;

namespace Sprocket
{
    /// <summary>Sprocket's "calm dark Forge" palette — graphite + ember, flat surfaces
    /// (no aurora blob backdrop / shimmer / glow hovers).</summary>
    internal static class SprocketTheme
    {
        // Surfaces
        public static readonly Color WindowBg = ColorTranslator.FromHtml("#101014");
        public static readonly Color TitleBarBg = ColorTranslator.FromHtml("#17171b");
        public static readonly Color Hairline = ColorTranslator.FromHtml("#232329");
        public static readonly Color CardBg = ColorTranslator.FromHtml("#17171b");
        public static readonly Color CardBorder = ColorTranslator.FromHtml("#2d2d34");

        // Fields / inputs
        public static readonly Color FieldBorder = ColorTranslator.FromHtml("#2d2d34");
        // No separate Fluent "field cue" in the dark Forge look — same as FieldBorder.
        public static readonly Color FieldBorderBottom = FieldBorder;
        public static readonly Color FieldHoverBg = ColorTranslator.FromHtml("#1c1c22");

        // Misc surfaces
        public static readonly Color TileBorder = ColorTranslator.FromHtml("#26262c");
        public static readonly Color TileLabelText = ColorTranslator.FromHtml("#b7bbc1");
        public static readonly Color GhostHoverBg = ColorTranslator.FromHtml("#232329");
        public static readonly Color ChipBg = CardBg;
        public static readonly Color RowHoverBg = ColorTranslator.FromHtml("#1c1c22");
        public static readonly Color RowDivider = ColorTranslator.FromHtml("#1c1c21");

        // Brand
        public static readonly Color Accent = ColorTranslator.FromHtml("#f97316");
        public static readonly Color AccentHover = ColorTranslator.FromHtml("#fb8a33");
        // On dark surfaces the "readable accent" shade needs to go lighter, not darker.
        public static readonly Color AccentDeep = ColorTranslator.FromHtml("#fdba74");
        public static readonly Color AccentTintBg = ColorTranslator.FromHtml("#2a1c12");
        public static readonly Color AccentTintBorder = ColorTranslator.FromHtml("#5c3618");

        // Text
        public static readonly Color TextPrimary = ColorTranslator.FromHtml("#eef0f2");
        public static readonly Color TextSecondary = ColorTranslator.FromHtml("#999ea6");
        public static readonly Color TextTertiary = ColorTranslator.FromHtml("#5b5f67");

        // Status
        public static readonly Color Success = ColorTranslator.FromHtml("#22c55e");
        public static readonly Color SuccessTintBg = ColorTranslator.FromHtml("#152a1d");
        public static readonly Color SuccessTintBorder = ColorTranslator.FromHtml("#1f5c34");
        public static readonly Color Danger = ColorTranslator.FromHtml("#ef4444");
        public static readonly Color DangerTintBg = ColorTranslator.FromHtml("#241416");
        public static readonly Color DangerTintBorder = ColorTranslator.FromHtml("#6b2727");
        // Pending/switchover state is its own amber, distinct from the ember brand accent
        // (matches the reviewed mock's daemon-switchover treatment).
        public static readonly Color Pending = ColorTranslator.FromHtml("#fcd34d");
        public static readonly Color PendingTintBg = ColorTranslator.FromHtml("#231f18");
        public static readonly Color PendingTintBorder = ColorTranslator.FromHtml("#4a4020");

        public static readonly FontFamily HeadingFamily = ResolveFamily("Segoe UI Semibold", "Segoe UI");
        public static readonly FontFamily BodyFamily = ResolveFamily("Segoe UI", "Segoe UI");

        /// <summary>Fluent icon glyph font (present on every Win10/11 machine).</summary>
        public const string IconFontName = "Segoe MDL2 Assets";

        /// <summary>MDL2 glyph from a codepoint (e.g. 0xE72C = Refresh).</summary>
        public static string Glyph(int codepoint)
        {
            return char.ConvertFromUtf32(codepoint);
        }

        private static FontFamily ResolveFamily(string preferred, string fallback)
        {
            try { return new FontFamily(preferred); }
            catch { return new FontFamily(fallback); }
        }
    }
}
