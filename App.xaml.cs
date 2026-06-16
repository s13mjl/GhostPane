using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using HideBar.Models;
using HideBar.Services;
using HideBar.ViewModels;

namespace HideBar;

public partial class App : Application
{
    /// <summary>
    /// 启动入口：解析命令行参数
    /// --test-hide-all = 启动后立刻触发"全部隐藏"，并把 hidden.json 写到磁盘
    /// --test-show-all = 启动后立刻触发"全部显示"，并把 hidden.json 清空
    /// 没有参数 = 正常 GUI 启动
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Length == 0) return;

        bool isHideAll = e.Args[0] == "--test-hide-all";
        bool isShowAll = e.Args[0] == "--test-show-all";
        if (!isHideAll && !isShowAll) return;

        // Create main window directly so we can hook Loaded before the dispatcher pump runs
        var vm = new MainViewModel();
        var win = new MainWindow { DataContext = vm };
        MainWindow = win;

        win.Loaded += (s, args) =>
        {
            if (isHideAll)
            {
                vm.HideAllCommand.Execute(null);
                Console.WriteLine($"[test-hide-all] hidden count: {vm.HiddenCount}");
            }
            else
            {
                vm.ShowAllCommand.Execute(null);
                Console.WriteLine($"[test-show-all] visible count: {vm.VisibleCount}");
            }
            // Flush via the Dispatcher
            win.Dispatcher.BeginInvoke(new Action(() =>
            {
                Thread.Sleep(300);
                Shutdown(0);
            }), System.Windows.Threading.DispatcherPriority.Background);
        };

        win.Show();
    }
}
