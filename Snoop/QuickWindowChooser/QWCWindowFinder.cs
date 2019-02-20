using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Snoop {
    public class QWCWindowFinder {
        static List<WindowInfo> GetVisibleWindows() {
            List<WindowInfo> result = new List<WindowInfo>();
            IntPtr childHandle;
            IntPtr parentHandle;

            parentHandle = QWCNativeMethods.GetDesktopWindow();                     
            Stack<IntPtr> children = new Stack<IntPtr>();
            children.Push(parentHandle);
            HashSet<IntPtr> currentChildren = new HashSet<IntPtr>(children);
            children.Push(IntPtr.Zero);            
            while (children.Count!=1) {
                childHandle = children.Pop();
                parentHandle = children.Peek();
                                
                var newChildHandle = QWCNativeMethods.FindWindowEx(parentHandle, childHandle, null, null);
                if (currentChildren.Contains(newChildHandle) || children.Count == 1 && !QWCNativeMethods.IsWindow(newChildHandle))
                    newChildHandle = IntPtr.Zero;
                if (newChildHandle != IntPtr.Zero) {
                    currentChildren.Add(newChildHandle);
                    children.Push(newChildHandle);
                    children.Push(IntPtr.Zero);
                    var wndInfo = new WindowInfo(newChildHandle);
                    if (wndInfo.IsVisible && wndInfo.Bounds.Height>double.Epsilon && wndInfo.Bounds.Width>double.Epsilon)
                        result.Add(wndInfo);
                }

                if (result.Count> 10000)
                    break;
            }
            return result;
        }        
        public static List<WindowInfo> GetSortedWindows() {
               var windows = GetVisibleWindows();
            var validWindows = windows.AsParallel().Where(x => x.IsValidProcess).ToList();
            var hdesktop = QWCNativeMethods.GetDesktopWindow();
            windows.RemoveAll(validWindows.Contains);
            List<WindowInfo> interestWindows = new List<WindowInfo>();
            foreach (var validWindow in validWindows) {
                interestWindows.Add(validWindow);
                for (int i = windows.Count - 1; i >= 0; i--) {
                    var allW = windows[i];
                    if (!QWCNativeMethods.IsWindow(allW.HWnd)) {
                        windows.RemoveAt(i);
                        continue;
                    }

                    var parent = QWCNativeMethods.GetAncestor(allW.HWnd, 1);
                    if (parent != IntPtr.Zero && parent != hdesktop && validWindows.All(x => x.HWnd != parent)) {
                        windows.RemoveAt(i);
                        continue;
                    }
                    if (allW.Bounds.IntersectsWith(validWindow.Bounds)) {
                        interestWindows.Add(allW);
                        windows.RemoveAt(i);                        
                    }                                            
                }
            }

            Dictionary<POINT, IntPtr> wfp = new Dictionary<POINT, IntPtr>();            
            var comparer = Comparer<WindowInfo>.Create((first, second) => {
                var intersection = Rect.Intersect(first.Bounds, second.Bounds);
                if (intersection.IsEmpty)
                    return 0;
                int offsetX = Math.Min(15, (int) (Math.Min(first.Bounds.Width, second.Bounds.Width) / 2));
                int offsetY = Math.Min(15, (int) (Math.Min(first.Bounds.Height, second.Bounds.Height) / 2));
                var points = new[] {
                    intersection.TopLeft + new Vector(offsetX, offsetY),
                    intersection.TopRight + new Vector(-offsetX, offsetY),
                    intersection.BottomRight + new Vector(-offsetX, -offsetY),
                    intersection.BottomLeft + new Vector(-offsetX, offsetY),                    
                }.Where(intersection.Contains);
                int pt1 = 0;
                int pt2 = 0;
                foreach (var point in points) {
                    var pt = new POINT((int) point.X, (int) point.Y);
                    if (!wfp.TryGetValue(pt, out var window)) {
                        window = NativeMethods.WindowFromPoint(pt);
                        if (!interestWindows.Select(x => x.HWnd).Contains(window)) {
                            window = QWCNativeMethods.GetAncestor(window, 1);                            
                        }
                        wfp[pt] = window;
                    }
                    
                    if (window == first.HWnd)
                        pt1++;
                    if (window == second.HWnd)
                        pt2++;
                    if (window == first.Parent)
                        pt1++;
                    if (window == second.Parent)
                        pt2++;                                                            
                }

                if (pt1 > pt2)
                    return 1;
                if (pt2 > pt1)
                    return -1;
                return 0;
            });
            interestWindows = interestWindows.OrderBy(x => x.Bounds.Width * x.Bounds.Height).ToList();
            for (int i = 0; i < interestWindows.Count; i++) {
                for (int j = 0; j < interestWindows.Count; j++) {
                    var wI = interestWindows[i];
                    var wJ = interestWindows[j];
                    if (!wI.Bounds.IntersectsWith(wJ.Bounds))
                        continue;
                    if (comparer.Compare(wI, wJ) < 0) {
                        interestWindows[i] = wJ;
                        interestWindows[j] = wI;
                    }                        
                }
            }

            return interestWindows;
        }
    }
}