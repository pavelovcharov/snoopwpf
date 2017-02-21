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
using ReflectionFramework.Attributes;
using ReflectionFramework.Internal;

namespace Snoop {
    public static class NativeMethods {
        public const int 
            SWP_NOMOVE = 0x0002,
            SWP_NOSIZE = 0x0001,
            SWP_NOACTIVATE = 0x0010,
            HWND_BROADCAST = 0xffff;
        public static readonly int WM_SHOWSNOOP = RegisterWindowMessage("WM_SHOWSNOOP");
        public static readonly int WM_HOTKEY = 0x0312;
        [DllImport("user32")]
        public static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32")]
        public static extern int RegisterWindowMessage(string message);
        [DllImport("user32.dll", EntryPoint = "RegisterHotKey", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll", EntryPoint = "UnregisterHotKey", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern short GlobalAddAtom(string lpString);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern short GlobalDeleteAtom(short atom);
        [DllImport("user32.dll", EntryPoint = "SetWindowPos", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, int uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        public static Int32 GetWindowLongPtr(IntPtr hWnd, int nIndex) {
            int iResult = 0;
            IntPtr result = IntPtr.Zero;

            if (IntPtr.Size == 4) {
                // use GetWindowLong
                result = GetWindowLongPtr32(hWnd, nIndex);
                iResult = Marshal.ReadInt32(result);
            } else {
                // use GetWindowLongPtr
                result = GetWindowLongPtr64(hWnd, nIndex);
                iResult = IntPtrToInt32(result);
            }

            return iResult;
        }

        public static int IntPtrToInt32(IntPtr intPtr) { return unchecked((int) intPtr.ToInt64()); }

        public static IntPtr SetWindowLongPtr(HandleRef hWnd, int nIndex, IntPtr dwNewLong) {
            if (IntPtr.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        static extern int SetWindowLong32(HandleRef hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        static extern IntPtr SetWindowLongPtr64(HandleRef hWnd, int nIndex, IntPtr dwNewLong);
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
    [Wrapper]
    [AssignableFrom("DevExpress.Xpf.Core.Native.IFrameworkRenderElementContext")]
    public interface IIFrameworkRenderElementContext {
        [InterfaceMember("DevExpress.Xpf.Core.Native.IFrameworkRenderElementContext")]
        int RenderChildrenCount { get; }
        [InterfaceMember("DevExpress.Xpf.Core.Native.IFrameworkRenderElementContext")]
        object GetRenderChild(int index);
        [InterfaceMember("DevExpress.Xpf.Core.Native.IFrameworkRenderElementContext")]
        Size RenderSize { get; }
    }    
    [Wrapper]
    [AssignableFrom("DevExpress.Xpf.Core.Native.FrameworkRenderElementContext")]
    public interface IFrameworkRenderElementContext : IIFrameworkRenderElementContext {        
        string Name { get; }
        void Render(DrawingContext drawingContext);
        IIElementHost ElementHost { get; }
        Visibility? Visibility { get; }
        IFrameworkRenderElement Factory { get; }
    }
    [Wrapper]
    public interface IIElementHost {
        [InterfaceMember("DevExpress.Xpf.Core.Native.IElementHost")]
        FrameworkElement Parent { get; }
    }    
    [Wrapper]
    [AssignableFrom("DevExpress.Xpf.Core.Native.IChrome")]
    [AssignableFrom("DevExpress.Xpf.Grid.LightweightCellEditor", Inverse = true)]
    public interface IIChrome {
        [InterfaceMember("DevExpress.Xpf.Core.Native.IChrome")]
        IFrameworkRenderElementContext Root { get; }
    }
    [Wrapper]
    public interface IChrome : IIChrome {
        
    }

    [Wrapper]
    [AssignableFrom("DevExpress.Xpf.Core.Native.RenderControlBaseContext")]
    public interface IRenderControlBaseContext : IFrameworkRenderElementContext {
        Transform GeneralTransform { get; }
        FrameworkElement Control { get; }
    }

    [Wrapper]
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
            return (obj as IReflectionHelperInterfaceWrapper)?.Source ?? obj;
        }

        public static IEnumerable<object> RenderDescendants(object context) {
            context = Simplify(context);
            if (renderDescendants == null)
                renderDescendants = ReflectionHelper.CreateInstanceMethodHandler<Func<object, IEnumerable>>(
                    null,
                    "RenderDescendants",
                    BindingFlags.Public | BindingFlags.Static,
                    GetCoreAssembly(context).GetType("DevExpress.Xpf.Core.Native.RenderTreeHelper"),
                    true, typeof(IEnumerable)
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
                    true, typeof(Transform)
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
                    true, typeof(IEnumerable)
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