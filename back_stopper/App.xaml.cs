using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
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
        //check version 
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            AutoUpdater.Start(@"\\192.168.122.2\Soft F2\Application\158.BackStopper\Build\updateVersion");
        }

    }
}
