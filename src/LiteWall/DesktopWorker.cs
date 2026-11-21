using System;
using System.Runtime.InteropServices;
using System.Text;

namespace LiteWall
{
    public static class DesktopWorker
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(
            IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam,
            uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(
            IntPtr parentHandle, IntPtr childAfter, string className, string? windowTitle);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        public static void AttachToDesktop(IntPtr windowHandle)
        {
            IntPtr progman = FindWindow("Progman", null);
            SendMessageTimeout(progman, 0x052C, new IntPtr(0), IntPtr.Zero, 0x0, 1000, out _);

            IntPtr workerw = IntPtr.Zero;
            IntPtr hWnd = IntPtr.Zero;

            while ((hWnd = FindWindowEx(IntPtr.Zero, hWnd, "WorkerW", null)) != IntPtr.Zero)
            {
                IntPtr shellView = FindWindowEx(hWnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellView != IntPtr.Zero)
                {
                    workerw = FindWindowEx(IntPtr.Zero, hWnd, "WorkerW", null);
                    break;
                }
            }

            if (workerw != IntPtr.Zero)
            {
                SetParent(windowHandle, workerw);
            }
        }

        public static bool IsFullscreenOrMaximizedAppRunning(int screenWidth, int screenHeight)
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero) return false;

            // Ignore LiteWall's own windows
            GetWindowThreadProcessId(fg, out uint fgPid);
            if (fgPid == (uint)Environment.ProcessId) return false;

            // Ignore the Windows Desktop itself (Progman, WorkerW, or the desktop icon list)
            StringBuilder className = new StringBuilder(256);
            GetClassName(fg, className, className.Capacity);
            string cls = className.ToString();
            if (cls == "Progman" || cls == "WorkerW" || cls == "SHELLDLL_DefView" || cls == "SysListView32")
            {
                return false;
            }

            if (GetWindowRect(fg, out RECT rect))
            {
                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;
                return width >= screenWidth && height >= screenHeight;
            }
            return false;
        }
    }
}
