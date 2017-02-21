using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Snoop;

namespace Snoop {
    public class EntryPoint {
        static Mutex mutex = new Mutex(true, "{FE692398-82E8-4E6D-9575-C36C513474D2}");
        [STAThread]
        public static void Main(string[] args) {
            if(mutex.WaitOne(TimeSpan.Zero, true)) {
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
