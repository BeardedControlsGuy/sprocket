using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Sprocket
{
    /// <summary>Persisted user settings: extra Niagara scan folders (WorkPlace Launcher's
    /// "+Folders" feature, its wpfolder key) and the Workbench UI language (its deflang key).</summary>
    internal sealed class UserSettings
    {
        public readonly List<string> Folders = new List<string>();
        public string Locale = "en";

        private static string SettingsFile
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Sprocket", "settings.txt");
            }
        }

        public static UserSettings Load()
        {
            UserSettings s = new UserSettings();
            try
            {
                if (!File.Exists(SettingsFile)) return s;
                foreach (string raw in File.ReadAllLines(SettingsFile))
                {
                    string line = raw.Trim();
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;

                    string key = line.Substring(0, eq).Trim();
                    string value = line.Substring(eq + 1).Trim();
                    if (value.Length == 0) continue;

                    if (string.Equals(key, "folder", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!ContainsIgnoreCase(s.Folders, value)) s.Folders.Add(value);
                    }
                    else if (string.Equals(key, "locale", StringComparison.OrdinalIgnoreCase))
                    {
                        s.Locale = value;
                    }
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(SettingsFile);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                StringBuilder sb = new StringBuilder();
                foreach (string f in Folders)
                    sb.AppendLine("folder=" + f);
                sb.AppendLine("locale=" + Locale);
                File.WriteAllText(SettingsFile, sb.ToString());
            }
            catch { }
        }

        public static bool ContainsIgnoreCase(List<string> list, string value)
        {
            foreach (string item in list)
            {
                if (string.Equals(item, value, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
