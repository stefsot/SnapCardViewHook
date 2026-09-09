using ManagedInjector;
using SnapCardViewHook.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnapCardViewHook.Launcher
{
    internal static class Loader
    {
        public static string TargetProcessName { get; set; } = "SNAP";
        
        public static Process GetActiveSnapProcess()
        {
            return Process.GetProcessesByName(TargetProcessName).FirstOrDefault();
        }

        public static bool Inject(string settings = null)
        {
            var process = GetActiveSnapProcess();

            if (process == null)
                return false;

            var type = typeof(Bootstrap);

            var transportData = new InjectorData
            {
                AssemblyName = type.Assembly.Location,
                ClassName = type.FullName,
                MethodName = nameof(Bootstrap.Init),
                SettingsFile = settings
            };

            Injector.Launch(process.MainWindowHandle, transportData);

            return true;
        }
    }
}
