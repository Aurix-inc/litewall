using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace LiteWall
{
    public partial class WallpaperWindow : Window
    {
        private readonly DispatcherTimer _perfTimer;
        private bool _isPaused = false;
        private IntPtr _mouseHookId = IntPtr.Zero;
        private LowLevelMouseProc? _mouseProc;

        public WallpaperWindow()
        {
            InitializeComponent();
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;

            _perfTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            _perfTimer.Tick += CheckPerformanceState;
            _perfTimer.Start();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            DesktopWorker.AttachToDesktop(hwnd);
            await WebView.EnsureCoreWebView2Async();
            WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            WebView.CoreWebView2.Settings.AreDevToolsEnabled = false;

            // Start listening for clicks on empty desktop space
            _mouseProc = HookCallback;
            _mouseHookId = SetHook(_mouseProc);
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_mouseHookId != IntPtr.Zero) UnhookWindowsHookEx(_mouseHookId);
            base.OnClosed(e);
        }

        public async void LoadFile(string filePath)
        {
            if (!File.Exists(filePath)) return;
            await WebView.EnsureCoreWebView2Async();

            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            string uri = new Uri(filePath).AbsoluteUri;

            if (ext == ".html" || ext == ".htm")
            {
                WebView.CoreWebView2.Navigate(uri);
            }
            else if (ext == ".mp4" || ext == ".webm" || ext == ".mkv")
            {
                string html = $@"
                <html><body style='margin:0;overflow:hidden;background:#000;'>
                <video id='vid' src='{uri}' autoplay loop muted playsinline 
                       style='width:100vw;height:100vh;object-fit:cover;'></video>
                </body></html>";
                WebView.NavigateToString(html);
            }
            else
            {
                string html = $@"
                <html><body style='margin:0;overflow:hidden;background:#000;'>
                <img src='{uri}' style='width:100vw;height:100vh;object-fit:cover;'/>
                </body></html>";
                WebView.NavigateToString(html);
            }
        }

        private async void CheckPerformanceState(object? sender, EventArgs e)
        {
            if (WebView.CoreWebView2 == null) return;

            bool shouldPause = DesktopWorker.IsFullscreenOrMaximizedAppRunning(
                (int)SystemParameters.PrimaryScreenWidth,
                (int)SystemParameters.PrimaryScreenHeight);

            if (shouldPause && !_isPaused)
            {
                _isPaused = true;
                await WebView.CoreWebView2.ExecuteScriptAsync(
                    "document.querySelectorAll('video').forEach(v => v.pause()); window.__paused = true;");
            }
            else if (!shouldPause && _isPaused)
            {
                _isPaused = false;
                await WebView.CoreWebView2.ExecuteScriptAsync(
                    "document.querySelectorAll('video').forEach(v => v.play()); window.__paused = false;");
            }
        }

        // --- Low-Level Mouse Forwarding to HTML Wallpaper ---
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData, flags, time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT Point);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        private IntPtr SetHook(LowLevelMouseProc proc)
        {
            using Process curProcess = Process.GetCurrentProcess();
            using ProcessModule curModule = curProcess.MainModule!;
            return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONDOWN && WebView.CoreWebView2 != null)
            {
                MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                IntPtr clickedWnd = WindowFromPoint(hookStruct.pt);

                StringBuilder cls = new StringBuilder(256);
                GetClassName(clickedWnd, cls, cls.Capacity);
                string className = cls.ToString();

                // Only forward clicks when clicking on the desktop layer
                if (className == "SysListView32" || className == "WorkerW" || className == "SHELLDLL_DefView")
                {
                    var source = PresentationSource.FromVisual(this);
                    double dpiX = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
                    double dpiY = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;

                    int cssX = (int)(hookStruct.pt.x * dpiX);
                    int cssY = (int)(hookStruct.pt.y * dpiY);

                    Dispatcher.BeginInvoke(async () =>
                    {
                        string js = $"(() => {{ const el = document.elementFromPoint({cssX}, {cssY}); if (el) el.click(); }})();";
                        await WebView.CoreWebView2.ExecuteScriptAsync(js);
                    });
                }
            }
            return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
        }
    }
}
