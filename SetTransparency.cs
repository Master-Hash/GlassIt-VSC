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
            var mainproc = Process.GetProcessById(pid);
            foreach (var proc in Process.GetProcessesByName(mainproc.ProcessName))
            {
                if (!string.IsNullOrEmpty(proc.MainModule?.FileName) &&
                    proc.MainModule?.FileName != mainproc.MainModule?.FileName)
                    continue;

                var hMainWnd = (HWND)proc.MainWindowHandle;
                if (hMainWnd == HWND.Null)
                    continue;

                var tid = PInvoke.GetWindowThreadProcessId(hMainWnd, out uint _);

                var result = PInvoke.EnumThreadWindows(tid, (hWnd, _) =>
                {
                    if (!PInvoke.IsWindowVisible(hWnd))
                        return true;

                    var exStyle = (WINDOW_EX_STYLE)PInvoke.GetWindowLongPtr(hWnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
                    PInvoke.SetWindowLongPtr(hWnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE,
                        (int)(exStyle | WINDOW_EX_STYLE.WS_EX_LAYERED));
                    return PInvoke.SetLayeredWindowAttributes(hWnd, default, alpha,
                        LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA);
                }, default);

                if (!result)
                    return false;
            }

            return true;
        }
    }
}
