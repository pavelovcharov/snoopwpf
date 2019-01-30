using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Remoting.Messaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Windows.Threading;
using Orc.Sort;
using Orc.Sort.NSort;
using Orc.Sort.NSort.Generic;
using Snoop.Shaders.Effects;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Image = System.Windows.Controls.Image;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Point = System.Windows.Point;
using Rectangle = System.Drawing.Rectangle;

namespace Snoop {
    public sealed class QuickWindowChooser : IDisposable {
        public delegate bool EnumedWindow(IntPtr handleWindow, ArrayList handles);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool EnumWindows(EnumedWindow lpEnumFunc, ArrayList lParam);

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

        public BitmapSource CaptureMonitor(Screen monitor, out Rect bounds, out double scaleX, out double scaleY, out double scaleIfPrimaryX, out double scaleIfPrimaryY) {
            GetDpiForMonitor(new IntPtr(monitor.GetHashCode()), DpiType.Effective, out var dpiX, out var dpiY);
            if (!monitor.Primary) {
                scaleX = 96d / (double) dpiX;
                scaleY = 96d / (double) dpiY;
                scaleIfPrimaryX = 1d;
                scaleIfPrimaryY = 1d;
            } else {
                scaleIfPrimaryX = 96d / (double) dpiX;
                scaleIfPrimaryY = 96d / (double) dpiY;
                scaleX = 1d;
                scaleY = 1d;
            }


            var captureRect = new Rectangle(
                (int) (monitor.Bounds.Left * scaleX),
                (int) (monitor.Bounds.Top * scaleY),
                (int) (monitor.Bounds.Width * scaleX),
                (int) (monitor.Bounds.Height * scaleY));
            bounds = new Rect(captureRect.Left, captureRect.Top, captureRect.Width, captureRect.Height);
            IntPtr dc = GetDC(IntPtr.Zero);
            IntPtr compatibleDc = CreateCompatibleDC(dc);

            BITMAPINFO bmi = new BITMAPINFO();
            bmi.biSize = 40;
            bmi.biWidth = captureRect.Width;
            bmi.biHeight = -captureRect.Height;
            bmi.biPlanes = (short) 1;
            bmi.biBitCount = (short) 32;
            bmi.biCompression = 0;

            IntPtr zero = IntPtr.Zero;
            IntPtr dibSection = CreateDIBSection(dc, ref bmi, BMIColorFormat.DIB_RGB_COLORS, ref zero, IntPtr.Zero, 0);
            IntPtr hObject = SelectObject(compatibleDc, dibSection);
            BitBlt(compatibleDc, 0, 0, captureRect.Width, captureRect.Height, dc, captureRect.Left, captureRect.Top, 0x00CC0020 | 0x40000000);
            Int32Rect sourceRect = new Int32Rect(0, 0, captureRect.Width, captureRect.Height);
            FormatConvertedBitmap formatConvertedBitmap = new FormatConvertedBitmap(Imaging.CreateBitmapSourceFromHBitmap(dibSection, IntPtr.Zero, sourceRect, BitmapSizeOptions.FromEmptyOptions()), PixelFormats.Rgb24, (BitmapPalette) null, 1.0);
            ReleaseDC(IntPtr.Zero, dc);
            SelectObject(compatibleDc, hObject);
            DeleteObject(dibSection);
            DeleteDC(compatibleDc);
            return formatConvertedBitmap;
        }

