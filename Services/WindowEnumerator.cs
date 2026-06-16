using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using HideBar.Models;

namespace HideBar.Services;

/// <summary>
/// Win32 窗口枚举服务 - 封装 EnumWindows 等 API
/// </summary>
public class WindowEnumerator
{
    // ===== Win32 API 声明 =====

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassNameW(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("psapi.dll", CharSet = CharSet.Unicode)]
    private static extern uint GetModuleFileNameExW(IntPtr hProcess, IntPtr hModule, StringBuilder lpFilename, uint nSize);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    /// <summary>
    /// 不应该被本程序隐藏的"系统窗口"类名
    /// </summary>
    private static readonly string[] _systemClasses = new[]
    {
        "Shell_TrayWnd",                   // 任务栏
        "Shell_SecondaryTrayWnd",         // 副屏任务栏
        "WorkerW",                         // 桌面 WorkerW
        "Progman",                         // Program Manager
        "Windows.UI.Core.CoreWindow",     // UWP 系统窗口（设置、文件资源管理器宿主）
        "ApplicationFrameWindow",        // UWP 框架窗口宿主
        "DesktopWindowXamlSource",        // XAML Island 宿主
        "XamlExplorerHostIslandWindow",  // 任务切换
        "TopLevelWindowForOverflowXamlIsland", // 系统托盘溢出
        "Xaml_WindowedPopupClass",         // 系统弹出窗口
        "SystemTray_Main",                 // 电池指示器
        "traynotify",                      // 通知区域
        "NotifyIconOverflowWindow",        // 系统托盘溢出
        "WindowsInputExperience",          // 输入法
        "ApplicationManager_DesktopShellWindow", // 应用管理器
        "SysShadow",                       // 窗口 DWM 阴影
        "HintWindow",                      // Windows 工具提示
        "tooltips_class32",                // 通用 tooltip
    };

    /// <summary>
    /// 类名以这些后缀结尾的是 Qt 内部辅助窗口（不需隐藏）
    /// </summary>
    private static readonly string[] _excludeClassSuffixes = new[]
    {
        "QWindowToolSaveBits",             // Qt 临时位图保存窗口
        "QWindowPopupDropShadowSaveBits",  // Qt 弹出窗口阴影
    };

    /// <summary>
    /// 不应该枚举的"自身进程"
    /// </summary>
    private static readonly string[] _selfProcessNames = new[]
    {
        "HideBar",
        "HideBar2",
    };

    private static bool IsSystemClass(string className)
    {
        if (Array.IndexOf(_systemClasses, className) >= 0) return true;
        foreach (var s in _excludeClassSuffixes)
        {
            if (className.EndsWith(s)) return true;
        }
        return false;
    }

    private static bool IsSelfProcess(string processName) =>
        Array.IndexOf(_selfProcessNames, processName) >= 0;

    /// <summary>
    /// 枚举所有可见的顶层窗口（已过滤系统窗口/自身）
    /// </summary>
    public List<WindowInfo> EnumerateVisibleTopLevelWindows()
    {
        var results = new List<WindowInfo>();
        var shellHandle = GetShellWindow();

        EnumWindows((hWnd, lParam) =>
        {
            // 1. 跳过不可见窗口
            if (!IsWindowVisible(hWnd)) return true;

            // 2. 跳过 Shell 窗口（桌面）
            if (hWnd == shellHandle) return true;

            // 3. 跳过子窗口（只要顶层）
            if (GetParent(hWnd) != IntPtr.Zero) return true;

            // 4. 读取窗口信息
            var title = GetWindowText(hWnd);
            var className = GetClassName(hWnd);

            // 5. 跳过系统类
            if (IsSystemClass(className)) return true;

            // 6. 跳过无标题且类名特殊的窗口
            if (string.IsNullOrWhiteSpace(title) &&
                (className.StartsWith("tooltips_class32") ||
                 className.StartsWith("IME") ||
                 className == "MSCTFIME UI" ||
                 className.StartsWith("Default IME") ||
                 className == "IME-Component"))
            {
                return true;
            }

            // 7. 跳过过小的窗口
            if (!GetWindowRect(hWnd, out var rect)) return true;
            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;
            if (width < 50 || height < 20) return true;

            // 8. 获取进程信息
            GetWindowThreadProcessId(hWnd, out uint pid);
            var processName = GetProcessName(pid);

            // 9. 跳过自己
            if (IsSelfProcess(processName)) return true;

            results.Add(new WindowInfo
            {
                Handle = hWnd,
                Title = title,
                ClassName = className,
                ProcessId = pid,
                ProcessName = processName,
                IsVisible = true
            });

            return true;
        }, IntPtr.Zero);

        return results;
    }

    private static string GetWindowText(IntPtr hWnd)
    {
        var sb = new StringBuilder(512);
        GetWindowTextW(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string GetClassName(IntPtr hWnd)
    {
        var sb = new StringBuilder(256);
        GetClassNameW(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string GetProcessName(uint pid)
    {
        try
        {
            using var p = System.Diagnostics.Process.GetProcessById((int)pid);
            return p.ProcessName;
        }
        catch
        {
            return $"PID:{pid}";
        }
    }
}
