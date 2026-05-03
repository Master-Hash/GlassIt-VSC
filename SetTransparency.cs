using Microsoft.JavaScript.NodeApi;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Windows;
using Microsoft.JavaScript.NodeApi.Interop;

namespace GlassIt
{
    [JSModule]
    public class SetTransParency
    {
        [JSExport("setTransparency")]
        public static bool SetTransparency(int pid, byte alpha)
        {
            Process mainproc = Process.GetProcessById(pid);
            return (from proc in Process.GetProcessesByName(mainproc.ProcessName)
                    // where proc.StartInfo.FileName == mainproc.StartInfo.FileName
                    select proc.MainWindowHandle
                into hMainWnd
                    where hMainWnd != IntPtr.Zero
                    select User32.GetWindowThreadProcessId(hMainWnd, out pid)
                into tid
                    select User32.EnumThreadWindows(tid, delegate (IntPtr hWnd, IntPtr lParam)
                    {
                        if (!User32.IsWindowVisible(hWnd))
                        {
                            return true;
                        }

                        var windowLong = User32.GetWindowLong(hWnd, GWL.EXSTYLE);
                        User32.SetWindowLong(hWnd, GWL.EXSTYLE, windowLong | WS.EX_LAYERED);
                        return User32.SetLayeredWindowAttributes(hWnd, 0, alpha, LWA.ALPHA);
                    }, IntPtr.Zero)).All(result => result);
        }
    }
}

namespace Windows
{
    internal static partial class User32
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool EnumThreadWindows(uint dwThreadId, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [LibraryImport("user32.dll")]
        public static partial uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool IsWindowVisible(IntPtr hWnd);

        [LibraryImport("user32.dll")]
        public static partial WS GetWindowLong(IntPtr hWnd, GWL nIndex);

        [LibraryImport("user32.dll")]
        public static partial int SetWindowLong(IntPtr hWnd, GWL nIndex, WS dwNewLong);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, LWA dwFlags);
    }

    internal enum GWL : int
    {
        EXSTYLE = -20,
        HINSTANCE = -6,
        HWNDPARENT = -8,
        ID = -12,
        STYLE = -16,
        USERDATA = -21,
        WNDPROC = -4,
    }

    [Flags]
    internal enum WS : int
    {
        EX_LAYERED = 0x80000,
    }

    internal enum LWA : int
    {
        COLORKEY = 1,
        ALPHA = 2,
    }
}
