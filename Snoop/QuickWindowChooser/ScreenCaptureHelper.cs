using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Snoop {
    public class ScreenCaptureHelper {
        public static BitmapSource CaptureMonitor(Screen monitor, out Rect bounds, out double scaleX, out double scaleY, out double scaleIfPrimaryX, out double scaleIfPrimaryY) {
            QWCNativeMethods.GetDpiForMonitor(new IntPtr(monitor.GetHashCode()), QWCNativeMethods.DpiType.Effective, out var dpiX, out var dpiY);
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
            IntPtr dc = QWCNativeMethods.GetDC(IntPtr.Zero);
            IntPtr compatibleDc = QWCNativeMethods.CreateCompatibleDC(dc);

            QWCNativeMethods.BITMAPINFO bmi = new QWCNativeMethods.BITMAPINFO();
            bmi.biSize = 40;
            bmi.biWidth = captureRect.Width;
            bmi.biHeight = -captureRect.Height;
            bmi.biPlanes = (short) 1;
            bmi.biBitCount = (short) 32;
            bmi.biCompression = 0;

            IntPtr zero = IntPtr.Zero;
            IntPtr dibSection = QWCNativeMethods.CreateDIBSection(dc, ref bmi, QWCNativeMethods.BMIColorFormat.DIB_RGB_COLORS, ref zero, IntPtr.Zero, 0);
            IntPtr hObject = QWCNativeMethods.SelectObject(compatibleDc, dibSection);
            QWCNativeMethods.BitBlt(compatibleDc, 0, 0, captureRect.Width, captureRect.Height, dc, captureRect.Left, captureRect.Top, 0x00CC0020 | 0x40000000);
            Int32Rect sourceRect = new Int32Rect(0, 0, captureRect.Width, captureRect.Height);
            FormatConvertedBitmap formatConvertedBitmap = new FormatConvertedBitmap(Imaging.CreateBitmapSourceFromHBitmap(dibSection, IntPtr.Zero, sourceRect, BitmapSizeOptions.FromEmptyOptions()), PixelFormats.Rgb24, (BitmapPalette) null, 1.0);
            QWCNativeMethods.ReleaseDC(IntPtr.Zero, dc);
            QWCNativeMethods.SelectObject(compatibleDc, hObject);
            QWCNativeMethods.DeleteObject(dibSection);
            QWCNativeMethods.DeleteDC(compatibleDc);
            formatConvertedBitmap.Freeze();
            return formatConvertedBitmap;
        }
    }
}