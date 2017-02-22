using System;
using System.Collections.Generic;
using System.Deployment.Application;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Snoop;

namespace Snoop {
    public class EntryPoint {
        static Mutex mutex = new Mutex(true, "{FE692398-82E8-4E6D-9575-C36C513474D2}"+GetVersion());

        static internal string GetVersion() { return Assembly.GetExecutingAssembly().GetName().Version.ToString(); }

        [STAThread]
        public static void Main(string[] args) {
            bool startNew = false;
            var currentVersion = GetVersion();
            var version = RegistrySettings.LastVersion;
            if (Version.Parse(version) < Version.Parse(currentVersion)) {
                startNew = true;
                RegistrySettings.LastVersion = currentVersion;
            }
            if (startNew) {
                foreach (var process in Process.GetProcessesByName("Snoop")) {
                    var currentId = Process.GetCurrentProcess().Id;
                    if (process.Id != currentId)
                        process.Kill();
                }
            }
            if(mutex.WaitOne(TimeSpan.Zero, true) | startNew) {
                App.Main();
                mutex.ReleaseMutex();
            } else {
                NativeMethods.SendMessage(
                (IntPtr)NativeMethods.HWND_BROADCAST,
                (uint)NativeMethods.WM_SHOWSNOOP,
                IntPtr.Zero,
                IntPtr.Zero);
            }
        }
    }
}
