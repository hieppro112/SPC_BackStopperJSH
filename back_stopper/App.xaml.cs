using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AutoUpdaterDotNET;

namespace back_stopper
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static Mutex mutex;
        //check version 
        protected override void OnStartup(StartupEventArgs e)
        {
            bool createdNew;
            mutex = new Mutex(true, "BackStopper", out createdNew);
            if (!createdNew)
            {
                MessageBox.Show("Ứng dụng đang chạy");
                Shutdown();
                return;
            }
            //base.OnStartup(e);
            //AutoUpdater.Start(@"\\192.168.122.2\Soft F2\Application\158.BackStopper\Build\updateVersion");
            // Tắt chuyển đổi touch → mouse để nhận đa chạm thật sự
            AppContext.SetSwitch("Switch.System.Windows.Input.Stylus.DisableStylusAndTouchSupport", false);

            base.OnStartup(e);
        }

    }
}
