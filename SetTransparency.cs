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
    public class SetTransParency
    {
        [JSExport("setTransparency")]
        [SupportedOSPlatform("windows5.0")]
        public static bool SetTransparency(int pid, byte alpha)
        {
            var mainproc = Process.GetProcessById(pid);
            return (from proc in Process.GetProcessesByName(mainproc.ProcessName)
                    where !string.IsNullOrEmpty(proc.MainModule?.FileName) &&
                          proc.MainModule.FileName == mainproc.MainModule?.FileName
                    select proc.MainWindowHandle
                into hMainWnd
                    where hMainWnd != IntPtr.Zero
                    select PInvoke.GetWindowThreadProcessId(new HWND(hMainWnd), out _)
                into tid
                    select PInvoke.EnumThreadWindows(tid, delegate (HWND hWnd, LPARAM lParam)
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
                    }, default)).All(result => result);
        }
    }
}
