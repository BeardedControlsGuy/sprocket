using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Win32;

namespace Sprocket
{
    internal enum DaemonState
    {
        Unknown,
        Running,
        Stopped,
        Starting,
        Stopping
    }

    /// <summary>Outcome of an elevated daemon operation, carrying plat.exe's own console output
    /// so failures can be shown to the user instead of vanishing with the console window.</summary>
    internal sealed class DaemonOpResult
    {
        public bool Ok;
        public bool Cancelled;   // user dismissed the UAC prompt — not an error worth a red dialog
        public int ExitCode;
        public string Output;

        public string FriendlyError
        {
            get
            {
                if (Cancelled) return "Administrator approval was declined.";
                string text = (Output == null) ? "" : Output.Trim();
                if (text.Length > 0) return text;
                return "plat.exe exited with code " + ExitCode + ".";
            }
        }
    }

    /// <summary>
    /// Finds and controls the Windows service backing a Niagara platform daemon.
    ///
    /// Key fact about Niagara on Windows: there is exactly ONE daemon service (named "Niagara"),
    /// no matter how many platforms are installed. It points at whichever install last ran
    /// <c>plat.exe installdaemon</c>. You therefore do NOT start a per-platform service —
    /// switching platforms means re-registering that single service against the new install.
    /// See <see cref="InstallDaemon"/>.
    /// </summary>
    internal static class DaemonStatus
    {
        public static string ServiceNameFor(NiagaraPlatform platform)
        {
            return FindServiceName(platform.InstallDir);
        }

        public static DaemonState Query(NiagaraPlatform platform)
        {
            string serviceName = FindServiceName(platform.InstallDir);
            if (serviceName == null) return DaemonState.Unknown;
            return QueryService(serviceName);
        }

        /// <summary>Install directory the one Niagara service currently points at, or null if no
        /// daemon service is registered at all.</summary>
        public static string RegisteredInstallDir()
        {
            string imagePath;
            if (FindDaemonService(out imagePath) == null) return null;
            return InstallDirFromImagePath(imagePath);
        }

        private static DaemonState QueryService(string serviceName)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "sc.exe";
                psi.Arguments = "query \"" + serviceName + "\"";
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.CreateNoWindow = true;

                using (Process p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(3000);

                    // Check the PENDING states before RUNNING/STOPPED — a daemon that's mid-boot
                    // (common: Niagara stations can take 10-60s) reports START_PENDING, which
                    // contains neither "RUNNING" nor "STOPPED" and was previously misread as
                    // Unknown, silently reverting the toggle button right after the user clicked it.
                    if (output.IndexOf("STOP_PENDING", StringComparison.OrdinalIgnoreCase) >= 0)
                        return DaemonState.Stopping;
                    if (output.IndexOf("START_PENDING", StringComparison.OrdinalIgnoreCase) >= 0)
                        return DaemonState.Starting;
                    if (output.IndexOf("RUNNING", StringComparison.OrdinalIgnoreCase) >= 0)
                        return DaemonState.Running;
                    if (output.IndexOf("STOPPED", StringComparison.OrdinalIgnoreCase) >= 0)
                        return DaemonState.Stopped;
                }
            }
            catch { }

