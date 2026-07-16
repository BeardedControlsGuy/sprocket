using System;
using System.Diagnostics;
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

    /// <summary>Finds and controls the Windows service backing a Niagara platform daemon.</summary>
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

            if (RunSc(args, elevate: false) == 0) return true;
            return RunSc(args, elevate: true) == 0;
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
                    // easily take longer than a few seconds — give it real time instead of the old
                    // 15s cap, and never touch ExitCode unless WaitForExit actually confirms exit
                    // (doing so on a still-running process throws, which the old code silently
                    // swallowed as a generic failure).
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
                            object imagePath = svc.GetValue("ImagePath");
                            if (imagePath == null) continue;

                            string path = imagePath.ToString();
                            if (path.IndexOf(installDir, StringComparison.OrdinalIgnoreCase) >= 0)
                                return name;
                        }
                    }
                }
            }
            catch { }

            return null;
        }
    }
}
