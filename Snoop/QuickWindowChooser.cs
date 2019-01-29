using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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

        public BitmapSource CaptureMonitor(Screen monitor, out Rect bounds, out double scaleX, out double scaleY) {
            GetDpiForMonitor(new IntPtr(monitor.GetHashCode()), DpiType.Effective, out var dpiX, out var dpiY);
            scaleX = 1;
            scaleY = 1;
            if (!monitor.Primary) {
                scaleX = 96d/(double) dpiX;
                scaleY = 96d/(double) dpiY;
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

        public static ArrayList GetWindows() {
            ArrayList windowHandles = new ArrayList();
            EnumedWindow callBackPtr = GetWindowHandle;
            EnumWindows(callBackPtr, windowHandles);

            return windowHandles;
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

        public List<ScreenSelectorData> openedWindows = new List<ScreenSelectorData>();
        public QuickWindowChooser() {
            var windows = GetWindows().OfType<IntPtr>().Select(x => new WindowInfo(x)).Where(x => x.IsVisible).Reverse().ToArray();
            foreach (var screen in Screen.AllScreens.Where((x,i)=>i<1)) {  
                var data = new ScreenSelectorData();
                var image2 = new Image() {
                    Source = CaptureMonitor(screen, out var bounds, out var scaleX, out var scaleY),
                    Stretch = Stretch.UniformToFill,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Width = bounds.Width,
                    Height = bounds.Height,
                    Effect = new GrayscaleShaderEffect()
                };               
                data.GrayScaleImage = image2;
                Viewbox vb = new Viewbox() {
                    Margin = new Thickness(7)
                };                
                var grid = new Grid(){ClipToBounds = true, Width = bounds.Width, Height = bounds.Height, Background = Brushes.White};                
                var grid3 = new Grid(){ClipToBounds = true, Width = bounds.Width, Height = bounds.Height};
                grid3.Children.Add(image2);
                grid3.Children.Add(new Viewbox() {
                    Child = grid, 
//                    Effect = new ContourShaderEffect() {Size = new Point(1/grid.Width, 1/grid.Height)}
                });                
                vb.Child = grid3;                                

                uint index = 10;
                Dictionary<int, uint> indicesByPID = new Dictionary<int, uint>();
                foreach (var windowInfo in windows) {
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
                        Margin = new Thickness(boundsInScreen.Left-1, boundsInScreen.Top-1, 0, 0),
                        Width = boundsInScreen.Width+1,
                        Height = boundsInScreen.Height+1,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,                        
                        Background = new SolidColorBrush(color),
                    };
                    grid.Children.Add(border);
                    if (windowInfo.IsValidProcess)
                        data.Add(windowInfo, border);
                }                
                var wnd = new Window() {Content = vb, WindowStyle = WindowStyle.None, Left = bounds.Left, Top = bounds.Top, AllowsTransparency = true, Background = Brushes.Transparent, Opacity = 0, UseLayoutRounding = true};
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
        Dictionary<Border, WindowInfo> infos = new Dictionary<Border, WindowInfo>();

        public void Add(WindowInfo windowInfo, Border border) {
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
                foreach (var element in Owner.openedWindows) {
                    element.Window.WindowState = WindowState.Maximized;
                }

                Window.Dispatcher.BeginInvoke(new Action(() => {
                    foreach (var element in Owner.openedWindows) {
                        element.Window.Opacity = 1d;
                    }                    
                }));
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