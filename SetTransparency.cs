using Microsoft.JavaScript.NodeApi;
using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.JavaScript.NodeApi.Interop;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace GlassIt
{
    [JSModule]
    public class Transparency
    {
        [JSExport("setTransparency")]
        [SupportedOSPlatform("windows5.0")]
        public static bool SetTransparency(int pid, byte alpha)
        {
            using var mainproc = Process.GetProcessById(pid);
            var executablePath = GetMainModuleFileName(mainproc);
            if (string.IsNullOrEmpty(executablePath))
            {
                return false;
            }

            var matchedWindow = false;
            var success = true;

            foreach (var proc in Process.GetProcessesByName(mainproc.ProcessName))
            {
                using (proc)
                {
                    if (proc is null) { continue; }
                    if (!string.Equals(GetMainModuleFileName(proc), executablePath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    var hMainWnd = proc.MainWindowHandle;
                    if (hMainWnd == IntPtr.Zero)
                    {
                        continue;
                    }

                    var tid = PInvoke.GetWindowThreadProcessId(new HWND(hMainWnd), out _);
                    if (tid == 0)
                    {
                        success = false;
                        continue;
                    }

                    matchedWindow = true;
                    success &= PInvoke.EnumThreadWindows(tid, (hWnd, _) => SetWindowAlpha(hWnd, alpha), default);
                }
            }

            return matchedWindow && success;
        }

        private static string? GetMainModuleFileName(Process process)
        {
            return process.MainModule?.FileName;
        }

        [SupportedOSPlatform("windows5.0")]
        private static BOOL SetWindowAlpha(HWND hWnd, byte alpha)
        {
            if (!PInvoke.IsWindowVisible(hWnd))
            {
                return true;
            }

            var windowLong = PInvoke.GetWindowLongPtr(hWnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
            PInvoke.SetWindowLongPtr(
                hWnd,
                WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE,
                windowLong | (nint)WINDOW_EX_STYLE.WS_EX_LAYERED);

            return PInvoke.SetLayeredWindowAttributes(
                hWnd,
                new COLORREF(0),
                alpha,
                LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA);
        }
    }
}
