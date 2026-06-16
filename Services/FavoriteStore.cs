using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using HideBar.Models;

namespace HideBar.Services;

/// <summary>
/// 收藏夹存储 - 持久化到 %APPDATA%\HideBar\favorites.json
/// 收藏按进程名（ProcessName）记录，跨进程实例保持
/// </summary>
public class FavoriteStore
{
    private readonly string _filePath;
    private HashSet<string> _favorites = new(StringComparer.OrdinalIgnoreCase);

    public FavoriteStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "HideBar");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "favorites.json");
        Load();
    }

    public IReadOnlyCollection<string> All => _favorites;

    public bool IsFavorite(string processName) => _favorites.Contains(processName);

    public void Toggle(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return;
        if (_favorites.Contains(processName))
            _favorites.Remove(processName);
        else
            _favorites.Add(processName);
        Save();
    }

    public void Add(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return;
        if (_favorites.Add(processName)) Save();
    }

    public void Remove(string processName)
    {
        if (_favorites.Remove(processName)) Save();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var json = File.ReadAllText(_filePath);
            var list = JsonSerializer.Deserialize<List<string>>(json);
            if (list != null) _favorites = new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            // 加载失败保持空集合
        }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_favorites, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // 静默失败
        }
    }
}
