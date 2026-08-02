using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using KeyboardLayoutIndicator.Interop;

namespace KeyboardLayoutIndicator.Fullscreen
{
    public static class FullscreenDetector
    {
        private static readonly HashSet<string> IgnoredClasses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Progman",
            "WorkerW",
            "Shell_TrayWnd",
            "Shell_SecondaryTrayWnd",
            "DV2ControlHost",
            "MsgrIMEWindowClass",
            "ConsoleWindowClass"
        };

        public static bool IsForegroundFullscreen()
        {
            IntPtr hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return false;
            if (!NativeMethods.IsWindowVisible(hwnd) || NativeMethods.IsIconic(hwnd)) return false;

            var sb = new StringBuilder(256);
            NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
            string className = sb.ToString();
            if (IgnoredClasses.Contains(className)) return false;

            if (!NativeMethods.GetWindowRect(hwnd, out var rect)) return false;

            IntPtr hMon = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            if (hMon == IntPtr.Zero) return false;

            var mi = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
            if (!NativeMethods.GetMonitorInfo(hMon, ref mi)) return false;

            bool coversMonitor =
                rect.Left <= mi.rcMonitor.Left &&
                rect.Top <= mi.rcMonitor.Top &&
                rect.Right >= mi.rcMonitor.Right &&
                rect.Bottom >= mi.rcMonitor.Bottom;

            if (!coversMonitor) return false;

            try
            {
                NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
                using var proc = System.Diagnostics.Process.GetProcessById((int)pid);
                if (string.Equals(proc.ProcessName, "explorer", StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            catch
            {
                //
            }

            return true;
        }
    }
}
