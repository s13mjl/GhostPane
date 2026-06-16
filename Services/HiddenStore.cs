using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace HideBar.Services;

/// <summary>
/// 持久化的"被本程序隐藏过的窗口"清单
/// 启动时根据这个清单重新隐藏这些窗口（按 PID + 进程名 + 窗口标题匹配）
/// </summary>
public class HiddenStore
{
    private readonly string _filePath;

    public HiddenStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "HideBar");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "hidden.json");
    }

    public List<HiddenEntry> Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return new List<HiddenEntry>();
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<HiddenEntry>>(json) ?? new List<HiddenEntry>();
        }
        catch
        {
            return new List<HiddenEntry>();
        }
    }

    public void Save(IEnumerable<HiddenEntry> entries)
    {
        try
        {
            var list = new List<HiddenEntry>(entries);
            var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // 静默
        }
    }
}

public class HiddenEntry
{
    public int Pid { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public long HiddenAt { get; set; }  // Unix 时间戳
}
