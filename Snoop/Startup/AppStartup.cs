using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Shell;
using CommandLine;
using MessageBox = System.Windows.MessageBox;

namespace Snoop.Startup {
    public static class AppStartup {
        [STAThread]
        public static void Main(string[] param) {            
            if (!IsAdministrator()) {
                var path = System.Reflection.Assembly.GetExecutingAssembly().CodeBase;
                ProcessStartInfo info = new ProcessStartInfo(path);
                info.UseShellExecute = true;
                info.Verb = "runas";
                info.Arguments = string.Join(" ", param);
                try {
                    Process.Start(info);
                } catch (Win32Exception ex) {
                    if (ex.NativeErrorCode == 1223)
                        MessageBox.Show("Administrator permission required");
                    else
                        throw;
                }
                return;
            }


#if !NETCORE
            var sOptions = new StartupOptions();
            new Parser().ParseArguments(param, sOptions);
#else            
            var sOptions = new Parser().ParseArguments<StartupOptions>(param).MapResult(x => x, x => new StartupOptions());
#endif            
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
            } else {
                EntryPoint.Main(param);
            }
        }

        public static bool IsAdministrator() {
            return (new WindowsPrincipal(WindowsIdentity.GetCurrent()))
                .IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public class StartupOptions {
#if !NETCORE
        [Option('s', "startup", DefaultValue = null)]
        public string StartupApp { get; set; }
        [Option('o', "options", DefaultValue = false)]
        public bool ShowOptions { get; set; }
#else
        [Option('s', "startup", Default = null)]
        public string StartupApp { get; set; }
        [Option('o', "options", Default = false)]
        public bool ShowOptions { get; set; }
#endif
    }
}