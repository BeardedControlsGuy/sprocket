using System.Drawing;

namespace Sprocket
{
    /// <summary>Sprocket's standalone "Forge" palette — independent of any distributor's brand.</summary>
    internal static class SprocketTheme
    {
        // Surfaces
        public static readonly Color Ink = ColorTranslator.FromHtml("#0d0d10");
        public static readonly Color InkRaised = ColorTranslator.FromHtml("#17171b");
        public static readonly Color InkPanel = ColorTranslator.FromHtml("#1b1b20");
        public static readonly Color InkBorder = ColorTranslator.FromHtml("#2d2d34");

        // Brand
        public static readonly Color Ember = ColorTranslator.FromHtml("#f97316");
        public static readonly Color EmberLight = ColorTranslator.FromHtml("#fdba74");
        public static readonly Color Rust = ColorTranslator.FromHtml("#c2410c");
        public static readonly Color Flame = ColorTranslator.FromHtml("#dc2626");
        public static readonly Color Sun = ColorTranslator.FromHtml("#fcd34d");
        public static readonly Color SunLight = ColorTranslator.FromHtml("#fde68a");

        // Text
        public static readonly Color TextPrimary = ColorTranslator.FromHtml("#eef0f2");
        public static readonly Color TextMuted = ColorTranslator.FromHtml("#999ea6");
        public static readonly Color TextFaint = ColorTranslator.FromHtml("#5b5f67");

        // Status
        public static readonly Color Danger = ColorTranslator.FromHtml("#ef4444");
        public static readonly Color Success = ColorTranslator.FromHtml("#22c55e");

        public static readonly FontFamily HeadingFamily = ResolveFamily("Segoe UI Semibold", "Segoe UI");
        public static readonly FontFamily BodyFamily = ResolveFamily("Segoe UI", "Segoe UI");

        /// <summary>Fluent icon glyph font (present on every Win10/11 machine).</summary>
        public const string IconFontName = "Segoe MDL2 Assets";

        /// <summary>MDL2 glyph from a codepoint (e.g. 0xE72C = Refresh).</summary>
        public static string Glyph(int codepoint)
        {
            return char.ConvertFromUtf32(codepoint);
        }

        /// <summary>Letter-spaced micro-label text ("PLATFORM" → "P L A T F O R M").</summary>
        public static string Track(string s)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(s.Length * 2);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == ' ') { sb.Append("  "); continue; }
                sb.Append(c);
                if (i < s.Length - 1 && s[i + 1] != ' ') sb.Append(' ');
            }
            return sb.ToString();
        }

        private static FontFamily ResolveFamily(string preferred, string fallback)
        {
            try { return new FontFamily(preferred); }
            catch { return new FontFamily(fallback); }
        }
    }
}
