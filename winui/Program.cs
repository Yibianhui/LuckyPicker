// ================================================================
// Program.cs — WinUI 3 入口（自定义 Main：单实例保护）
// ================================================================
using System;
using System.Threading;
using System.Windows.Forms;
using Microsoft.UI.Xaml;

namespace LuckyPickerWinUI
{
    public static class Program
    {
        static Mutex singleMutex;

        [STAThread]
        static void Main(string[] args)
        {
            // 单实例保护：已有实例时提示并唤起其主窗口
            bool firstInstance;
            singleMutex = new Mutex(true, AppVersion.SingleInstanceMutex, out firstInstance);
            if (!firstInstance)
            {
                bool notified = false;
                try
                {
                    using (var ev = EventWaitHandle.OpenExisting(AppVersion.ShowMainEvent))
                    {
                        ev.Set();
                        notified = true;
                    }
                }
                catch { }
                MessageBox.Show(
                    "YBH幸运摇人器 已在运行中。\n\n" +
                    (notified ? "已为你打开正在运行的主窗口。\n" : "可从桌面托盘图标唤出主窗口。\n") +
                    "（单实例运行，不会重复启动）",
                    AppVersion.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool bootMinimized = args != null && Array.Exists(args,
                a => string.Equals(a, AutoStart.BootArg, StringComparison.OrdinalIgnoreCase));

            AppConfig.Load();
            var core = new LuckyCore();
            core.Load(LuckyCore.LoadDataFile());

            WinRT.ComWrappersSupport.InitializeComWrappers();
            Microsoft.UI.Xaml.Application.Start(p =>
            {
                var context = new AppContextHolder(core, bootMinimized);
                var app = new App();
                app.InitializeComponent();
                app.OnAppStarted(context);
            });
        }
    }

    // 启动上下文：把核心逻辑与启动参数传给 App
    public class AppContextHolder
    {
        public LuckyCore Core;
        public bool BootMinimized;
        public AppContextHolder(LuckyCore core, bool bootMinimized)
        {
            Core = core;
            BootMinimized = bootMinimized;
        }
    }
}
