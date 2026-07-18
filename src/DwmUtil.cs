using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Sprocket
{
    /// <summary>Windows 11 DWM window chrome (rounded corners). No-op on older Windows.</summary>
    internal static class DwmUtil
    {
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

        /// <summary>Requests rounded corners for this form's title-bar window, matching native Win11 chrome.
        /// Call once the handle exists (e.g. from HandleCreated); safe to call repeatedly.</summary>
        public static void RequestRoundedCorners(Form form)
        {
            if (!form.IsHandleCreated) return;
            try
            {
                int preference = DWMWCP_ROUND;
                DwmSetWindowAttribute(form.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
            }
            catch
            {
                // Pre-Win11 / dwmapi unavailable — square corners, nothing to do.
            }
        }
    }
}
