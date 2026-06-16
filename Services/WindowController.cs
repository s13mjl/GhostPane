using System;
using System.Runtime.InteropServices;
using HideBar.Models;

namespace HideBar.Services;

/// <summary>
/// 窗口控制服务 - 隐藏、显示、最小化、置顶等
/// 所有操作都做了 try/catch 容错 + 句柄有效性检查
/// </summary>
public class WindowController
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextW(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    // ShowWindow 命令
    private const int SW_HIDE = 0;
    private const int SW_SHOWNORMAL = 1;
    private const int SW_SHOWMINIMIZED = 2;
    private const int SW_SHOWMAXIMIZED = 3;
    private const int SW_RESTORE = 9;

    // SetWindowPos 标志
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new(-2);

    /// <summary>检查句柄是否有效（窗口还存在）</summary>
    public bool IsValid(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return false;
        try
        {
            return IsWindow(hWnd);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>隐藏窗口（容错）</summary>
    public bool Hide(IntPtr hWnd)
    {
        if (!IsValid(hWnd)) return false;
        try { return ShowWindow(hWnd, SW_HIDE); }
        catch { return false; }
    }

    /// <summary>显示窗口（容错）</summary>
    public bool Show(IntPtr hWnd)
    {
        if (!IsValid(hWnd)) return false;
        try { return ShowWindow(hWnd, SW_SHOWNORMAL); }
        catch { return false; }
    }

    /// <summary>最小化窗口（容错）</summary>
    public bool Minimize(IntPtr hWnd)
    {
        if (!IsValid(hWnd)) return false;
        try { return ShowWindow(hWnd, SW_SHOWMINIMIZED); }
        catch { return false; }
    }

    /// <summary>置顶窗口（容错）</summary>
    public bool SetTopMost(IntPtr hWnd, bool topMost)
    {
        if (!IsValid(hWnd)) return false;
        try
        {
            var insertAfter = topMost ? HWND_TOPMOST : HWND_NOTOPMOST;
            return SetWindowPos(hWnd, insertAfter, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
        catch { return false; }
    }

    /// <summary>窗口当前是否可见</summary>
    public bool IsVisible(IntPtr hWnd)
    {
        if (!IsValid(hWnd)) return false;
        try { return IsWindowVisible(hWnd); }
        catch { return false; }
    }

    /// <summary>检查窗口是否真的还存在且可见</summary>
    public bool IsWindowActuallyVisible(IntPtr hWnd)
    {
        return IsValid(hWnd) && IsVisible(hWnd);
    }

    /// <summary>读取窗口标题（用于持久化匹配）</summary>
    public string GetTitle(IntPtr hWnd)
    {
        if (!IsValid(hWnd)) return string.Empty;
        try
        {
            var sb = new System.Text.StringBuilder(512);
            GetWindowTextW(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }
        catch { return string.Empty; }
    }

    /// <summary>读取窗口所属 PID</summary>
    public uint GetPid(IntPtr hWnd)
    {
        if (!IsValid(hWnd)) return 0;
        try
        {
            GetWindowThreadProcessId(hWnd, out uint pid);
            return pid;
        }
        catch { return 0; }
    }
}
