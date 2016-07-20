// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32.SafeHandles;
using ReflectionFramework;
using ReflectionFramework.Internal;

namespace Snoop {
    public static class NativeMethods {
        [Flags]
        public enum SnapshotFlags : uint {
            HeapList = 0x00000001,
            Process = 0x00000002,
            Thread = 0x00000004,
            Module = 0x00000008,
            Module32 = 0x00000010,
            Inherit = 0x80000000,
            All = 0x0000001F
        }

        public static IntPtr[] ToplevelWindows {
            get {
                var windowList = new List<IntPtr>();
                var handle = GCHandle.Alloc(windowList);
                try {
                    EnumWindows(EnumWindowsCallback, (IntPtr) handle);
                }
                finally {
                    handle.Free();
                }

                return windowList.ToArray();
            }
        }

        public static Process GetWindowThreadProcess(IntPtr hwnd) {
            int processID;
            GetWindowThreadProcessId(hwnd, out processID);

            try {
                return Process.GetProcessById(processID);
            }
            catch (ArgumentException) {
                return null;
            }
        }

        static bool EnumWindowsCallback(IntPtr hwnd, IntPtr lParam) {
            ((List<IntPtr>) ((GCHandle) lParam).Target).Add(hwnd);
            return true;
        }

        [DllImport("user32.dll")]
        static extern int EnumWindows(EnumWindowsCallBackDelegate callback, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern int GetWindowThreadProcessId(IntPtr hwnd, out int processId);

        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        internal static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32")]
        public static extern IntPtr LoadLibrary(string librayName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern ToolHelpHandle CreateToolhelp32Snapshot(SnapshotFlags dwFlags, int th32ProcessID);

        [DllImport("kernel32.dll")]
        public static extern bool Module32First(ToolHelpHandle hSnapshot, ref MODULEENTRY32 lpme);

        [DllImport("kernel32.dll")]
        public static extern bool Module32Next(ToolHelpHandle hSnapshot, ref MODULEENTRY32 lpme);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hHandle);


        // anvaka's changes below


        public static Point GetCursorPosition() {
            var pos = new Point();
            var win32Point = new POINT();
            if (GetCursorPos(ref win32Point)) {
                pos.X = win32Point.X;
                pos.Y = win32Point.Y;
            }
            return pos;
        }

        public static IntPtr GetWindowUnderMouse() {
            var pt = new POINT();
            if (GetCursorPos(ref pt)) {
                return WindowFromPoint(pt);
            }
            return IntPtr.Zero;
        }

        public static Rect GetWindowRect(IntPtr hwnd) {
            var rect = new RECT();
            GetWindowRect(hwnd, out rect);
            return new Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetCursorPos(ref POINT pt);

        [DllImport("user32.dll")]
        static extern IntPtr WindowFromPoint(POINT Point);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        delegate bool EnumWindowsCallBackDelegate(IntPtr hwnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct MODULEENTRY32 {
            public uint dwSize;
            public uint th32ModuleID;
            public uint th32ProcessID;
            public uint GlblcntUsage;
            public uint ProccntUsage;
            readonly IntPtr modBaseAddr;
            public uint modBaseSize;
            readonly IntPtr hModule;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szModule;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExePath;
        }

        public class ToolHelpHandle : SafeHandleZeroOrMinusOneIsInvalid {
            ToolHelpHandle()
                : base(true) {}

            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
            protected override bool ReleaseHandle() {
                return CloseHandle(handle);
            }
        }
    }        
    [ReflectionHelperAttributes.Wrapper]
    [ReflectionHelperAttributes.AssignableFrom("DevExpress.Xpf.Core.Native.IFrameworkRenderElementContext")]
    public interface IIFrameworkRenderElementContext {
        [ReflectionHelperAttributes.InterfaceMember("DevExpress.Xpf.Core.Native.IFrameworkRenderElementContext")]
        int RenderChildrenCount { get; }
        [ReflectionHelperAttributes.InterfaceMember("DevExpress.Xpf.Core.Native.IFrameworkRenderElementContext")]
        object GetRenderChild(int index);
        [ReflectionHelperAttributes.InterfaceMember("DevExpress.Xpf.Core.Native.IFrameworkRenderElementContext")]
        Size RenderSize { get; }
    }    
    [ReflectionHelperAttributes.Wrapper]
    [ReflectionHelperAttributes.AssignableFrom("DevExpress.Xpf.Core.Native.FrameworkRenderElementContext")]
    public interface IFrameworkRenderElementContext : IIFrameworkRenderElementContext {        
        string Name { get; }
        void Render(DrawingContext drawingContext);
        IIElementHost ElementHost { get; }
        Visibility? Visibility { get; }
        IFrameworkRenderElement Factory { get; }
    }
    [ReflectionHelperAttributes.Wrapper]
    public interface IIElementHost {
        [ReflectionHelperAttributes.InterfaceMember("DevExpress.Xpf.Core.Native.IElementHost")]
        FrameworkElement Parent { get; }
    }    
    [ReflectionHelperAttributes.Wrapper]
    [ReflectionHelperAttributes.AssignableFrom("DevExpress.Xpf.Core.Native.IChrome")]
    [ReflectionHelperAttributes.AssignableFrom("DevExpress.Xpf.Grid.LightweightCellEditor", Inverse = true)]
    public interface IIChrome {
        [ReflectionHelperAttributes.InterfaceMember("DevExpress.Xpf.Core.Native.IChrome")]
        IFrameworkRenderElementContext Root { get; }
    }
    [ReflectionHelperAttributes.Wrapper]
    public interface IChrome : IIChrome {
        
    }

    [ReflectionHelperAttributes.Wrapper]
    [ReflectionHelperAttributes.AssignableFrom("DevExpress.Xpf.Core.Native.RenderControlBaseContext")]
    public interface IRenderControlBaseContext : IFrameworkRenderElementContext {
        Transform GeneralTransform { get; }
        FrameworkElement Control { get; }
    }

    [ReflectionHelperAttributes.Wrapper]
    public interface IFrameworkRenderElement {
        Visibility Visibility { get; set; }
    }    

    public class RenderTreeHelper {
        static bool Is(object obj, string typeName, string typeNamespace, bool isInterface) {
            if (obj == null)
                return false;
            var type = obj.GetType();
            while (type != null) {
                Type[] types = { type };
                if (isInterface) {
                    types = types.Concat(type.GetInterfaces()).ToArray();
                }
                foreach (var typeOrInterface in types) {
                    var isValidType =
                        (String.IsNullOrEmpty(typeNamespace) || String.Equals(typeNamespace, typeOrInterface.Namespace))
                        && (String.IsNullOrEmpty(typeName) || String.Equals(typeName, typeOrInterface.Name));
                    if (isValidType)
                        return true;
                }
                type = type.BaseType;
            }
            return false;
        }

        static Assembly GetCoreAssembly(object obj) {
            if (Is(obj, null, "DevExpress.Xpf.Core.Native", false)) {
                return obj.GetType().Assembly;
            }
            return null;
        }

        [ThreadStatic] static Func<object, IEnumerable> renderDescendants;

        [ThreadStatic] static Func<object, Transform> transformToRoot;

        [ThreadStatic] static Func<object, IEnumerable> renderAncestors;

        [ThreadStatic] static Func<object, object, object> hitTest;

        static object Simplify(object obj) {
            return (obj as IReflectionGeneratedObject)?.Source ?? obj;
        }

        public static IEnumerable<object> RenderDescendants(object context) {
            context = Simplify(context);
            if (renderDescendants == null)
                renderDescendants = ReflectionHelper.CreateInstanceMethodHandler<Func<object, IEnumerable>>(
                    null,
                    "RenderDescendants",
                    BindingFlags.Public | BindingFlags.Static,
                    GetCoreAssembly(context).GetType("DevExpress.Xpf.Core.Native.RenderTreeHelper"),
                    true, typeof(IEnumerable), null
                    );
            return renderDescendants(context).OfType<object>();
        }

        public static Transform TransformToRoot(object frec) {
            frec = Simplify(frec);
            if (transformToRoot == null)
                transformToRoot = ReflectionHelper.CreateInstanceMethodHandler<Func<object, Transform>>(
                    null,
                    "TransformToRoot",
                    BindingFlags.Public | BindingFlags.Static,
                    GetCoreAssembly(frec).GetType("DevExpress.Xpf.Core.Native.RenderTreeHelper"),
                    true, typeof(Transform), null
                    );
            return transformToRoot(frec);
        }

        public static IEnumerable<object> RenderAncestors(object context) {
            context = Simplify(context);
            if (renderAncestors == null)
                renderAncestors = ReflectionHelper.CreateInstanceMethodHandler<Func<object, IEnumerable>>(
                    null,
                    "RenderAncestors",
                    BindingFlags.Public | BindingFlags.Static,
                    GetCoreAssembly(context).GetType("DevExpress.Xpf.Core.Native.RenderTreeHelper"),
                    true, typeof(IEnumerable), null
                    );
            return renderAncestors(context).OfType<object>();
        }

        public static object HitTest(object root, Point point) {
            root = Simplify(root);
            if (hitTest == null)
                hitTest = ReflectionHelper.CreateInstanceMethodHandler<Func<object, object, object>>(
                    null,
                    "HitTest",
                    BindingFlags.Public | BindingFlags.Static,
                    GetCoreAssembly(root).GetType("DevExpress.Xpf.Core.Native.RenderTreeHelper"),
                    true, typeof(object), null, 2
                    );
            return hitTest(root, point);
        }
    }
}