        public static IList GetWindows() {
            ArrayList windowHandles = new ArrayList();
            EnumedWindow callBackPtr = GetWindowHandle;
            EnumWindows(callBackPtr, windowHandles);

            return windowHandles;
        }               
          static List<IntPtr> GetWindows3() {
            List<IntPtr> result = new List<IntPtr>();
            IntPtr childHandle;
            IntPtr firstHandle;
            IntPtr parentHandle;

            parentHandle = GetDesktopWindow();                     
            Stack<IntPtr> children = new Stack<IntPtr>();
            children.Push(parentHandle);
            HashSet<IntPtr> currentChildren = new HashSet<IntPtr>(children);
            children.Push(IntPtr.Zero);            
            while (children.Count!=1) {
                childHandle = children.Pop();
                parentHandle = children.Peek();
                                
                var newChildHandle = FindWindowEx(parentHandle, childHandle, null, null);
                if (currentChildren.Contains(newChildHandle) || children.Count == 1 && !IsWindow(newChildHandle))
                    newChildHandle = IntPtr.Zero;
                if (newChildHandle != IntPtr.Zero) {
                    currentChildren.Add(newChildHandle);
                    children.Push(newChildHandle);
                    children.Push(IntPtr.Zero);
                    result.Add(newChildHandle);
                }

                if (result.Count> 10000)
                    break;
            }
            return result;
        }
          static List<IntPtr> GetWindows5() {
              List<IntPtr> result = new List<IntPtr>();
              IntPtr parentHandle;

              parentHandle = GetTopWindow(IntPtr.Zero);
              while (parentHandle != IntPtr.Zero) {
                  result.Add(parentHandle);
                  parentHandle = GetWindow(parentHandle, 2);
              }

              return result;
          }
          static List<IntPtr> GetWindows4(bool alt = false) {
            List<IntPtr> result = new List<IntPtr>();
            /* locals */
            uint lv_Cnt;
            IntPtr lv_hWnd;
            bool lv_Result;
            IntPtr lv_hFirstWnd;
            IntPtr lv_hDeskWnd;
            IntPtr[] lv_List;



            // first try api to get full window list including immersive/metro apps
            lv_List = _Gui_BuildWindowList(IntPtr.Zero, IntPtr.Zero, true, false, 0, out lv_Cnt);
            if (alt)
                lv_List = null;

            // success?
            if (lv_List != null) {
                // loop through list
                while (lv_Cnt-- > 0) {
                    // get handle
                    lv_hWnd = lv_List[lv_Cnt];

                    // filter out the invalid entry (0x00000001) then call the callback
                    if (IsWindow(lv_hWnd))
                        result.Add(lv_hWnd);
                }
            } else {
                // get desktop window, this is equivalent to specifying NULL as hwndParent
                lv_hDeskWnd = GetDesktopWindow();

                // fallback to using FindWindowEx, get first top-level window
                lv_hFirstWnd = FindWindowEx(lv_hDeskWnd, IntPtr.Zero, null, null);

                // init the enumeration
                lv_Cnt = 0;
                lv_hWnd = lv_hFirstWnd;

                // loop through windows found
                // - since 2012 the EnumWindows API in windows has a problem (on purpose by MS)
                //   that it does not return all windows (no metro apps, no start menu etc)
                // - luckally the FindWindowEx() still is clean and working
                
                while (IntPtr.Zero != lv_hWnd) {
                    // call the callback
                    result.Add(lv_hWnd);
                    
                    
                    // get next window
                    lv_hWnd = FindWindowEx(lv_hDeskWnd, lv_hWnd, null, null);

                    // protect against changes in window hierachy during enumeration
                    if (lv_hWnd == lv_hFirstWnd || lv_Cnt++ > 10000)
                        break;
                }
            }

            // return the result
            return result;
        }

        [DllImport("win32u.dll")]
        static extern uint NtUserBuildHwndList
        (
            IntPtr in_hDesk,
            IntPtr in_hWndNext,
            bool in_EnumChildren,
            bool in_RemoveImmersive,
            uint in_ThreadID,
            uint in_Max,
            IntPtr[] out_List,
            out uint out_Cnt
        );
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindow(IntPtr hWnd);
        [DllImport("User32.dll")]
        private static extern IntPtr GetDesktopWindow();
        [DllImport("User32.dll")]
        private static extern IntPtr GetTopWindow(IntPtr hwnd);
        [DllImport("User32.dll")]
        private static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);
        [DllImport("User32.dll")]
        public static extern IntPtr GetParent(IntPtr hwnd);
        [DllImport("User32.dll")]
        public static extern IntPtr GetAncestor(IntPtr hwnd,uint gaFlags);        
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        static IntPtr[] _Gui_BuildWindowList
        (
            IntPtr in_hDesk,
            IntPtr in_hWnd,
            bool in_EnumChildren,
            bool in_RemoveImmersive,
            uint in_ThreadID,
            out uint out_Cnt
        ) {
            /* locals */
            uint lv_Max;
            uint lv_Cnt;
            uint lv_NtStatus;
            IntPtr[] lv_List;

            // initial size of list
            lv_Max = 512;

            // retry to get list
            for (;;) {
                // allocate list
                lv_List = new IntPtr[lv_Max];

                // call the api
                lv_NtStatus = NtUserBuildHwndList(
                    in_hDesk, in_hWnd,
                    in_EnumChildren, in_RemoveImmersive, in_ThreadID,
                    lv_Max, lv_List, out lv_Cnt);

                // success?
                if (lv_NtStatus != 0x0)
                    break;


                // other error then buffersize? or no increase in size?
                if (lv_NtStatus != 0xc0000023 || lv_Cnt <= lv_Max)
                    break;

                // update max plus some extra to take changes in number of windows into account
                lv_Max = lv_Cnt + 16;
            }

            // return the count
            out_Cnt = lv_Cnt;

            // return the list, or NULL when failed
            return lv_List;
        }

