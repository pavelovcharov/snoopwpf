using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
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

        public List<ScreenSelectorData> openedWindows = new List<ScreenSelectorData>();
        public QuickWindowChooser() {
            var windows = GetWindows().OfType<IntPtr>().Select(x => new WindowInfo(x)).Where(x => x.IsVisible).Reverse().ToArray();
            foreach (var screen in Screen.AllScreens) {  
                var data = new ScreenSelectorData();
                var image = new Image() {
                    Source = CaptureMonitor(screen, out var bounds, out var scaleX, out var scaleY),
                    Stretch = Stretch.UniformToFill,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Width = bounds.Width, 
                    Height = bounds.Height
                };
                data.ColorfulImage = image;
                Image image2 = new Image() {
                    Source = image.Source,
                    Stretch = image.Stretch,
                    VerticalAlignment = image.VerticalAlignment,
                    HorizontalAlignment = image.HorizontalAlignment,
                    Width = image.Width, Height = image.Height,
                    Effect = new GrayscaleShaderEffect() 
                };
                data.GrayScaleImage = image2;
                var windowsInBounds = windows.Where(x => bounds.IntersectsWith(x.Bounds)).Select(x => new WindowInfoEx(x, x.Bounds)).ToArray();
                for (int i = 0; i < windowsInBounds.Length; i++) {
                    var current = windowsInBounds[i];
                    if (current.Bounds.IsEmpty)
                        continue;
                    for (int j = i + 1; j < windowsInBounds.Length; j++) {
                        var next = windowsInBounds[j];
                        if (next.Bounds.IsEmpty)
                            continue;
                        if (!next.Bounds.IntersectsWith(current.Bounds))
                            continue;
                        var intersection = Rect.Intersect(current.Bounds, next.Bounds);                         

                        current.Bounds = new Rect(
                            Math.Abs(current.Bounds.Left - intersection.Left) < 0.1 ? intersection.Right : current.Bounds.Left,
                            Math.Abs(current.Bounds.Top - intersection.Top) < 0.1 ? intersection.Bottom : current.Bounds.Top,
                            Math.Max(0,current.Bounds.Width - intersection.Width),
                            Math.Max(0,current.Bounds.Height - intersection.Height)
                        );
                    }
                }

                var results = windowsInBounds.Where(x => x.WindowInfo.IsValidProcess && !x.Bounds.IsEmpty);
                Viewbox vb = new Viewbox(){};                
                var grid = new Grid(){ClipToBounds = true, Width = bounds.Width, Height = bounds.Height};
                vb.Child = grid;
                grid.Children.Add(image);
                grid.Children.Add(image2);
                
                foreach (var windowInfo in results) {
                    var scaledBounds = new Rect(windowInfo.Bounds.Left * scaleX, windowInfo.Bounds.Top * scaleY, windowInfo.Bounds.Width * scaleX, windowInfo.Bounds.Height * scaleY);
                    var boundsInScreen = new Rect(new Point(scaledBounds.Left - bounds.Left, scaledBounds.Top - bounds.Top), scaledBounds.Size);
                    var border = new Border() {
                        Margin = new Thickness(boundsInScreen.Left, boundsInScreen.Top, 0, 0),
                        Width = boundsInScreen.Width,
                        Height = boundsInScreen.Height,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        BorderThickness = new Thickness(3),
                        BorderBrush = (Brush)new BrushConverter().ConvertFrom("#FF297BDC"),
                        Background = Brushes.Transparent,
                        Effect = new BlurEffect(){Radius = 10, KernelType = KernelType.Gaussian}
                    };
                    var border2 = new Border() {
                        Margin = new Thickness(boundsInScreen.Left, boundsInScreen.Top, 0, 0),
                        Width = boundsInScreen.Width,
                        Height = boundsInScreen.Height,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        BorderThickness = new Thickness(3),
                        BorderBrush = (Brush)new BrushConverter().ConvertFrom("#FF297BDC"),
                    };
                    grid.Children.Add(border2);
                    grid.Children.Add(border);
                    data.Add(windowInfo.WindowInfo, border);
                }                
                var wnd = new Window() {Content = vb, WindowStyle = WindowStyle.None, Left = bounds.Left, Top = bounds.Top, AllowsTransparency = true, Background = Brushes.Transparent, Opacity = 0};
                data.Window = wnd;
                data.Owner = this;
                data.Init();
                openedWindows.Add(data);                
            }
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
        public Image ColorfulImage { get; set; }
        public Window Window { get; set; }
        public QuickWindowChooser Owner { get; set; }
        public Image GrayScaleImage { get; set; }
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
            var blur = border.Effect as BlurEffect;
            var effect = ((GrayscaleShaderEffect) GrayScaleImage.Effect); 
            if (!set) {
                effect.VisibleRect = new Point4D();
                blur.Radius = 10;
            } else {
                blur.Radius = 20;                
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
            foreach (var window in Owner.openedWindows) {
                Application.Current.Shutdown();
            }
        }

        void WndOnLoaded(object sender, RoutedEventArgs e) {
            var wnd = (Window)sender;
            wnd.WindowState = WindowState.Maximized;
            wnd.Opacity = 1d;
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