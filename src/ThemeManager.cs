using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Sprocket
{
    /// <summary>Workbench theme picker — port of WorkPlace Launcher's checkThemes/saveTheme/setTheme:
    /// find *theme*.jar modules and write the chosen one into brand.properties.</summary>
    internal static class ThemeManager
    {
        /// <summary>Theme names found in the platform's \modules\ folder, derived from jar
        /// filenames the same way WPL did (strip "-ux" and "theme" from the base name).</summary>
        public static List<string> FindThemes(NiagaraPlatform platform)
        {
            List<string> names = new List<string>();
            string[] files;
            try { files = Directory.GetFiles(platform.ModulesDir, "*.jar"); }
            catch { return names; }

            foreach (string file in files)
            {
                string baseName = Path.GetFileNameWithoutExtension(file);
                if (baseName.IndexOf("theme", StringComparison.OrdinalIgnoreCase) < 0) continue;

                string name = Regex.Replace(baseName, "-ux", "", RegexOptions.IgnoreCase);
                name = Regex.Replace(name, "theme", "", RegexOptions.IgnoreCase).Trim('-', '_');
                if (name.Length == 0) name = baseName;

                if (!ContainsIgnoreCase(names, name)) names.Add(name);
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        public static string GetCurrentTheme(NiagaraPlatform platform)
        {
            string value;
            return ReadProperties(platform.BrandPropertiesPath).TryGetValue("workbench.theme.default", out value)
                ? value : null;
        }

        /// <summary>Sets workbench.theme.default and locks it (workbench.theme.locked=true) so a
        /// user can't override it from inside Workbench itself — same behavior as WPL.</summary>
        public static bool SetTheme(NiagaraPlatform platform, string themeName)
        {
            string path = platform.BrandPropertiesPath;
            try
            {
                string[] existing = File.Exists(path) ? File.ReadAllLines(path) : new string[0];
                List<string> outLines = new List<string>();
                bool wroteDefault = false, wroteLocked = false;

                foreach (string raw in existing)
                {
                    string trimmed = raw.TrimStart();
                    if (trimmed.StartsWith("workbench.theme.default", StringComparison.OrdinalIgnoreCase))
                    {
                        outLines.Add("workbench.theme.default=" + themeName);
                        wroteDefault = true;
                    }
                    else if (trimmed.StartsWith("workbench.theme.locked", StringComparison.OrdinalIgnoreCase))
                    {
                        outLines.Add("workbench.theme.locked=true");
                        wroteLocked = true;
                    }
                    else
                    {
                        outLines.Add(raw);
                    }
                }
                if (!wroteDefault) outLines.Add("workbench.theme.default=" + themeName);
                if (!wroteLocked) outLines.Add("workbench.theme.locked=true");

                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllLines(path, outLines.ToArray());
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool ContainsIgnoreCase(List<string> list, string value)
        {
            foreach (string s in list)
                if (string.Equals(s, value, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static Dictionary<string, string> ReadProperties(string path)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            string[] lines;
            try { lines = File.ReadAllLines(path); }
            catch { return result; }

            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                int eq = line.IndexOf('=');
                if (eq <= 0) continue;

                result[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            return result;
        }
    }
}
