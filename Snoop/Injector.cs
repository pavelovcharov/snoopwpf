// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Snoop {
    internal class Injector {
        static string Suffix(IntPtr windowHandle) {
            var window = new WindowInfo(windowHandle);
            var bitness = IntPtr.Size == 8 ? "64" : "32";
            var clr = "3.5";


            foreach (var module in window.Modules) {
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

                if
                    (
                    module.szModule.StartsWith("PresentationFramework", StringComparison.OrdinalIgnoreCase) ||
                    module.szModule.StartsWith("PresentationCore", StringComparison.OrdinalIgnoreCase) ||
                    module.szModule.StartsWith("wpfgfx", StringComparison.OrdinalIgnoreCase)
                    ) {
                    if (FileVersionInfo.GetVersionInfo(module.szExePath).FileMajorPart > 3) {
                        clr = "4.0";
                    }
                }
                if (module.szModule.Contains("wow64.dll")) {
                    if (FileVersionInfo.GetVersionInfo(module.szExePath).FileMajorPart > 3) {
                        bitness = "32";
                    }
                }
            }
            return bitness + "-" + clr;
        }

        internal static void Launch(IntPtr windowHandle, Assembly assembly, string className, string methodName, string optFileName = null) {
            var location = Assembly.GetEntryAssembly().Location;
            var directory = Path.GetDirectoryName(location);
            var file = Path.Combine(directory, "ManagedInjectorLauncher" + Suffix(windowHandle) + ".exe");
            if (optFileName != null)
                location = Path.Combine(directory, optFileName);

            Process.Start(file,
                windowHandle + " \"" + location + "\" \"" + className + "\" \"" + methodName + "\"");
        }
        delegate int GetCLRRuntimeHost(Guid uuid, [MarshalAs(UnmanagedType.IUnknown)]out object host);
        internal static void LaunchNetCore(IntPtr windowHandle, Assembly assembly, string className, string methodName, string optFileName = null)
        {
            var location = Assembly.GetEntryAssembly().Location;
            var directory = Path.GetDirectoryName(location);
            var file = Path.Combine(directory, "ManagedInjectorLauncher" + Suffix(windowHandle) + ".exe");
            if (optFileName != null)
                location = Path.Combine(directory, optFileName);

            var core_root = Environment.GetEnvironmentVariable("CORE_ROOT") ?? @"C:\Program Files\dotnet\shared\Microsoft.NETCore.App\3.0.0-preview-27122-01";
            if (string.IsNullOrEmpty(core_root))
                return;
            var handle = NativeMethods.GetModuleHandle(Path.Combine(core_root, "coreclr.dll"));
            if (handle == IntPtr.Zero)
                return;
            var clrRuntimeHostHandle = NativeMethods.GetProcAddress(handle, "GetCLRRuntimeHost");
            if (clrRuntimeHostHandle == IntPtr.Zero)
                return;
            var getCLRRuntimeHostHandle = (GetCLRRuntimeHost)Marshal.GetDelegateForFunctionPointer(clrRuntimeHostHandle, typeof(GetCLRRuntimeHost));
            var result = getCLRRuntimeHostHandle(new Guid("64F6D366-D7C2-4F1F-B4B2-E8160CAC43AF"), out object hostObject);
            if (result != 0)
                return;
            var host = (ICLRRuntimeHost4)hostObject;
            host.ExecuteInDefaultAppDomain(typeof(SnoopUI).Assembly.Location, "Snoop.SnoopUI", "GoBabyGo", null, out var returnValue);

        }


        [ComImport, Guid("64F6D366-D7C2-4F1F-B4B2-E8160CAC43AF"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface ICLRRuntimeHost4
        {

            void Start();
            void Stop();
            void SetHostControl();
            void GetCLRControl();
            void UnloadAppDomain();
            void ExecuteInAppDomain();
            void GetCurrentAppDomainId();
            void ExecuteApplication();
            IntPtr ExecuteInDefaultAppDomain(
             string pwzAssemblyPath,
             string pwzTypeName,
             string pwzMethodName,
             string pwzArgument,
             out IntPtr pReturnValue);
        }

    }
}