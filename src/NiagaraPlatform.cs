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

        public string UserHomeDir
        {
            get
            {
                string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string brand = string.IsNullOrEmpty(BrandId) ? DisplayName : BrandId;
                return Path.Combine(baseDir, "Niagara4.15", brand);
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
