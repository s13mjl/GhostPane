using System;
using System.Windows;
using System.Windows.Input;
using HideBar.ViewModels;

namespace HideBar;

/// <summary>
/// 主窗口 code-behind - 三个列表的双击行为 + 启动恢复
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.RestoreHiddenOnStartup();
        }
    }

    /// <summary>收藏栏双击 = 切换该进程所有窗口的隐藏状态</summary>
    private void FavoriteList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (vm.SelectedWindow == null) return;
        vm.HideFromFavoriteCommand.Execute(vm.SelectedWindow);
    }

    /// <summary>可见列表项点击"隐藏"按钮</summary>
    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (sender is System.Windows.Controls.Button btn &&
            btn.DataContext is Models.WindowInfo win)
        {
            vm.SelectedWindow = win;
            vm.HideCommand.Execute(null);
        }
    }

    /// <summary>已隐藏列表项点击"显示"按钮</summary>
    private void ShowButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (sender is System.Windows.Controls.Button btn &&
            btn.DataContext is Models.WindowInfo win)
        {
            vm.SelectedWindow = win;
            vm.ShowCommand.Execute(null);
        }
    }
}
