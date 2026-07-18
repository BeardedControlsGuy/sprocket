using System;
using System.IO;

namespace Sprocket
{
    /// <summary>A detected Niagara install (brand-agnostic: Honeywell Optimizer, Distech EC-Net, etc).</summary>
    internal sealed class NiagaraPlatform
    {
        public string DisplayName;
        public string InstallDir;
        public string BrandId;
        public string BrandVersion;

        /// <summary>
        /// Niagara's per-user home, e.g. %USERPROFILE%\Niagara4.14\distech.
        ///
        /// The version segment must track the install's own major.minor — this used to be
        /// hardcoded to "Niagara4.15", so on a machine with several Niagara versions side by side
        /// every 4.14 install silently resolved to a 4.15 home that may not even exist, making
        /// nre.properties (memory settings) and navTree.xml (nav import/export) read and write
        /// the wrong platform's files.
        /// </summary>
        public string UserHomeDir
        {
            get
            {
                string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string brand = string.IsNullOrEmpty(BrandId) ? DisplayName : BrandId;
                string versionRoot = Path.Combine(baseDir, HomeVersionFolder);

                // If the version root exists, it is authoritative even when the brand folder inside
                // it doesn't yet — Niagara creates that on first run. Only when the whole root is
                // absent (an OEM numbering scheme we guessed wrong, e.g. JCI's FX "14.14") is it
                // safe to go looking elsewhere. Searching more eagerly than this would happily
                // resolve a 4.14 install to a 4.15 home, which is the exact bug this replaced.
                if (Directory.Exists(versionRoot)) return Path.Combine(versionRoot, brand);

                string fallback = FindExistingHome(baseDir, brand);
                return fallback ?? Path.Combine(versionRoot, brand);
            }
        }

        private static string FindExistingHome(string baseDir, string brand)
        {
            try
            {
                foreach (string root in Directory.GetDirectories(baseDir, "Niagara*"))
                {
                    string candidate = Path.Combine(root, brand);
                    if (Directory.Exists(candidate)) return candidate;
                }
            }
            catch { }
            return null;
        }

        /// <summary>"Niagara4.15" for N4, or "niagara" for the AX 3.x line, which never used a
        /// version-suffixed home folder.</summary>
        private string HomeVersionFolder
        {
            get
            {
                string version = VersionString;
                if (version == null) return "Niagara4.15";
                if (version.StartsWith("3.")) return "niagara";

                string[] parts = version.Split('.');
                if (parts.Length < 2) return "Niagara4.15";
                return "Niagara" + parts[0] + "." + parts[1];
            }
        }

        /// <summary>Version from brand.properties, falling back to the trailing version in the
        /// folder name (e.g. "OptimizerSupervisor-N4.15.1.16" -> "4.15.1.16").</summary>
        private string VersionString
        {
            get
            {
                if (!string.IsNullOrEmpty(BrandVersion)) return BrandVersion;
                if (string.IsNullOrEmpty(DisplayName)) return null;

                int dash = DisplayName.LastIndexOf('-');
                if (dash < 0 || dash == DisplayName.Length - 1) return null;

                string tail = DisplayName.Substring(dash + 1).TrimStart('N', 'n');
                return (tail.Length > 0 && char.IsDigit(tail[0])) ? tail : null;
            }
        }

        public string NavTreeXmlPath
        {
            get { return Path.Combine(UserHomeDir, "etc", "navTree.xml"); }
        }

        public string ModulesDir
        {
            get { return Path.Combine(InstallDir, "modules"); }
        }

        public string NreProperties
        {
            get { return Path.Combine(UserHomeDir, "etc", "nre.properties"); }
        }

        public string BrandPropertiesPath
        {
            get { return Path.Combine(InstallDir, "etc", "brand.properties"); }
        }

        public string BinDir
        {
            get { return Path.Combine(InstallDir, "bin"); }
        }

        public string WbExe
        {
            get { return Path.Combine(BinDir, "wb.exe"); }
        }

        public string WbWExe
        {
            get { return Path.Combine(BinDir, "wb_w.exe"); }
        }

        public string ConsoleExe
        {
            get { return Path.Combine(BinDir, "console.exe"); }
        }

        public string PlatExe
        {
            get { return Path.Combine(BinDir, "plat.exe"); }
        }

        public string CustomHomeOrd
        {
            get
            {
                string home = Path.Combine(InstallDir, "etc", "custom_home.html");
                return File.Exists(home) ? "file:!etc/custom_home.html" : null;
            }
        }

        public bool HasConsole
        {
            get { return File.Exists(ConsoleExe); }
        }

        public bool HasPlatDaemonInstaller
        {
            get { return File.Exists(PlatExe); }
        }

        public string Bitness
        {
            get { return PeInspector.IsX64(WbExe) ? "64-BIT" : "32-BIT"; }
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }
}
