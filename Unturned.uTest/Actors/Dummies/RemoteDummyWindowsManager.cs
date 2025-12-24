using System;
using System.Runtime.InteropServices;

// ReSharper disable InconsistentNaming

namespace uTest.Dummies;

internal static unsafe class RemoteDummyWindowsManager
{
    ///// <summary>
    ///// Distributes the windows corresponding to the given window handles into a tile/grid.
    ///// </summary>
    ///// <returns>The number of windows arranged.</returns>
    //public static int TileWindows(bool horizontal, ReadOnlySpan<nint> windows)
    //{
    //    return TileWindows(0, horizontal, null, windows);
    //}
    //
    ///// <summary>
    ///// Distributes the windows corresponding to the given window handles into a tile/grid.
    ///// </summary>
    ///// <returns>The number of windows arranged.</returns>
    //public static unsafe int TileWindows(nint parent, bool horizontal, RectInt? tileArea, ReadOnlySpan<nint> windows)
    //{
    //    RECT* ptr = null;
    //
    //    // ReSharper disable once TooWideLocalVariableScope
    //    RECT r;
    //
    //    if (tileArea.HasValue)
    //    {
    //        RectInt r2 = tileArea.Value;
    //        r.left = r2.x;
    //        r.right = r2.x + r2.width;
    //        r.top = r2.y;
    //        r.bottom = r2.y + r2.height;
    //        ptr = &r;
    //    }
    //
    //    fixed (nint* kids = windows)
    //    {
    //        return TileWindows(parent, horizontal ? MDITILE_HORIZONTAL : MDITILE_VERTICAL, ptr, (uint)windows.Length, kids);
    //    }
    //}

    /// <summary>
    /// Fetches the window handle (HWND) of the Unity window for the currently-running application.
    /// </summary>
    public static nint GetWindowHandle(bool isConsole)
    {
        if (isConsole)
        {
            // you can't get a console window HWND using the other method
            // https://learn.microsoft.com/en-us/troubleshoot/windows-server/performance/obtain-console-window-handle
            string title = Guid.NewGuid().ToString("N");
            string oldTitle = Console.Title;
            Console.Title = title;
            try
            {
                Thread.Sleep(40);

                char* titleBuffer = stackalloc char[33];
                for (int i = 0; i < 32; ++i)
                    titleBuffer[i] = title[i];

                return FindWindowW(null, titleBuffer);
            }
            finally
            {
                Console.Title = oldTitle;
            }
        }

        WindowHandleState state = new WindowHandleState();
        int dwThreadId = GetCurrentThreadId();
        EnumThreadWindows(dwThreadId, state.Enumerator, 0);
        return state.Handle;
    }

    /// <summary>
    /// Places the window in it's correct location in a grid of <paramref name="count"/> windows.
    /// </summary>
    /// <param name="display"><c>HMONITOR</c> monitor handle to the monitor to tile on.</param>
    /// <param name="window"><c>HWND</c> handle to the application to tile.</param>
    /// <param name="index">Index of this window within the list of all windows that need tiling.</param>
    /// <param name="count">Total number of windows that need tiling.</param>
    /// <returns>Whether or not the operation succeeded.</returns>
    public static bool AlignWindowToGrid(nint display, nint window, int index, int count, out bool isPrimaryMonitor)
    {
        // note: TileWindow would work great for this but it un-maximizes all windows
        MONITORINFO mi = default;
        mi.cbSize = (uint)sizeof(MONITORINFO);
        if (!GetMonitorInfoW(display, &mi))
        {
            isPrimaryMonitor = false;
            return false;
        }

        isPrimaryMonitor = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0;
        ref RECT workArea = ref mi.rcWork;

        int width = workArea.right - workArea.left;
        int height = workArea.bottom - workArea.top;

        int gridEdgeSize = (int)Math.Ceiling(Math.Sqrt(count));

        int x = index % gridEdgeSize;
        int y = index / gridEdgeSize;


        double wndCx = width / (double)gridEdgeSize;
        double wndCy = height / (double)gridEdgeSize;
        int wndX = (int)Math.Round(workArea.left + x * wndCx);
        int wndY = (int)Math.Round(workArea.top + y * wndCy);

        return SetWindowPos(window, 0, wndX, wndY, (int)Math.Ceiling(wndCx), (int)Math.Ceiling(wndCy), SWP_NOZORDER);
    }

    private sealed class WindowHandleState
    {
        public nint Handle;

        public bool Enumerator(nint hWnd, nint lParam)
        {
            Handle = hWnd;
            return false;
        }
    }

    private const uint SWP_NOZORDER = 0x0004;

    private const uint MONITOR_DEFAULTTOPRIMARY = 0x00000001;
    private const uint MONITORINFOF_PRIMARY = 0x00000001;

    /// <summary>
    /// Gets a <c>HMONITOR</c> display handle from a <c>HWND</c> window handle.
    /// </summary>
    public static nint GetMonitorHandle(nint hWnd)
    {
        return MonitorFromWindow(hWnd, MONITOR_DEFAULTTOPRIMARY);
    }
    

    /// <summary>
    /// Gets a <c>HMONITOR</c> display handle for the primary monitor.
    /// </summary>
    public static nint GetPrimaryMonitorHandle()
    {
        return MonitorFromWindow(0, MONITOR_DEFAULTTOPRIMARY);
    }

    /// <summary>
    /// Sets the title of a window.
    /// </summary>
    /// <param name="hWnd">Window handle</param>
    /// <param name="title">Null-terminated string.</param>
    public static void SetWindowTitle(nint hWnd, ReadOnlySpan<char> title)
    {
        fixed (char* ptr = title)
        {
            SetWindowTextW(hWnd, ptr);
        }
    }

    private delegate bool EnumThreadDelegate(nint hWnd, nint lParam);

    /// <summary>
    /// Checks if a window is currently visible.
    /// </summary>
    /// <returns>Whether or not the window is currently visible.</returns>
    [DllImport("Kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(nint hWnd);

    [DllImport("User32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumThreadWindows(int dwThreadId, EnumThreadDelegate lpfn, nint lParam);

    [DllImport("User32.dll")]
    private static extern nint FindWindowW(char* lpClassName, char* lpWindowName);

    [DllImport("Kernel32.dll")]
    private static extern int GetCurrentThreadId();

    [DllImport("User32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfoW(nint hMonitor, MONITORINFO* lpmi);

    [DllImport("Kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("Kernel32.dll")]
    private static extern nint MonitorFromWindow(nint hwnd, uint dwFlags);

    [DllImport("User32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowTextW(nint hWnd, char* lpString);


    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }
}