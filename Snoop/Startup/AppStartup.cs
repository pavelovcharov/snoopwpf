using System;
using System.Diagnostics;
using System.Threading;
using CommandLine;

namespace Snoop.Startup {
    public static class AppStartup {
        [STAThread]
        public static void Main(string[] param) {
            var sOptions = new StartupOptions();
            new Parser().ParseArguments(param, sOptions);
            if (!string.IsNullOrEmpty(sOptions.StartupApp)) {
                var process = new Process();
                var sInfo = new ProcessStartInfo(sOptions.StartupApp);
                process.StartInfo = sInfo;
                if (process.Start()) {
                    var sleepCount = 0;
                    while (IntPtr.Zero == process.MainWindowHandle && sleepCount++ < 100)
                        Thread.Sleep(100);
                    var mWindow = process.MainWindowHandle;
                    var wInfo = new WindowInfo(mWindow);
                    if (wInfo.IsValidProcess)
                        wInfo.Snoop();
                }
            }
            else {
                App.Main();
            }
        }
    }

    public class StartupOptions {
        [Option('s', "startup", DefaultValue = null)]
        public string StartupApp { get; set; }
    }
}