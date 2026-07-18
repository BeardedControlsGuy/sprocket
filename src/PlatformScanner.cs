using System;
using System.Collections.Generic;
using System.IO;

namespace Sprocket
{
    /// <summary>Finds installed Niagara platforms by looking for &lt;root&gt;\&lt;install&gt;\bin\wb.exe.</summary>
    internal static class PlatformScanner
    {
        private static readonly string[] ScanRoots = new[]
        {
            @"C:\",
            @"C:\Program Files",
            @"C:\Program Files (x86)"
        };

        public static List<NiagaraPlatform> Scan()
        {
            List<NiagaraPlatform> found = new List<NiagaraPlatform>();
            List<string> seen = new List<string>();

            List<string> roots = new List<string>(ScanRoots);
            foreach (string custom in UserSettings.Load().Folders)
            {
                if (!UserSettings.ContainsIgnoreCase(roots, custom))
                    roots.Add(custom);
            }

            foreach (string root in roots)
            {
                // Installs typically live two levels down, e.g. C:\Honeywell\OptimizerSupervisor-N4.15.1.16\bin\wb.exe
                CheckAndDescend(root, found, seen, 2);
            }

            found.Sort(delegate(NiagaraPlatform a, NiagaraPlatform b)
            {
                return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
            });

            return found;
        }

        /// <summary>Scan a single root (used to sanity-check a folder the user adds).</summary>
        public static List<NiagaraPlatform> ScanRoot(string root)
        {
            List<NiagaraPlatform> found = new List<NiagaraPlatform>();
            CheckAndDescend(root, found, new List<string>(), 2);
            return found;
        }

        private static void CheckAndDescend(string dir, List<NiagaraPlatform> found, List<string> seen, int depthRemaining)
        {
            string wbExe = Path.Combine(dir, "bin", "wb.exe");
            if (File.Exists(wbExe))
            {
                if (UserSettings.ContainsIgnoreCase(seen, dir)) return;
                seen.Add(dir);
                NiagaraPlatform p = Describe(dir);
                if (p != null) found.Add(p);
                return;
            }

            if (depthRemaining <= 0) return;

            string[] subDirs;
            try { subDirs = Directory.GetDirectories(dir); }
            catch { return; }

            foreach (string sub in subDirs)
                CheckAndDescend(sub, found, seen, depthRemaining - 1);
        }

        private static NiagaraPlatform Describe(string installDir)
        {
            NiagaraPlatform p = new NiagaraPlatform();
            p.InstallDir = installDir;
            // Always the folder name (e.g. "OptimizerSupervisor-N4.15.1.16") — matches
            // WorkPlace Launcher's own dropdown label and, unlike brand.properties'
            // workbench.title below, is guaranteed short and unique. Also used to build
            // UserHomeDir when brand.id is missing, so it needs to stay path-safe.
            p.DisplayName = Path.GetFileName(installDir);

            string brandPropsPath = Path.Combine(installDir, "etc", "brand.properties");
            if (File.Exists(brandPropsPath))
            {
                Dictionary<string, string> props = ReadProperties(brandPropsPath);

                // workbench.title is meant for a Workbench window title / splash banner, not
                // a short list label — some OEM brand.properties (seen on a Honeywell
                // OptimizerSupervisor install) set it to a full sentence like "Welcome to
                // Optimizer Supervisor", which showed up verbatim as the platform's name
                // everywhere (dropdown, daemon messages, tray tooltip). Never trust it as
                // DisplayName; folder name above is always what's shown.

                string brandId;
                if (props.TryGetValue("brand.id", out brandId))
                    p.BrandId = brandId;

                string brandVersion;
                if (props.TryGetValue("brand.version", out brandVersion))
                    p.BrandVersion = brandVersion;
            }

            return p;
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

                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                result[key] = value;
            }

            return result;
        }
    }
}
