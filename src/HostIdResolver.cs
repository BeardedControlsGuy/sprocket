using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Sprocket
{
    /// <summary>
    /// Resolves a Niagara Host ID the honest way: boot the platform's own NRE via plat.exe
    /// and read the ID it prints, rather than guessing at Tridium's hashing algorithm.
    /// Slow (~15-25s cold boot) so results are cached to disk per install. Skips the attempt
    /// entirely if Workbench/Console/plat are already running against the same install —
    /// they hold a lock on the module registry and plat.exe hangs indefinitely waiting for it.
    /// </summary>
    internal static class HostIdResolver
    {
        private static readonly Regex HostIdPattern =
            new Regex(@"on\s+(Win-[0-9A-Fa-f-]+)\s*\(", RegexOptions.Compiled);

        public static void ResolveAsync(NiagaraPlatform platform, Action<string> onResolved)
        {
            string cachePath = CachePathFor(platform);
            string cached = ReadCache(cachePath);
            if (cached != null)
            {
                onResolved(cached);
                return;
            }

            if (AnotherNiagaraProcessIsUsing(platform))
            {
                onResolved("close Workbench to detect");
                return;
            }

            onResolved("detecting…");

            Thread t = new Thread(delegate()
            {
                string hostId = ResolveViaPlat(platform);
                if (hostId != null) WriteCache(cachePath, hostId);
                onResolved(hostId ?? "unavailable");
            });
            t.IsBackground = true;
            t.Start();
        }

        private static bool AnotherNiagaraProcessIsUsing(NiagaraPlatform platform)
        {
            string[] names = { "wb", "wb_w", "plat", "station" };
            foreach (string name in names)
            {
                Process[] procs = Process.GetProcessesByName(name);
                foreach (Process p in procs)
                {
                    try
                    {
                        string path = p.MainModule.FileName;
                        if (path.StartsWith(platform.InstallDir, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    catch { /* inaccessible (different user/elevated) — ignore, not ours to worry about */ }
                }
            }
            return false;
        }

        private static string ResolveViaPlat(NiagaraPlatform platform)
        {
            if (!File.Exists(platform.PlatExe)) return null;

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = platform.PlatExe;
                psi.WorkingDirectory = platform.InstallDir;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;

                StringBuilder combined = new StringBuilder();
                object sync = new object();

                using (Process p = new Process())
                {
                    p.StartInfo = psi;
                    p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                    {
                        if (e.Data != null) lock (sync) combined.AppendLine(e.Data);
                    };
                    p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                    {
                        if (e.Data != null) lock (sync) combined.AppendLine(e.Data);
                    };

                    p.Start();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();

                    bool exited = p.WaitForExit(75000);
                    if (!exited)
                    {
                        try { p.Kill(); } catch { }
                        return null;
                    }
                }

                Match m;
                lock (sync) { m = HostIdPattern.Match(combined.ToString()); }
                return m.Success ? m.Groups[1].Value : null;
            }
            catch
            {
                return null;
            }
        }

        private static string CachePathFor(NiagaraPlatform platform)
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sprocket");
            string safeName = platform.InstallDir.Replace(':', '_').Replace('\\', '_').Replace(' ', '_');
            return Path.Combine(dir, "hostid_" + safeName + ".txt");
        }

        private static string ReadCache(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    string v = File.ReadAllText(path).Trim();
                    if (v.Length > 0) return v;
                }
            }
            catch { }
            return null;
        }

        private static void WriteCache(string path, string value)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, value);
            }
            catch { }
        }
    }
}
