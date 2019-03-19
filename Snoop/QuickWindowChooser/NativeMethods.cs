using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace Snoop {
    public class QWCNativeMethods {
        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr onj);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        public struct BITMAPINFO {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)]
            public byte[] bmiColors;
        }

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO bmi, BMIColorFormat iUsage, ref IntPtr ppvBits, IntPtr hSection, int dwOffset);

        public enum BMIColorFormat {
            DIB_RGB_COLORS,
            DIB_PAL_COLORS,
        }

        [DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [DllImport("gdi32.dll")]
        public static extern bool BitBlt(IntPtr hdc, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

        [DllImport("User32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        public static extern IntPtr DeleteDC(IntPtr hdc);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindow(IntPtr hWnd);
        [DllImport("User32.dll")]
        public static extern IntPtr GetDesktopWindow();        
        [DllImport("User32.dll")]
        public static extern IntPtr GetParent(IntPtr hwnd);
        [DllImport("User32.dll")]
        public static extern IntPtr GetAncestor(IntPtr hwnd,uint gaFlags);        
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);
   
        [DllImport("Shcore.dll")]
        public static extern IntPtr GetDpiForMonitor([In] IntPtr hmonitor, [In] DpiType dpiType, [Out] out uint dpiX, [Out] out uint dpiY);
        
        public enum DpiType {
            Effective = 0,
            Angular = 1,
            Raw = 2,
        }               
    }
     public class WindowInfo {
        static readonly Dictionary<int, bool> processIDToValidityMap = new Dictionary<int, bool>();
        IEnumerable<NativeMethods.MODULEENTRY32> _modules;

        public WindowInfo(IntPtr hwnd) {
            HWnd = hwnd;
        }

        public IEnumerable<NativeMethods.MODULEENTRY32> Modules {
            get {
                if (_modules == null)
                    _modules = GetModules();
                return _modules;
            }
        }

        Rect? bounds;
        public Rect Bounds { get { return (Rect) (bounds ?? (bounds = GetBounds())); } }

        bool? isVisible;
        public bool IsVisible { get { return (bool) (isVisible ?? (isVisible = GetIsVisible())); } }

        bool GetIsVisible() { return NativeMethods.IsWindowVisible(HWnd); }
        Rect GetBounds() {
            return  NativeMethods.GetWindowRect(HWnd);            
        }

        public bool IsValidProcess {
            get {
                var isValid = false;
                try {
                    if (HWnd == IntPtr.Zero)
                        return false;

                    var process = OwningProcess;
                    if (process == null)
                        return false;

                    // see if we have cached the process validity previously, if so, return it.
                    if (processIDToValidityMap.TryGetValue(process.Id, out isValid))
                        return isValid;

                    // else determine the process validity and cache it.
                    if (process.Id == Process.GetCurrentProcess().Id) {
                        isValid = false;

                        // the above line stops the user from snooping on snoop, since we assume that ... that isn't their goal.
                        // to get around this, the user can bring up two snoops and use the second snoop ... to snoop the first snoop.
                        // well, that let's you snoop the app chooser. in order to snoop the main snoop ui, you have to bring up three snoops.
                        // in this case, bring up two snoops, as before, and then bring up the third snoop, using it to snoop the first snoop.
                        // since the second snoop inserted itself into the first snoop's process, you can now spy the main snoop ui from the
                        // second snoop (bring up another main snoop ui to do so). pretty tricky, huh! and useful!
                    }
                    else {
                        // a process is valid to snoop if it contains a dependency on PresentationFramework, PresentationCore, or milcore (wpfgfx).
                        // this includes the files:
                        // PresentationFramework.dll, PresentationFramework.ni.dll
                        // PresentationCore.dll, PresentationCore.ni.dll
                        // wpfgfx_v0300.dll (WPF 3.0/3.5)
                        // wpfgrx_v0400.dll (WPF 4.0)

                        // note: sometimes PresentationFramework.dll doesn't show up in the list of modules.
                        // so, it makes sense to also check for the unmanaged milcore component (wpfgfx_vxxxx.dll).
                        // see for more info: http://snoopwpf.codeplex.com/Thread/View.aspx?ThreadId=236335

                        // sometimes the module names aren't always the same case. compare case insensitive.
                        // see for more info: http://snoopwpf.codeplex.com/workitem/6090

                        foreach (var module in Modules) {
                            if
                                (
                                module.szModule.StartsWith("PresentationFramework", StringComparison.OrdinalIgnoreCase) ||
                                module.szModule.StartsWith("PresentationCore", StringComparison.OrdinalIgnoreCase) ||
                                module.szModule.StartsWith("wpfgfx", StringComparison.OrdinalIgnoreCase)
                                ) {
                                isValid = true;
                                break;
                            }
                        }
                    }

                    processIDToValidityMap[process.Id] = isValid;
                }
                catch (Exception) {}
                return isValid;
            }
        }

        Process op;
        public Process OwningProcess {
            get { return op ?? (op = NativeMethods.GetWindowThreadProcess(HWnd)); }
        }

        public IntPtr HWnd { get; }

        IntPtr? parent;

        public IntPtr Parent {
            get { return (IntPtr) (parent ?? (parent = QWCNativeMethods.GetParent(HWnd))); }
        }

        public string Description {
            get {
                var process = OwningProcess;
                return process.MainWindowTitle + " - " + process.ProcessName + " [" + process.Id + "]";
            }
        }

        public event EventHandler<AttachFailedEventArgs> AttachFailed;

        /// <summary>
        ///     Similar to System.Diagnostics.WinProcessManager.GetModuleInfos,
        ///     except that we include 32 bit modules when Snoop runs in 64 bit mode.
        ///     See http://blogs.msdn.com/b/jasonz/archive/2007/05/11/code-sample-is-your-process-using-the-silverlight-clr.aspx
        /// </summary>
        IEnumerable<NativeMethods.MODULEENTRY32> GetModules() {
            int processId;
            NativeMethods.GetWindowThreadProcessId(HWnd, out processId);

            var me32 = new NativeMethods.MODULEENTRY32();
            var hModuleSnap =
                NativeMethods.CreateToolhelp32Snapshot(
                    NativeMethods.SnapshotFlags.Module | NativeMethods.SnapshotFlags.Module32, processId);
            if (!hModuleSnap.IsInvalid) {
                using (hModuleSnap) {
                    me32.dwSize = (uint) Marshal.SizeOf(me32);
                    if (NativeMethods.Module32First(hModuleSnap, ref me32)) {
                        do {
                            yield return me32;
                        } while (NativeMethods.Module32Next(hModuleSnap, ref me32));
                    }
                }
            }
        }

        public override string ToString() {
            return Description;
        }
        [DllImport("user32.dll", SetLastError=true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);
        [Flags]
        public enum ProcessAccessFlags : uint
        {
            All = 0x001F0FFF,
            Terminate = 0x00000001,
            CreateThread = 0x00000002,
            VirtualMemoryOperation = 0x00000008,
            VirtualMemoryRead = 0x00000010,
            VirtualMemoryWrite = 0x00000020,
            DuplicateHandle = 0x00000040,
            CreateProcess = 0x000000080,
            SetQuota = 0x00000100,
            SetInformation = 0x00000200,
            QueryInformation = 0x00000400,
            QueryLimitedInformation = 0x00001000,
            Synchronize = 0x00100000
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(
            ProcessAccessFlags processAccess,
            bool bInheritHandle,
            uint processId
        );        
        [DllImport("psapi.dll")]
        static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, [Out] StringBuilder lpBaseName, [In] [MarshalAs(UnmanagedType.U4)] int nSize);
        public void Snoop() {
            Mouse.OverrideCursor = Cursors.Wait;
            try {
                GetWindowThreadProcessId(HWnd, out var pId);
                bool isNetCoreApp = false;
#if !NETCORE
                using (var proc = Process.GetProcessById(pId)) {
                    var file = proc.MainModule.FileName;
                    using (var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true)) {
                        //try {
                            //var platformAsm = AssemblyDefinition.ReadAssembly(file);
                            //foreach (var attr in platformAsm.CustomAttributes) {
                            //    if (attr.AttributeType.FullName != "System.Runtime.Versioning.TargetFrameworkAttribute") continue;
                            //    var targetFrameworkVersion = attr.Properties[0].Argument.Value.ToString();
                            //    if (targetFrameworkVersion.Contains(".NET Framework"))
                                    isNetCoreApp = false;
                        //    }
                        //} catch {
                        //    //seems we're in the .netcore app
                        //    isNetCoreApp = true;
                        //}
                    }
                }
#endif

                if (!isNetCoreApp)
                    Injector.Launch(HWnd, typeof(SnoopUI).Assembly, typeof(SnoopUI).FullName, "GoBabyGo");
                else
                    Injector.LaunchNetCore(HWnd, typeof(SnoopUI).Assembly, typeof(SnoopUI).FullName, "GoBabyGo", @"NetCore\SnoopNetCorev3.exe");
            }
            catch (Exception e) {
                OnFailedToAttach(e);
            }
            Mouse.OverrideCursor = null;
        }

        void OnFailedToAttach(Exception e) {
            var handler = AttachFailed;
            if (handler != null) {
                handler(this, new AttachFailedEventArgs(e, Description));
            }
        }
    }

    public class AttachFailedEventArgs : EventArgs {
        public AttachFailedEventArgs(Exception attachException, string windowName) {
            AttachException = attachException;
            WindowName = windowName;
        }

        public Exception AttachException { get; }
        public string WindowName { get; }
    }

    public class AttachFailedHandler {

        public AttachFailedHandler(WindowInfo window) {
            window.AttachFailed += OnSnoopAttachFailed;
        }

        void OnSnoopAttachFailed(object sender, AttachFailedEventArgs e) {
            MessageBox.Show
                (
                    string.Format
                        (
                            "Failed to attach to {0}. Exception occured:{1}{2}",
                            e.WindowName,
                            Environment.NewLine,
                            e.AttachException
                        ),
                    "Can't Snoop the process!"
                );            
        }
    }
}