using System;

namespace HideBar.Models;

/// <summary>
/// 表示一个 Windows 窗口的信息
/// </summary>
public class WindowInfo
{
    /// <summary>窗口句柄 (HWND)</summary>
    public IntPtr Handle { get; set; }

    /// <summary>窗口标题（可能为空）</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>窗口类名</summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>所属进程 ID</summary>
    public uint ProcessId { get; set; }

    /// <summary>所属进程名</summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>当前是否可见</summary>
    public bool IsVisible { get; set; }

    /// <summary>是否已被本程序隐藏</summary>
    public bool IsHiddenByMe { get; set; }

    /// <summary>是否被用户收藏（按进程）</summary>
    public bool IsFavorite { get; set; }

    /// <summary>显示文本：进程名 + 标题</summary>
    public string DisplayText => string.IsNullOrWhiteSpace(Title)
        ? ProcessName
        : $"{ProcessName}  —  {Title}";

    /// <summary>短标题（用于列表项）</summary>
    public string ShortTitle => string.IsNullOrWhiteSpace(Title)
        ? "(无标题)"
        : Title;
}
