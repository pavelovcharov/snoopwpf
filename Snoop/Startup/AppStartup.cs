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

            try { NativeMethods.SetProcessDpiAwarenessContext((int) NativeMethods.DPI_AWARENESS_CONTEXT.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2); }
            catch {
                try { NativeMethods.SetProcessDpiAwareness(NativeMethods.PROCESS_DPI_AWARENESS.Process_Per_Monitor_DPI_Aware); }
                catch { NativeMethods.SetProcessDPIAware(); }
            }


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
        [Option('s', "startup", DefaultValue = null)]
        public string StartupApp { get; set; }
        [Option('o', "options", DefaultValue = false)]
        public bool ShowOptions { get; set; }
    }
}