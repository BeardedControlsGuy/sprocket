using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace Sprocket
{
    internal static class ProcessLauncher
    {
        public static void LaunchWorkbench(NiagaraPlatform platform)
        {
            string args = "-profile:workbench:WbProfile" + LocaleArg();
            string home = platform.CustomHomeOrd;
            if (home != null) args += " " + home;

            Start(platform.WbWExe, args, platform.InstallDir, "Workbench");
        }

        /// <summary>Workbench with its console window left visible alongside the GUI — matches
        /// WorkPlace Launcher's "program-console" mode, useful for seeing stack traces that
        /// don't make it into a dialog.</summary>
        public static void LaunchWorkbenchWithConsole(NiagaraPlatform platform)
        {
            string args = "-profile:workbench:WbProfile" + LocaleArg();
            string home = platform.CustomHomeOrd;
            if (home != null) args += " " + home;

            Start(platform.WbExe, args, platform.InstallDir, "Workbench (with console)");
        }

        public static void LaunchAlarmPortal(NiagaraPlatform platform)
        {
            Start(platform.WbWExe, "-profile:alarm:AlarmPortalProfile" + LocaleArg(),
                platform.InstallDir, "Alarm Portal");
        }

        /// <summary>Workbench UI language, matching WorkPlace Launcher's -locale flag.</summary>
        private static string LocaleArg()
        {
            string locale = UserSettings.Load().Locale;
            if (string.IsNullOrEmpty(locale)) return "";
            return " -locale:" + locale;
        }

        public static void LaunchConsole(NiagaraPlatform platform)
        {
            Start(platform.ConsoleExe, "", platform.InstallDir, "Console");
        }

        public static void LaunchPlatformDaemonInstaller(NiagaraPlatform platform)
        {
            Start(platform.PlatExe, "installdaemon", platform.InstallDir, "Platform Daemon installer");
        }

        public static void OpenInstallFolder(NiagaraPlatform platform)
        {
            Start("explorer.exe", "\"" + platform.InstallDir + "\"", platform.InstallDir, "Explorer");
        }

        public static void OpenUrl(string url)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = url;
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Couldn't open the browser:\r\n" + ex.Message, "Sprocket",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void Start(string exePath, string args, string workingDir, string friendlyName)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = exePath;
                psi.Arguments = args;
                psi.WorkingDirectory = workingDir;
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Couldn't start " + friendlyName + ":\r\n" + ex.Message,
                    "Sprocket",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