        static bool GetWindowHandle(IntPtr windowHandle, ArrayList windowHandles) {
            windowHandles.Add(windowHandle);
            return true;
        }

        [DllImport("Shcore.dll")]
        private static extern IntPtr GetDpiForMonitor([In] IntPtr hmonitor, [In] DpiType dpiType, [Out] out uint dpiX, [Out] out uint dpiY);

        //https://msdn.microsoft.com/en-us/library/windows/desktop/dn280511(v=vs.85).aspx
        public enum DpiType {
            Effective = 0,
            Angular = 1,
            Raw = 2,
        }

        double Area(Rect rect) {
            if (rect.IsEmpty)
                return 0d;
            return rect.Size.Width * rect.Size.Height;
        }
        public class MySwap<T> : ISwap<T> {
            public int SwapCount { get; set; }
            #region Methods
            public void Swap(IList<T> array, int left, int right)
            {
                T swap = array[left];
                array[left] = array[right];
                array[right] = swap;
                SwapCount++;
            }

            public void Set(IList<T> array, int left, int right)
            {
                array[left] = array[right];
                SwapCount++;
            }

            public void Set(IList<T> array, int left, T obj)
            {
                array[left] = obj;
                SwapCount++;
            }
            #endregion
        }
        public List<ScreenSelectorData> openedWindows = new List<ScreenSelectorData>();
        public QuickWindowChooser() {
            var windows = GetWindows3().OfType<IntPtr>().Select(x => new WindowInfo(x)).Where(x=>x.IsVisible).ToList();
            var validWindows = windows.Where(x => x.IsValidProcess && x.Bounds.Width!=0 && x.Bounds.Height!=0).ToArray();
            var hdesktop = GetDesktopWindow();
            windows.RemoveAll(validWindows.Contains);
            List<WindowInfo> interestWindows = new List<WindowInfo>();            
            foreach (var validWindow in validWindows) {
                interestWindows.Add(validWindow);
                for (int i = windows.Count - 1; i >= 0; i--) {
                    var allW = windows[i];
                    if (!IsWindow(allW.HWnd) || allW.Bounds.Width==0 || allW.Bounds.Height==0) {
                        windows.RemoveAt(i);
                        continue;
                    }

                    var parent = GetAncestor(allW.HWnd, 1);
                    if (parent != IntPtr.Zero && parent != hdesktop && !validWindows.Any(x=>x.HWnd==parent)) {
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
//                    first.Bounds.TopLeft,
//                    first.Bounds.TopRight,
//                    first.Bounds.BottomRight,
//                    first.Bounds.BottomLeft,
//                    second.Bounds.TopLeft,
//                    second.Bounds.TopRight,
//                    second.Bounds.BottomRight,
//                    second.Bounds.BottomLeft
//                    first.Bounds.TopLeft + new Vector(offsetX, offsetY),
//                    first.Bounds.TopRight + new Vector(-offsetX, offsetY),
//                    first.Bounds.BottomRight + new Vector(-offsetX, -offsetY),
//                    first.Bounds.BottomLeft + new Vector(-offsetX, offsetY),
//                    second.Bounds.TopLeft + new Vector(offsetX, offsetY),
//                    second.Bounds.TopRight + new Vector(-offsetX, offsetY),
//                    second.Bounds.BottomRight + new Vector(-offsetX, -offsetY),
//                    second.Bounds.BottomLeft + new Vector(-offsetX, offsetY),
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
                            window = GetAncestor(window, 1);                            
                        }
                        wfp[pt] = window;
                    }

                    SetCursorPos(pt.X, pt.Y);
                    
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
                        
//            new QuickSorter<WindowInfo>(comparer, new DefaultSwap<WindowInfo>()).Sort(interestWindows);
            var sorder = new MySwap<WindowInfo>();
//            do {                
//                sorder.SwapCount = 0;
            new BubbleSorter<WindowInfo>(comparer, sorder).Sort(interestWindows);
                new HeapSort<WindowInfo>(comparer, sorder).Sort(interestWindows);
//            } while (sorder.SwapCount != 0);            
//            new OddEvenTransportSorter<WindowInfo>(comparer, new DefaultSwap<WindowInfo>()).Sort(interestWindows);
//            interestWindows.Reverse();
            foreach (var screen in Screen.AllScreens.Where((x,i)=>true)) {  
                var data = new ScreenSelectorData();
                var image2 = new Image() {
                    Source = CaptureMonitor(screen, out var bounds, out var scaleX, out var scaleY, out var primaryScaleX, out var primaryScaleY),
                    Stretch = Stretch.UniformToFill,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Width = bounds.Width,
                    Height = bounds.Height,
                    Effect = new GrayscaleShaderEffect()
                };               
                data.GrayScaleImage = image2;
                Viewbox vb = new Viewbox() {
                    Margin = new Thickness(0)
                };                
                var grid = new Grid(){ClipToBounds = true, Width = bounds.Width, Height = bounds.Height, Background = Brushes.White};                
                var grid3 = new Grid(){ClipToBounds = true, Width = bounds.Width, Height = bounds.Height};
                grid3.Children.Add(image2);
                var effect = new ContourShaderEffect() {Size = new Point(1 / grid.Width, 1 / grid.Height)};
                grid3.Children.Add(new Viewbox() {
                    Child = grid, 
                    Effect = effect
                });                
                vb.Child = grid3;                                

                uint index = 10;
                Dictionary<int, uint> indicesByPID = new Dictionary<int, uint>();
                var realBounds = new Rect(screen.Bounds.Left, screen.Bounds.Top, screen.Bounds.Width, screen.Bounds.Height);
                var windowsInBounds = interestWindows.Where(x => realBounds.IntersectsWith(x.Bounds)).ToArray();
                foreach (var windowInfo in windowsInBounds) {
                    var scaledBounds = new Rect(windowInfo.Bounds.Left * scaleX, windowInfo.Bounds.Top * scaleY, windowInfo.Bounds.Width * scaleX, windowInfo.Bounds.Height * scaleY);
                    var boundsInScreen = new Rect(new Point(scaledBounds.Left - bounds.Left, scaledBounds.Top - bounds.Top), scaledBounds.Size);
					
                    if (!indicesByPID.TryGetValue(windowInfo.OwningProcess.Id, out var currentIndex)) {
                        index+=10;
                        currentIndex = index;
                        indicesByPID[windowInfo.OwningProcess.Id] = currentIndex;
                    }
                    
                    var byte4 = BitConverter.GetBytes(currentIndex);
                    var color = Color.FromArgb(255, byte4[0], byte4[1], 255);
                    if (!windowInfo.IsValidProcess) {
                        color = Color.FromArgb(255, 255, 0, 255);
                    }
                    var border = new Border() {
                        Margin = new Thickness(boundsInScreen.Left, boundsInScreen.Top, 0, 0),
                        Width = boundsInScreen.Width,
                        Height = boundsInScreen.Height,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,                        
                        Background = new SolidColorBrush(color),
                    };
                    grid.Children.Add(border);
                    if (windowInfo.IsValidProcess)
                        data.Add(windowInfo, border, effect);
                }

                var captionHeight = SystemParameters.CaptionHeight / (primaryScaleY * scaleY);
                
                var wnd = new Window() {Content = vb, WindowStyle = WindowStyle.None, Topmost = true, ResizeMode =ResizeMode.NoResize ,Left = bounds.Left, Top = bounds.Top-captionHeight, Width = bounds.Width*primaryScaleX, Height = bounds.Height*primaryScaleY+captionHeight*2, UseLayoutRounding = true};
                RenderOptions.SetEdgeMode(wnd, EdgeMode.Aliased);
                RenderOptions.SetBitmapScalingMode(wnd, BitmapScalingMode.NearestNeighbor);                
                data.Window = wnd;
                data.Owner = this;                
                openedWindows.Add(data);                
            }

            openedWindows[openedWindows.Count - 1].Last = true;
            foreach (var element in openedWindows)
                element.Init();
        }
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetCursorPos(int x, int y);


        void ReleaseUnmanagedResources() {
            // TODO release unmanaged resources here
        }

        public void Dispose() {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        ~QuickWindowChooser() { ReleaseUnmanagedResources(); }
    }

    public class ScreenSelectorData {        
        public Window Window { get; set; }
        public QuickWindowChooser Owner { get; set; }
        public Image GrayScaleImage { get; set; }
        public bool Last { get; set; }
        public ContourShaderEffect Effect { get; set; }
        Dictionary<Border, WindowInfo> infos = new Dictionary<Border, WindowInfo>();

        public void Add(WindowInfo windowInfo, Border border, ContourShaderEffect effect) {
            Effect = effect;
            infos.Add(border, windowInfo);
            border.MouseEnter+=BorderOnMouseEnter;
            border.MouseLeave+=BorderOnMouseLeave;
            border.MouseLeftButtonUp+=BorderOnMouseLeftButtonUp;
        }

        void BorderOnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            var border = sender as Border;
            var info = infos[border];
            info.Snoop();
            Application.Current.Shutdown();
        }

        void BorderOnMouseLeave(object sender, MouseEventArgs e) {
            Update(sender, false);
        }

        void BorderOnMouseEnter(object sender, MouseEventArgs e) {            
            Update(sender, true);
        }

        void Update(object sender, bool set) {
            var border = sender as Border;
            var color = ((SolidColorBrush) border.Background).Color;
            Effect.SetSelection(color, set ? (Color?) color : null);
            var effect = ((GrayscaleShaderEffect) GrayScaleImage.Effect); 
            if (!set) {
                effect.VisibleRect = new Point4D();
            } else {
                var pos = border.TransformToVisual(Window).TransformBounds(new Rect(new Point(), border.RenderSize));
                var w = Window.ActualWidth;
                var h = Window.ActualHeight;
                effect.VisibleRect = new Point4D(pos.Left/w, pos.Top/h, pos.Right/w, pos.Bottom/h);
            }
        }

        public void Init() {
            if(infos.Keys.Count>1 && infos.Keys.Any(x=>x.Width>10 && x.Height>10))
                foreach (var border in infos.Keys) {
                    if (border.Width <= 10 || border.Height <= 10)
                        border.Visibility = Visibility.Collapsed;
                }

            Window.Loaded += WndOnLoaded;
            Window.PreviewKeyDown += WndOnPreviewKeyDown;
            Window.Show();
        }
        
        void WndOnPreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key != Key.Escape)
                return;
            foreach (var window in Owner.openedWindows) {
                Application.Current.Shutdown();
            }
        }

        void WndOnLoaded(object sender, RoutedEventArgs e) {
            if (Last) {
//                foreach (var element in Owner.openedWindows) {
//                    element.Window.WindowState = WindowState.Maximized;
//                }

//                Window.Dispatcher.BeginInvoke(new Action(() => {
//                    foreach (var element in Owner.openedWindows) {
//                        element.Window.Opacity = 1d;
//                    }                    
//                }));
            }            
        }
    }
    public class WindowInfoEx {
        public WindowInfo WindowInfo { get; }
        public Rect Bounds { get; set; }

        public WindowInfoEx(WindowInfo windowInfo, Rect bounds) {
            WindowInfo = windowInfo;
            Bounds = bounds;
        }
    }
}