            return DaemonState.Unknown;
        }

        /// <summary>Polls until the service reaches a terminal Running/Stopped state or the timeout
        /// elapses, so callers see the real outcome instead of whatever transient state existed
        /// right after issuing the start/stop command.</summary>
        public static DaemonState WaitForSettled(string serviceName, int timeoutMs)
        {
            int waited = 0;
            const int intervalMs = 400;
            DaemonState state = QueryService(serviceName);
            while ((state == DaemonState.Starting || state == DaemonState.Stopping) && waited < timeoutMs)
            {
                Thread.Sleep(intervalMs);
                waited += intervalMs;
                state = QueryService(serviceName);
            }
            return state;
        }

        /// <summary>Starts or stops the service, elevating via UAC only if the plain attempt is denied access.</summary>
        public static bool SetRunning(string serviceName, bool running)
        {
            string verb = running ? "start" : "stop";
            string args = verb + " \"" + serviceName + "\"";

            if (RunSc(args, false) == 0) return true;
            return RunSc(args, true) == 0;
        }

        /// <summary>
        /// Points the single Niagara daemon service at <paramref name="platform"/> by running that
        /// install's own <c>plat.exe installdaemon</c>, which stops and deletes any existing daemon
        /// service first, then registers and starts this one.
        ///
        /// This MUST be elevated: plat.exe is manifested asInvoker (it never self-elevates) and
        /// installdaemon calls OpenSCManager/CreateService, which require administrator. Running it
        /// unelevated — as Sprocket did through v3.1.2 — fails with "cannot open service manager",
        /// and because plat.exe is a console app started via ShellExecute the window flashes shut
        /// before anyone can read it, so the whole thing looks like the button simply does nothing.
        /// That was the root cause of "Sprocket can't launch the daemon".
        /// </summary>
        public static DaemonOpResult InstallDaemon(NiagaraPlatform platform)
        {
            return RunPlatElevated(platform, "installdaemon");
        }

        /// <summary>Removes the Niagara daemon service registered against this platform.</summary>
        public static DaemonOpResult UninstallDaemon(NiagaraPlatform platform)
        {
            return RunPlatElevated(platform, "uninstalldaemon");
        }

        /// <summary>
        /// Runs a plat.exe subcommand elevated, capturing its console output.
        ///
        /// ShellExecute + runas is the only way to trigger UAC, but it forbids stream redirection —
        /// so the command is wrapped in cmd.exe with the output redirected to a temp file, which is
        /// read back once the process exits. Without this, plat.exe's diagnostics ("cannot open
        /// service manager", "service could not be started", ...) are lost entirely.
        /// </summary>
        private static DaemonOpResult RunPlatElevated(NiagaraPlatform platform, string subCommand)
        {
            DaemonOpResult result = new DaemonOpResult();
            result.ExitCode = -1;

            if (!platform.HasPlatDaemonInstaller)
            {
                result.Output = "This install has no bin\\plat.exe, so its daemon can't be registered.";
                return result;
            }

            string logPath = Path.Combine(Path.GetTempPath(),
                "sprocket_plat_" + Guid.NewGuid().ToString("N") + ".log");

            // cmd strips the outermost pair of quotes with /c, so the inner command keeps its own
            // quoting intact. cd /d first: plat.exe resolves niagara_home relative to where it runs.
            string inner = "cd /d \"" + platform.InstallDir + "\" && "
                + "\"" + platform.PlatExe + "\" " + subCommand
                + " > \"" + logPath + "\" 2>&1";

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "cmd.exe";
                psi.Arguments = "/c \"" + inner + "\"";
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                psi.WindowStyle = ProcessWindowStyle.Hidden;

                using (Process p = Process.Start(psi))
                {
                    // Generous: the user has to answer a UAC prompt, and installdaemon stops the
                    // outgoing daemon (which can take a while) before starting the new one.
                    if (!p.WaitForExit(180000))
                    {
                        try { p.Kill(); } catch { }
                        result.Output = "Timed out waiting for plat.exe " + subCommand + ".";
                        return result;
                    }
                    result.ExitCode = p.ExitCode;
                }
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // 1223 == ERROR_CANCELLED: the UAC consent dialog was dismissed.
                if (ex.NativeErrorCode == 1223) result.Cancelled = true;
                else result.Output = ex.Message;
                return result;
            }
            catch (Exception ex)
            {
                result.Output = ex.Message;
                return result;
            }
            finally
            {
                try
                {
                    if (File.Exists(logPath))
                    {
                        result.Output = File.ReadAllText(logPath);
                        File.Delete(logPath);
                    }
                }
                catch { }
            }

            // plat.exe is not reliable about its exit code, so trust the service registry over it:
            // the operation worked if the daemon service now points where we asked.
            string nowAt = RegisteredInstallDir();
            bool registeredHere = nowAt != null && SamePath(nowAt, platform.InstallDir);
            result.Ok = (subCommand == "uninstalldaemon") ? (nowAt == null) : registeredHere;
            return result;
        }

        private static int RunSc(string args, bool elevate)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "sc.exe";
                psi.Arguments = args;

                if (elevate)
                {
                    psi.UseShellExecute = true;
                    psi.Verb = "runas";
                    psi.WindowStyle = ProcessWindowStyle.Hidden;
                }
                else
                {
                    psi.UseShellExecute = false;
                    psi.RedirectStandardOutput = true;
                    psi.CreateNoWindow = true;
                }

                using (Process p = Process.Start(psi))
                {
                    // The elevated path blocks on a human responding to the UAC prompt, which can
                    // easily take longer than a few seconds — give it real time, and never touch
                    // ExitCode unless WaitForExit actually confirms exit (doing so on a still-running
                    // process throws, which the old code silently swallowed as a generic failure).
                    int timeoutMs = elevate ? 120000 : 15000;
                    if (!p.WaitForExit(timeoutMs))
                    {
                        try { p.Kill(); } catch { }
                        return -1;
                    }
                    return p.ExitCode;
                }
            }
            catch
            {
                return -1;
            }
        }

        private static string FindServiceName(string installDir)
        {
            string imagePath;
            string name = FindDaemonService(out imagePath);
            if (name == null) return null;

            string owner = InstallDirFromImagePath(imagePath);
            return (owner != null && SamePath(owner, installDir)) ? name : null;
        }

        /// <summary>Locates the registered Niagara daemon service, whichever install it belongs to.</summary>
        private static string FindDaemonService(out string imagePath)
        {
            imagePath = null;
            try
            {
                using (RegistryKey services = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services"))
                {
                    if (services == null) return null;

                    foreach (string name in services.GetSubKeyNames())
                    {
                        using (RegistryKey svc = services.OpenSubKey(name))
                        {
                            if (svc == null) continue;
                            object value = svc.GetValue("ImagePath");
                            if (value == null) continue;

                            string path = value.ToString();
                            // Match the daemon binary itself rather than doing a loose substring test
                            // against the install directory — an install path that happens to be a
                            // prefix of another (or of some unrelated service's path) would otherwise
                            // claim the wrong service.
                            if (path.IndexOf("niagarad.exe", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                imagePath = path;
                                return name;
                            }
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>"C:\Niagara\Foo-4.15\bin\niagarad.exe" (quoted or not) -> "C:\Niagara\Foo-4.15".</summary>
        private static string InstallDirFromImagePath(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath)) return null;
            try
            {
                string path = imagePath.Trim();

                // ImagePath may be quoted and may carry trailing service arguments.
                if (path.StartsWith("\""))
                {
                    int close = path.IndexOf('"', 1);
                    path = (close > 1) ? path.Substring(1, close - 1) : path.Replace("\"", "");
                }
                else
                {
                    int exe = path.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                    if (exe >= 0) path = path.Substring(0, exe + 4);
                }

                string binDir = Path.GetDirectoryName(path);          // ...\bin
                if (string.IsNullOrEmpty(binDir)) return null;
                return Path.GetDirectoryName(binDir);                 // ...\<install>
            }
            catch { return null; }
        }

        private static bool SamePath(string a, string b)
        {
            if (a == null || b == null) return false;
            return string.Equals(a.TrimEnd('\\'), b.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
        }
    }
}
