using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using HideBar.Models;
using HideBar.Services;

namespace HideBar.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly WindowEnumerator _enumerator = new();
    private readonly WindowController _controller = new();
    private readonly FavoriteStore _favorites = new();
    private readonly HiddenStore _hiddenStore = new();

    public ObservableCollection<WindowInfo> Windows { get; } = new();

    public ICollectionView VisibleView { get; }
    public ICollectionView HiddenView { get; }
    public ICollectionView FavoriteView { get; }

    private WindowInfo? _selectedWindow;
    public WindowInfo? SelectedWindow
    {
        get => _selectedWindow;
        set => SetProperty(ref _selectedWindow, value);
    }

    private string _statusText = "就绪";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private int _visibleCount;
    public int VisibleCount
    {
        get => _visibleCount;
        set => SetProperty(ref _visibleCount, value);
    }

    private int _hiddenCount;
    public int HiddenCount
    {
        get => _hiddenCount;
        set => SetProperty(ref _hiddenCount, value);
    }

    private int _favoriteCount;
    public int FavoriteCount
    {
        get => _favoriteCount;
        set => SetProperty(ref _favoriteCount, value);
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                VisibleView.Refresh();
                HiddenView.Refresh();
                FavoriteView.Refresh();
            }
        }
    }

    // ===== 命令 =====
    public ICommand RefreshCommand { get; }
    public ICommand HideCommand { get; }
    public ICommand ShowCommand { get; }
    public ICommand ToggleCommand { get; }
    public ICommand MinimizeCommand { get; }
    public ICommand TopMostCommand { get; }
    public ICommand HideAllCommand { get; }
    public ICommand ShowAllCommand { get; }
    public ICommand ToggleFavoriteCommand { get; }
    public ICommand HideFromFavoriteCommand { get; }
    public ICommand ShowFromFavoriteCommand { get; }

    public MainViewModel()
    {
        VisibleView = CollectionViewSource.GetDefaultView(Windows);
        HiddenView = new CollectionViewSource { Source = Windows }.View;
        FavoriteView = new CollectionViewSource { Source = Windows }.View;

        VisibleView.Filter = w => FilterVisible((WindowInfo)w!);
        HiddenView.Filter = w => FilterHidden((WindowInfo)w!);
        FavoriteView.Filter = w => FilterFavorite((WindowInfo)w!);

        RefreshCommand = new RelayCommand(_ => Refresh());
        HideCommand = new RelayCommand(_ => SafeHideSelected(), _ => SelectedWindow is { IsHiddenByMe: false } && _controller.IsValid(SelectedWindow.Handle));
        ShowCommand = new RelayCommand(_ => SafeShowSelected(), _ => SelectedWindow is { IsHiddenByMe: true });
        ToggleCommand = new RelayCommand(_ => ToggleSelected(), _ => SelectedWindow != null && _controller.IsValid(SelectedWindow.Handle));
        MinimizeCommand = new RelayCommand(_ => MinimizeSelected(), _ => SelectedWindow != null && _controller.IsValid(SelectedWindow.Handle));
        TopMostCommand = new RelayCommand(_ => ToggleTopMost(), _ => SelectedWindow != null && _controller.IsValid(SelectedWindow.Handle));
        HideAllCommand = new RelayCommand(_ => HideAll());
        ShowAllCommand = new RelayCommand(_ => ShowAll(), _ => Windows.Any(w => w.IsHiddenByMe));
        ToggleFavoriteCommand = new RelayCommand(_ => ToggleFavorite(), _ => SelectedWindow != null);
        HideFromFavoriteCommand = new RelayCommand(w => HideByProcess((WindowInfo)w!));
        ShowFromFavoriteCommand = new RelayCommand(w => ShowByProcess((WindowInfo)w!));

        Refresh();
    }

    // ===== 过滤 =====

    private bool FilterVisible(WindowInfo w) => !w.IsHiddenByMe && MatchesSearch(w);
    private bool FilterHidden(WindowInfo w) => w.IsHiddenByMe && MatchesSearch(w);
    private bool FilterFavorite(WindowInfo w) => _favorites.IsFavorite(w.ProcessName) && MatchesSearch(w);

    private bool MatchesSearch(WindowInfo w)
    {
        if (string.IsNullOrWhiteSpace(_searchText)) return true;
        var q = _searchText;
        return w.ProcessName.Contains(q, StringComparison.OrdinalIgnoreCase)
            || w.Title.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    // ===== 刷新 =====

    public void Refresh()
    {
        try
        {
            var enumerated = _enumerator.EnumerateVisibleTopLevelWindows();
            var existingByHandle = Windows.ToDictionary(w => w.Handle);
            var seenHandles = new HashSet<IntPtr>();

            // 1. 同步可见窗口
            foreach (var w in enumerated)
            {
                seenHandles.Add(w.Handle);
                if (existingByHandle.TryGetValue(w.Handle, out var existing))
                {
                    existing.Title = w.Title;
                    existing.ClassName = w.ClassName;
                    existing.ProcessName = w.ProcessName;
                    existing.IsVisible = true;
                    existing.IsFavorite = _favorites.IsFavorite(w.ProcessName);
                }
                else
                {
                    w.IsFavorite = _favorites.IsFavorite(w.ProcessName);
                    Windows.Add(w);
                }
            }

            // 2. 清理已失效的隐藏窗口（句柄已无效）
            var invalidHidden = Windows.Where(w => w.IsHiddenByMe && !_controller.IsValid(w.Handle)).ToList();
            foreach (var w in invalidHidden)
            {
                Windows.Remove(w);
            }

            // 3. 重新排序：可见在前，按进程名+标题
            var ordered = Windows
                .OrderBy(w => w.IsHiddenByMe)
                .ThenBy(w => w.ProcessName)
                .ThenBy(w => w.Title)
                .ToList();

            Windows.Clear();
            foreach (var w in ordered) Windows.Add(w);

            UpdateCounts();
            VisibleView.Refresh();
            HiddenView.Refresh();
            FavoriteView.Refresh();
            StatusText = $"已刷新 - {DateTime.Now:HH:mm:ss}  |  共 {Windows.Count} 个窗口（{VisibleCount} 可见 / {HiddenCount} 已隐藏）";
        }
        catch (Exception ex)
        {
            StatusText = $"刷新出错: {ex.Message}";
        }
    }

    // ===== 启动时恢复隐藏 =====

    /// <summary>
    /// 启动时调用：读取持久化的隐藏列表，对当前还存在的窗口重新执行隐藏
    /// </summary>
    public void RestoreHiddenOnStartup()
    {
        try
        {
            var stored = _hiddenStore.Load();
            if (stored.Count == 0) return;

            int restored = 0;
            var currentWindows = _enumerator.EnumerateVisibleTopLevelWindows();

            foreach (var entry in stored)
            {
                // 按 进程名+标题 匹配当前窗口
                var match = currentWindows.FirstOrDefault(w =>
                    w.ProcessName.Equals(entry.ProcessName, StringComparison.OrdinalIgnoreCase) &&
                    (string.IsNullOrEmpty(entry.Title) || w.Title.Equals(entry.Title, StringComparison.OrdinalIgnoreCase)));

                if (match != null && _controller.IsValid(match.Handle))
                {
                    if (_controller.Hide(match.Handle))
                    {
                        match.IsHiddenByMe = true;
                        match.IsVisible = false;
                        match.IsFavorite = _favorites.IsFavorite(match.ProcessName);
                        restored++;
                    }
                }
            }

            // 把恢复的窗口加进集合
            foreach (var w in currentWindows.Where(x => x.IsHiddenByMe && !Windows.Any(y => y.Handle == x.Handle)))
            {
                Windows.Add(w);
            }

            UpdateCounts();
            VisibleView.Refresh();
            HiddenView.Refresh();
            FavoriteView.Refresh();
            StatusText = $"启动恢复: 自动隐藏 {restored} 个窗口（来自上次会话）";
        }
        catch (Exception ex)
        {
            StatusText = $"启动恢复失败: {ex.Message}";
        }
    }

    // ===== 业务操作（全部带 try/catch） =====

    private void SafeHideSelected()
    {
        if (SelectedWindow == null) return;
        try
        {
            if (!_controller.IsValid(SelectedWindow.Handle))
            {
                StatusText = "窗口已失效，跳过";
                return;
            }
            if (_controller.Hide(SelectedWindow.Handle))
            {
                SelectedWindow.IsHiddenByMe = true;
                SelectedWindow.IsVisible = false;
                PersistHiddenState();
                UpdateCounts();
                VisibleView.Refresh();
                HiddenView.Refresh();
                StatusText = $"已隐藏: {SelectedWindow.DisplayText}";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"隐藏失败: {ex.Message}";
        }
    }

    private void SafeShowSelected()
    {
        if (SelectedWindow == null) return;
        try
        {
            if (_controller.Show(SelectedWindow.Handle))
            {
                SelectedWindow.IsHiddenByMe = false;
                SelectedWindow.IsVisible = true;
                PersistHiddenState();
                UpdateCounts();
                VisibleView.Refresh();
                HiddenView.Refresh();
                StatusText = $"已显示: {SelectedWindow.DisplayText}";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"显示失败: {ex.Message}";
        }
    }

    private void ToggleSelected()
    {
        if (SelectedWindow == null) return;
        if (SelectedWindow.IsHiddenByMe) SafeShowSelected();
        else SafeHideSelected();
    }

    private void MinimizeSelected()
    {
        if (SelectedWindow == null) return;
        try
        {
            _controller.Minimize(SelectedWindow.Handle);
            StatusText = $"已最小化: {SelectedWindow.DisplayText}";
        }
        catch (Exception ex) { StatusText = $"最小化失败: {ex.Message}"; }
    }

    private void ToggleTopMost()
    {
        if (SelectedWindow == null) return;
        try
        {
            _controller.SetTopMost(SelectedWindow.Handle, true);
            StatusText = $"已置顶: {SelectedWindow.DisplayText}";
        }
        catch (Exception ex) { StatusText = $"置顶失败: {ex.Message}"; }
    }

    private void ToggleFavorite()
    {
        if (SelectedWindow == null) return;
        _favorites.Toggle(SelectedWindow.ProcessName);
        foreach (var w in Windows.Where(w => w.ProcessName == SelectedWindow.ProcessName))
        {
            w.IsFavorite = _favorites.IsFavorite(w.ProcessName);
        }
        FavoriteView.Refresh();
        UpdateCounts();
        StatusText = _favorites.IsFavorite(SelectedWindow.ProcessName)
            ? $"已收藏: {SelectedWindow.ProcessName}"
            : $"已取消收藏: {SelectedWindow.ProcessName}";
    }

    private void HideByProcess(WindowInfo w)
    {
        try
        {
            var targets = Windows.Where(x => x.ProcessName == w.ProcessName && !x.IsHiddenByMe).ToList();
            foreach (var t in targets)
            {
                if (_controller.Hide(t.Handle))
                {
                    t.IsHiddenByMe = true;
                    t.IsVisible = false;
                }
            }
            PersistHiddenState();
            UpdateCounts();
            VisibleView.Refresh();
            HiddenView.Refresh();
            StatusText = $"已隐藏进程 {w.ProcessName} 的 {targets.Count} 个窗口";
        }
        catch (Exception ex) { StatusText = $"批量隐藏失败: {ex.Message}"; }
    }

    private void ShowByProcess(WindowInfo w)
    {
        try
        {
            var targets = Windows.Where(x => x.ProcessName == w.ProcessName && x.IsHiddenByMe).ToList();
            foreach (var t in targets)
            {
                if (_controller.Show(t.Handle))
                {
                    t.IsHiddenByMe = false;
                    t.IsVisible = true;
                }
            }
            PersistHiddenState();
            UpdateCounts();
            VisibleView.Refresh();
            HiddenView.Refresh();
            StatusText = $"已显示进程 {w.ProcessName} 的 {targets.Count} 个窗口";
        }
        catch (Exception ex) { StatusText = $"批量显示失败: {ex.Message}"; }
    }

    private void HideAll()
    {
        try
        {
            foreach (var w in Windows.Where(x => !x.IsHiddenByMe).ToList())
            {
                if (_controller.Hide(w.Handle))
                {
                    w.IsHiddenByMe = true;
                    w.IsVisible = false;
                }
            }
            PersistHiddenState();
            UpdateCounts();
            VisibleView.Refresh();
            HiddenView.Refresh();
            StatusText = "已隐藏全部可见窗口";
        }
        catch (Exception ex) { StatusText = $"全部隐藏失败: {ex.Message}"; }
    }

    private void ShowAll()
    {
        try
        {
            foreach (var w in Windows.Where(x => x.IsHiddenByMe).ToList())
            {
                if (_controller.Show(w.Handle))
                {
                    w.IsHiddenByMe = false;
                    w.IsVisible = true;
                }
            }
            PersistHiddenState();
            UpdateCounts();
            VisibleView.Refresh();
            HiddenView.Refresh();
            StatusText = "已显示全部隐藏窗口";
        }
        catch (Exception ex) { StatusText = $"全部显示失败: {ex.Message}"; }
    }

    // ===== 持久化 =====

    private void PersistHiddenState()
    {
        try
        {
            var entries = Windows
                .Where(w => w.IsHiddenByMe)
                .Select(w => new HiddenEntry
                {
                    Pid = (int)w.ProcessId,
                    ProcessName = w.ProcessName,
                    Title = w.Title,
                    ClassName = w.ClassName,
                    HiddenAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                })
                .ToList();
            _hiddenStore.Save(entries);
        }
        catch
        {
            // 静默
        }
    }

    private void UpdateCounts()
    {
        VisibleCount = Windows.Count(w => !w.IsHiddenByMe);
        HiddenCount = Windows.Count(w => w.IsHiddenByMe);
        FavoriteCount = Windows.Count(w => _favorites.IsFavorite(w.ProcessName));
    }
}
